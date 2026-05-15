using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Bradford.Core.Interfaces;
using Bradford.Core.Models;
using Microsoft.Extensions.Logging;

namespace Bradford.Core.Services;

public class AgentService : IAgentService
{
    private readonly ILlmService         _llm;
    private readonly IRagService         _rag;
    private readonly IConversationService _conversation;
    private readonly IToolService        _tools;
    private readonly ILogger<AgentService> _logger;

    private const int MaxToolRounds = 3;   // prevent infinite loops

    // camelCase serialization so frontend reads structured?.libraries (not structured?.Libraries)
    private static readonly System.Text.Json.JsonSerializerOptions _camelCase = new()
        { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

    private const string SystemPrompt = """
        You are Alex, a friendly and knowledgeable AI assistant for Bradford Council.
        You help Bradford residents with any council service or question.

        {RAG_CONTEXT}

        ## Tools available — use them proactively
        - **lookup_addresses_for_postcode** — get real property addresses for a postcode
        - **get_bin_dates_for_address** — get full bin collection info for an address
        - **search_bradford_council** — search bradford.gov.uk for any topic
        - **fetch_council_page** — read a specific council page in full
        - **get_council_tax_info** — all council tax info: paying, bands, single person discount, student discount, discounts, exemptions, arrears, debt, enforcement, change of address, appeals, empty properties, landlords, bills, reduction letters (do NOT use for band lookups)
        - **lookup_council_tax_band** — look up the actual council tax band and annual amount for a specific postcode (use only when user wants their specific band/amount)
        - **check_planning_application** — planning portal search
        - **find_local_services** — libraries with distance from postcode, parks, leisure centres
        - **get_library_details** — opening hours, facilities, address and how to join for a specific library (call this after user picks a library)
        - **find_schools_near_postcode** — find schools near a Bradford postcode with Ofsted ratings and distance
        - **get_school_details** — full info for a specific school: Ofsted, type, phase, age range, admissions link
        - **get_education_info** — Bradford education policies: admissions, SEND, free school meals, term dates
        - **get_benefits_info** — Bradford Council benefits: Housing Benefit, Council Tax Reduction, Universal Credit, Free School Meals, Crisis Fund, Assisted Purchase, overpayments, appeals, change of circumstances, cost of living help, landlord info

        ## Library queries — strict flow
        0. If user directly names a specific library (e.g. "Idle Library", "Bradford Central", "Shipley library")
           → call **get_library_details(library_name="{name}")** immediately. Do NOT ask for a postcode.
        1. Scan the CURRENT message for a Bradford postcode (BD…). If one is present → go to step 2 immediately.
           Only if NO postcode anywhere in the current message or conversation → ask: "What's your Bradford postcode?"
        2. Call **find_local_services(service="library", location="{postcode}")** — returns ALL libraries sorted by distance
        3. Present the full numbered table. End with: "Tell me which library you'd like — say its name or number."
        4. When user picks a library by name or number → call **get_library_details(library_name="{name}")** immediately
        5. Present the details in sections: Facilities, Opening Hours, How to Join
        6. Format facilities as a bullet list; use a table for opening hours if available

        ## Council tax — two flows

        ### Flow A — Band / amount lookup (needs postcode)
        TRIGGER: "what band am I", "how much is my council tax", "what is my council tax", "council tax amount",
                 "my band", "check my band", or any Bradford postcode (BD…) mentioned alongside council tax.
        1. Scan the CURRENT message for a Bradford postcode. If present → call **lookup_council_tax_band** immediately.
           If no postcode → ask: "What's your Bradford postcode? I'll look up your band and amount."
           NEVER call lookup_addresses_for_postcode for council tax — it is only for bin dates.
        2. Postcode identified → call **lookup_council_tax_band(postcode="{postcode}")** — no other tool first.
        3. Write ONE sentence: "I found X properties at {postcode} — tap yours to see your exact band and annual charge."
        4. User provides house number → call **lookup_council_tax_band(postcode="{postcode}", address="{house_number}")**.

        ### Flow B — Council tax information (no postcode needed)
        TRIGGER: questions about paying, discounts, arrears, appeals, empty properties, students, single person,
                 moving home, debt, what council tax is, landlords, bills, enforcement, reduction letters.
        → call **get_council_tax_info(query="{topic}")** immediately. Do NOT ask for a postcode.

        Routing table for Flow B:
        - "how to pay" / "direct debit" / "paypoint" → query = "how to pay council tax"
        - "single person" / "live alone" → query = "single person discount"
        - "student" / "university" → query = "student discount"
        - "discount" / "reduce my bill" / "exemption" → query = "reduce council tax bill"
        - "can't pay" / "arrears" / "struggling" → query = "problems paying council tax"
        - "debt advice" / "enforcement" / "bailiff" / "court" → query = "council tax debt enforcement"
        - "moved" / "change of address" / "change of circumstances" → query = "change of address council tax"
        - "death" / "someone died" / "bereavement" → query = "reporting death council tax"
        - "appeal" / "dispute" / "wrong band" / "valuation" → query = "council tax appeal"
        - "empty property" / "second home" / "holiday home" → query = "empty property council tax"
        - "landlord" / "HMO" / "tenancy" → query = "landlord council tax"
        - "what is council tax" / "what does it fund" → query = "what is council tax"
        - "my bill" / "2026" / "this year's bill" → query = "council tax bill 2026"
        - "reduction letter" / "what does my letter mean" → query = "council tax reduction letter"
        - "contact" / "phone number" → query = "contact council tax team"

        ## CRITICAL — Council tax card handling
        When [[COUNCIL_TAX_CARD]] appears in a tool result:
        - The UI shows an interactive council tax card automatically
        - Write ONE short sentence only with the band and amount
        - Do NOT reproduce the full rates table yourself
        - Do NOT call get_council_tax_info after a band lookup

        ## Council tax address selected — reply in exactly 3 separate bubbles
        TRIGGER: user message "I live at [address]. My council tax is Band [X], annual charge [annual] ([monthly]/month)..."
        - The band and amounts are stated IN THE MESSAGE — use them exactly. Do NOT invent or change any figures.
        - Do NOT call any tool.
        - Separate each reply with a blank line so they appear as separate bubbles.

        **Bubble 1 — Band & amount (use the exact figures from the message):**
        "Your property at **[address]** is **Band [X]** — **[annual]/year** (about **[monthly]/month** over 10 months)."

        **Bubble 2 — How to pay:**
        "You can pay your council tax in several ways:
        - **Online / Direct Debit** — [Pay your Council Tax](https://www.bradford.gov.uk/council-tax/pay-your-council-tax/pay-your-council-tax/) — choose 5th, 10th, 15th, 25th or 28th of the month, spread over **10 or 12 months**
        - **Phone** — automated line **0345 145 0071** (24/7, just your reference number)
        - **Bank transfer / Standing order** — Sort code **56-00-36** · Account **00143790** · Name: **City of Bradford Metropolitan Council** · Reference: your Council Tax reference number from your bill
        - **PayPoint** — in person at local PayPoint outlets (£300 limit per visit)
        - **Post Office** — cash or cheque payable to Bradford Council"

        **Bubble 3 — Discounts & reductions:**
        "You may be able to lower your bill:
        - **25% off** if you live alone ([Single Person Discount](https://www.bradford.gov.uk/council-tax/reduce-your-bill/single-person-discount-for-the-bradford-district/))
        - **Exempt** if all adults are full-time students
        - [Council Tax Reduction](https://www.bradford.gov.uk/benefits/applying-for-benefits/housing-benefit-and-council-tax-reduction/) if you're on a low income
        - Discounts for carers, disabled adaptations, severe mental impairment, and more
        See all options at [Reduce your bill](https://www.bradford.gov.uk/council-tax/reduce-your-bill/reduce-your-bill/)"

        ## Bin collection — two different flows

        ### Flow A — Bin INFO questions (no address needed)
        Use this when the user asks about policies, procedures, or general recycling info:
        - "my bin wasn't collected" / "missed collection" / "bin not collected" → call **get_bin_info(query="missed collection")**
        - "what goes in grey/green/brown bin" → call **get_bin_info(query="what goes in bins")**
        - "garden waste / brown bin subscription" → call **get_bin_info(query="garden waste subscription")**
        - "bulky waste / large items" → call **get_bin_info(query="bulky waste")**
        - "bad weather / snow" → call **get_bin_info(query="bad weather bin collection")**
        - "assisted collection" → call **get_bin_info(query="assisted collection")**
        - "new bin / replacement bin" → call **get_bin_info(query="new replacement bin")**
        - "food waste" → call **get_bin_info(query="food waste collection")**
        - "recycling centre / tip" → call **get_bin_info(query="recycling centre")**
        - "electrical items / fridge / TV" → call **get_bin_info(query="electrical items")**
        - "hazardous waste / chemicals" → call **get_bin_info(query="hazardous waste")**
        - "needles / sharps / syringes" → call **get_bin_info(query="sharps needles")**
        DO NOT ask for a postcode for these queries.

        ### Flow B — Bin DATE queries (needs address)
        Use this ONLY when user explicitly wants to know WHEN their bin will be collected:
        - "when is my bin collected" / "bin collection dates" / "what day is my bin"
        1. Scan the CURRENT message for a Bradford postcode (BD…). If present → go to step 2.
           Only if NO postcode anywhere → ask: "What's your Bradford postcode?"
        2. If user gives ONLY a postcode → call lookup_addresses_for_postcode to show address picker
        3. If user gives a FULL address including house number AND postcode (e.g. "15 Oak Road BD5 8LT")
           → call get_bin_dates_for_address(postcode="{postcode}", address="{house_number_and_street}") directly — skip step 2
        4. User selects address from picker → call get_bin_dates_for_address immediately
        5. The UI shows a visual bin card automatically — do NOT repeat schedule details

        ## CRITICAL — Address handling
        When [[ADDRESS_LIST]] appears in a tool result:
        - The UI shows an interactive address picker automatically — NEVER list the addresses yourself
        - Your entire reply for this step must be ONE short sentence, e.g.:
          "I found 23 addresses for BD5 8LT — please tap yours below."
        - If the list has 0 addresses, say: "I couldn't find individual addresses for that postcode — please type your full address and I'll look up your bin dates."
        - Do NOT number or bullet the addresses. Do NOT mention individual street names.

        ## CRITICAL — Bin dates handling
        When [[BIN_DATE_CARD]] appears in a tool result:
        - The UI shows a visual bin collection card automatically
        - Use a markdown table for the schedule summary (see format below)
        - Keep the reply to 2-3 short sections max

        ## Bin schedule table format (use this exact format)
        | Bin | Collected | Contents |
        |-----|-----------|----------|
        | 🗑️ Grey | Fortnightly | General household waste |
        | ♻️ Green | Fortnightly (alternate weeks) | Paper, glass, plastic, tins |
        | 🌿 Brown | Weekly Apr–Nov · Fortnightly Dec–Mar | Garden waste (subscription) |

        ## Schools — strict flow
        TRIGGER: any message containing "school", "primary", "secondary", "academy", "education",
                 "ofsted", "admissions", "nursery", "year 7", "year 6", "sixth form", "enrol",
                 "enroll", "children's school", "kids school", or any Bradford postcode alongside school-related terms.

        0. If user directly names a specific school (e.g. "Thornton Primary", "Belle Vue Girls' Academy")
           → call **get_school_details(school_name="{name}")** immediately. Do NOT ask for a postcode.
        1. If user asks about education policy, admissions process, SEND, free school meals, term dates
           (no specific school or postcode mentioned) → call **get_education_info(topic="{topic}")**.
        2. If user asks for schools near them or mentions a postcode → call **find_schools_near_postcode(postcode="{postcode}")**.
           If no postcode → ask: "What's your Bradford postcode? I'll find the nearest schools."
        3. Present the school list. End with: "Tell me a school name for full details, or ask 'which is best' for a comparison."
        4. When user picks a school → call **get_school_details(school_name="{name}")** immediately.

        ## CRITICAL — School card handling
        When [[SCHOOL_LIST]] appears: the UI shows a school picker card automatically.
        - Write ONE short sentence: "Here are the X schools nearest to {postcode}."
        - Do NOT list school names yourself.

        When [[SCHOOL_CARD]] appears: the UI shows a school details card automatically.
        - Summarise in 2-3 sentences: Ofsted rating, phase/type, age range.
        - Include admissions advice: "Apply through Bradford Council for community schools; contact the school directly for academies."
        - Add comparison advice when asked: use Ofsted rating as the primary metric; Outstanding > Good > Requires Improvement.

        ## School comparison ("which is best?")
        When the user asks which school is best or to compare schools:
        - Use Ofsted rating as the main criterion (Outstanding best, then Good)
        - Mention phase fit (primary vs secondary), distance, and type (community vs academy)
        - Recommend the closest Outstanding or Good school
        - Always note: "The best school for your child depends on their needs — visit before deciding."

        ## Benefits — routing rules
        TRIGGER: any message containing "benefit", "housing benefit", "council tax reduction",
                 "council tax support", "universal credit", "free school meals", "FSM", "crisis fund",
                 "food bank", "emergency help", "hardship", "discretionary housing", "rent shortfall",
                 "assisted purchase", "overpayment", "appeal benefit", "change of circumstances",
                 "cost of living", "welfare", "benefit advice", "landlord benefit", "myinfo".

        IMPORTANT — do NOT use get_benefits_info for council tax BAND lookups (use lookup_council_tax_band).
        IMPORTANT — do NOT use get_benefits_info for free school meals term dates (use get_education_info).

        Routing table — call get_benefits_info with the appropriate query:
        - "housing benefit" / "council tax reduction" / "council tax support" → query = "housing benefit council tax reduction"
        - "universal credit" / "UC" / "managed migration" → query = "universal credit"
        - "free school meals" / "FSM" → query = "free school meals"
        - "crisis fund" / "emergency payment" / "hardship" → query = "crisis resilience fund"
        - "food bank" / "emergency food" → query = "emergency food providers"
        - "housing payment" / "rent shortfall" / "DHP" / "bedroom tax" → query = "housing payment discretionary"
        - "assisted purchase" / "household items" / "furniture" → query = "assisted purchase scheme"
        - "overpayment" / "overpaid" → query = "overpayment benefit"
        - "payment date" / "when paid" / "missing payment" → query = "benefit payment dates"
        - "appeal" / "dispute" / "challenge decision" → query = "benefit appeal"
        - "change of circumstances" / "report a change" / "circumstances changed" → query = "change of circumstances"
        - "backdate" / "late claim" → query = "backdating benefit"
        - "cost of living" / "struggling" / "financial help" → query = "cost of living help"
        - "proof" / "documents needed" → query = "proof documents benefit"
        - "welfare advice" / "who can help" → query = "welfare advice help"
        - "landlord" + "benefit" → query = "landlord housing benefit"
        - "myinfo" / "benefit review" / "online account" → query = "myinfo benefit review"
        - "notification" / "decision letter" → query = "benefit notification letter"

        After answering a benefits query:
        - Always include the official Bradford Council link from OFFICIAL_BRADFORD_LINK in the tool result
        - Always end with the follow-up question from FOLLOW_UP_SUGGESTION

        ## How to write replies
        - Keep replies short — 2-3 sentences per section max
        - Separate each distinct topic with a blank line (so they split into separate bubbles)
        - Use markdown tables for schedules, comparisons, or multi-column data
        - Use bullet points for lists of 3+ items
        - Bold key facts: dates, amounts, phone numbers
        - Never write walls of text

        ## Official links — MANDATORY on every answer
        Every answer about a council service MUST end with a relevant Bradford Council link.

        **Rule 1 — Tool results:** When a tool result contains `OFFICIAL_BRADFORD_LINK: [Title](url)`,
        always include it formatted as: "For full details: [Title](url)"

        **Rule 2 — All other answers:** Even when answering from your own knowledge (no tool called),
        always end with the most relevant Bradford Council page link. Use these:
        - Council tax (general) → https://www.bradford.gov.uk/council-tax/
        - Pay council tax → https://www.bradford.gov.uk/council-tax/pay-your-council-tax/pay-your-council-tax/
        - Council tax bands → https://www.bradford.gov.uk/council-tax/council-tax-bills/council-tax-bands-and-amounts/
        - Discounts / reduce bill → https://www.bradford.gov.uk/council-tax/reduce-your-bill/reduce-your-bill/
        - Bins / recycling → https://www.bradford.gov.uk/recycling-and-waste/bin-collections/bin-collections-in-the-bradford-district/
        - Benefits (general) → https://www.bradford.gov.uk/benefits/
        - Housing Benefit / CTR → https://www.bradford.gov.uk/benefits/applying-for-benefits/housing-benefit-and-council-tax-reduction/
        - Schools → https://www.bradford.gov.uk/education-and-skills/
        - School admissions → https://www.bradford.gov.uk/education-and-skills/school-admissions/apply-for-a-place-at-one-of-bradford-districts-schools/
        - Libraries → https://www.bradford.gov.uk/libraries/
        - Contact Bradford Council → https://www.bradford.gov.uk/contact-us/

        **Rule 3 — Follow-up questions:** When a tool result contains `FOLLOW_UP_SUGGESTION: {text}`,
        always end your reply with that question on its own line after the official link.

        ## Rules
        - Emergencies / homelessness: **01274 431000** (24/7)
        - Never invent phone numbers, prices, dates or addresses
        - Bank transfer for council tax: Sort code **56-00-36** · Account **00143790** · Name: City of Bradford Metropolitan Council
        - If unsure, direct to bradford.gov.uk or call 01274 431000
        """;

    public AgentService(
        ILlmService llm,
        IRagService rag,
        IConversationService conversation,
        IToolService tools,
        ILogger<AgentService> logger)
    {
        _llm          = llm;
        _rag          = rag;
        _conversation = conversation;
        _tools        = tools;
        _logger       = logger;
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        var (systemPrompt, history) = await PrepareContextAsync(request, ct);

        // Add the user's message — strip frontend-injected context prefixes before saving to DB
        history.Add(new LlmMessage { Role = "user", Content = request.Message });
        await _conversation.SaveTurnAsync(request.SessionId, "user", StripContextPrefixes(request.Message), ct);

        // Run agentic loop
        var toolDefs    = _tools.GetToolDefinitions();
        var usedSources = new List<string>();
        bool toolsUsed  = false;
        int rounds      = 0;

        while (rounds < MaxToolRounds)
        {
            rounds++;
            var response = await _llm.GenerateWithToolsAsync(systemPrompt, history, toolDefs, ct);

            if (!response.HasToolCalls)
            {
                var reply = response.Content ?? string.Empty;
                await _conversation.SaveTurnAsync(request.SessionId, "assistant", reply, ct);

                var (addresses, binDates, libraries, councilTax, ctProperties, schools, schoolDetails) = ExtractStructuredData(history);

                return new ChatResponse
                {
                    SessionId            = request.SessionId,
                    Reply                = reply,
                    Sources              = usedSources.Distinct().ToList(),
                    UsedRag              = toolsUsed || usedSources.Count > 0,
                    Addresses            = addresses,
                    BinDates             = binDates,
                    Libraries            = libraries,
                    CouncilTaxInfo       = councilTax,
                    CouncilTaxProperties = ctProperties,
                    Schools              = schools,
                    SchoolDetails        = schoolDetails
                };
            }

            toolsUsed = true;
            var assistantMsg = new AssistantToolCallMessage { ToolCalls = response.ToolCalls! };
            history.Add(assistantMsg);

            foreach (var toolCall in response.ToolCalls!)
            {
                _logger.LogInformation("Tool call: {Name}({Args})", toolCall.Name, toolCall.Arguments);
                var result = await _tools.ExecuteAsync(toolCall.Name, toolCall.Arguments, ct);
                ExtractUrls(result, usedSources);
                history.Add(new ToolResultMessage { ToolCallId = toolCall.Id, Content = result });
            }
        }

        // Fallback if loop exhausted
        var fallback = "I searched Bradford Council's website but couldn't find a definitive answer. Please call **01274 431000** or visit **bradford.gov.uk** directly.";
        await _conversation.SaveTurnAsync(request.SessionId, "assistant", fallback, ct);
        return new ChatResponse { SessionId = request.SessionId, Reply = fallback, Sources = usedSources };
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // For streaming: run tools first (non-streaming), then stream the final answer
        var (systemPrompt, history) = await PrepareContextAsync(request, ct);
        history.Add(new LlmMessage { Role = "user", Content = request.Message });
        await _conversation.SaveTurnAsync(request.SessionId, "user", StripContextPrefixes(request.Message), ct);

        // Run tool loop silently
        var toolDefs = _tools.GetToolDefinitions();
        int rounds   = 0;

        while (rounds < MaxToolRounds)
        {
            rounds++;
            var response = await _llm.GenerateWithToolsAsync(systemPrompt, history, toolDefs, ct);
            if (!response.HasToolCalls) break;

            var assistantMsg = new AssistantToolCallMessage { ToolCalls = response.ToolCalls! };
            history.Add(assistantMsg);

            foreach (var tc in response.ToolCalls!)
            {
                var result = await _tools.ExecuteAsync(tc.Name, tc.Arguments, ct);
                history.Add(new ToolResultMessage { ToolCallId = tc.Id, Content = result });
            }
        }

        // Stream the final answer
        var sb = new StringBuilder();
        await foreach (var token in _llm.GenerateStreamAsync(systemPrompt, history, ct))
        {
            sb.Append(token);
            yield return token;
        }

        await _conversation.SaveTurnAsync(request.SessionId, "assistant", sb.ToString(), ct);

        // Emit structured data as a final special event so the frontend can render cards
        var (addresses, binDates, libraries, councilTax, ctProperties, schools, schoolDetails) = ExtractStructuredData(history);
        var structured = new ChatResponse
        {
            Addresses            = addresses,
            BinDates             = binDates,
            Libraries            = libraries,
            CouncilTaxInfo       = councilTax,
            CouncilTaxProperties = ctProperties,
            Schools              = schools,
            SchoolDetails        = schoolDetails
        };
        yield return "[STRUCTURED]" + System.Text.Json.JsonSerializer.Serialize(structured, _camelCase);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private async Task<(string systemPrompt, List<LlmMessage> history)> PrepareContextAsync(
        ChatRequest request, CancellationToken ct)
    {
        var historyTask = _conversation.GetHistoryAsync(request.SessionId, maxTurns: 20, ct);

        var history = await historyTask;

        // Skip RAG for tool-based queries (postcodes, bin, library, council tax)
        // — saves 200-1500ms per request with no accuracy loss for these query types
        string contextBlock;
        if (IsToolQuery(request.Message))
        {
            contextBlock = "No prior indexed knowledge — rely on your tools.";
        }
        else
        {
            try
            {
                var ragContext = await _rag.RetrieveAsync(request.Message, topK: 3, ct);
                contextBlock = ragContext.HasResults
                    ? ragContext.BuildContextBlock()
                    : "No prior indexed knowledge — rely on your tools to search live.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning("RAG retrieval failed (non-fatal): {Msg}", ex.Message);
                contextBlock = "No prior indexed knowledge — rely on your tools to search live.";
            }
        }

        var prompt = SystemPrompt.Replace("{RAG_CONTEXT}", contextBlock);
        return (prompt, history);
    }

    // Extract structured data markers from tool results.
    // For addresses: keep the largest list (Bradford form > Overpass fallback).
    internal static (List<AddressOption>? addresses, BinDateCard? binDates, List<LibraryOption>? libraries, CouncilTaxCard? councilTax, List<CouncilTaxPropertyOption>? ctProperties, List<SchoolOption>? schools, SchoolCard? schoolDetails)
        ExtractStructuredData(List<LlmMessage> history)
    {
        List<AddressOption>?            addresses    = null;
        BinDateCard?                    binDates     = null;
        List<LibraryOption>?            libraries    = null;
        CouncilTaxCard?                 councilTax   = null;
        List<CouncilTaxPropertyOption>? ctProperties = null;
        List<SchoolOption>?             schools      = null;
        SchoolCard?                     schoolDetails = null;

        foreach (var msg in history.OfType<ToolResultMessage>())
        {
            var content = msg.Content ?? "";

            var addrJson = ExtractBetweenMarkers(content, "[[ADDRESS_LIST]]", "[[/ADDRESS_LIST]]");
            if (addrJson != null)
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<List<AddressOption>>(addrJson);
                    if (parsed != null && parsed.Count > (addresses?.Count ?? 0))
                        addresses = parsed;
                }
                catch { }
            }

            var binJson = ExtractBetweenMarkers(content, "[[BIN_DATE_CARD]]", "[[/BIN_DATE_CARD]]");
            if (binJson != null)
            {
                try { binDates = System.Text.Json.JsonSerializer.Deserialize<BinDateCard>(binJson); } catch { }
            }

            var libJson = ExtractBetweenMarkers(content, "[[LIBRARY_LIST]]", "[[/LIBRARY_LIST]]");
            if (libJson != null)
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<List<LibraryOption>>(libJson);
                    if (parsed != null && parsed.Count > (libraries?.Count ?? 0))
                        libraries = parsed;
                }
                catch { }
            }

            var ctJson = ExtractBetweenMarkers(content, "[[COUNCIL_TAX_CARD]]", "[[/COUNCIL_TAX_CARD]]");
            if (ctJson != null)
            {
                try { councilTax = System.Text.Json.JsonSerializer.Deserialize<CouncilTaxCard>(ctJson); } catch { }
            }

            var ctPropJson = ExtractBetweenMarkers(content, "[[COUNCIL_TAX_PROPERTY_LIST]]", "[[/COUNCIL_TAX_PROPERTY_LIST]]");
            if (ctPropJson != null)
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<List<CouncilTaxPropertyOption>>(ctPropJson);
                    if (parsed != null && parsed.Count > 0) ctProperties = parsed;
                }
                catch { }
            }

            var schoolListJson = ExtractBetweenMarkers(content, "[[SCHOOL_LIST]]", "[[/SCHOOL_LIST]]");
            if (schoolListJson != null)
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<List<SchoolOption>>(schoolListJson);
                    if (parsed != null && parsed.Count > (schools?.Count ?? 0)) schools = parsed;
                }
                catch { }
            }

            var schoolCardJson = ExtractBetweenMarkers(content, "[[SCHOOL_CARD]]", "[[/SCHOOL_CARD]]");
            if (schoolCardJson != null)
            {
                try { schoolDetails = System.Text.Json.JsonSerializer.Deserialize<SchoolCard>(schoolCardJson); } catch { }
            }
        }

        return (addresses, binDates, libraries, councilTax, ctProperties, schools, schoolDetails);
    }

    private static string? ExtractBetweenMarkers(string text, string open, string close)
    {
        var start = text.IndexOf(open, StringComparison.Ordinal);
        if (start < 0) return null;
        start += open.Length;
        var end = text.IndexOf(close, start, StringComparison.Ordinal);
        if (end < 0) return null;
        return text[start..end].Trim();
    }

    // Strips [User: name=...] and [brief/detailed reply] prefixes injected by buildApiText()
    // so they don't get stored in the conversation DB and inflate subsequent turn token costs.
    private static string StripContextPrefixes(string message) =>
        Regex.Replace(message,
            @"^(\s*\[User:[^\]]*\]\s*|\s*\[(brief|detailed)[^\]]*\]\s*)+",
            "", RegexOptions.IgnoreCase).Trim();

    // Detect queries that will definitely hit a tool — skip expensive RAG for these
    internal static bool IsToolQuery(string message)
    {
        var m = message.ToLower();
        // UK postcode pattern
        if (System.Text.RegularExpressions.Regex.IsMatch(m, @"\b[Bb][Dd]\d{1,2}\s?\d[A-Za-z]{2}\b")) return true;
        // Common tool trigger keywords
        var toolWords = new[] { "bin", "recycling", "collection", "library", "libraries",
                                "council tax", "tax band", "band ", "planning", "blue badge",
                                "postcode", "address", "i live at", "nearest",
                                "school", "primary", "secondary", "academy", "ofsted",
                                "admissions", "nursery", "sixth form", "free school meal",
                                "send ", "special education", "term date" };
        return toolWords.Any(w => m.Contains(w));
    }

    private static void ExtractUrls(string text, List<string> urls)
    {
        foreach (var w in text.Split(' ', '\n', '\r'))
        {
            var clean = w.Trim('.', ',', ')', '(', '"', '\'');
            if (clean.StartsWith("https://www.bradford.gov.uk", StringComparison.OrdinalIgnoreCase) && clean.Length > 30)
                urls.Add(clean);
        }
    }
}
