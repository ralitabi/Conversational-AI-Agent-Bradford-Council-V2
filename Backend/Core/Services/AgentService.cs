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
        - **get_council_tax_info** — council tax bands, rates, discounts, payments (general info)
        - **lookup_council_tax_band** — look up the actual band and amount for a specific postcode (use this when user asks "how much is my council tax" or "what band am I")
        - **check_planning_application** — planning portal search
        - **find_local_services** — libraries with distance from postcode, parks, leisure centres
        - **get_library_details** — opening hours, facilities, address and how to join for a specific library (call this after user picks a library)
        - **find_schools_near_postcode** — find schools near a Bradford postcode with Ofsted ratings and distance
        - **get_school_details** — full info for a specific school: Ofsted, type, phase, age range, admissions link
        - **get_education_info** — Bradford education policies: admissions, SEND, free school meals, term dates

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

        ## Council tax band / amount — STRICT flow — follow exactly
        TRIGGER: any message containing "council tax", "tax band", "tax amount", "how much tax",
                 "what band", "my band", "rates", "council tax bill", or any Bradford postcode (BD…).
        1. Scan the CURRENT user message first. If it contains a Bradford postcode (pattern: BD followed
           by digits and letters, e.g. BD7 3BX, BD1 1AA) → treat it as provided and go to step 2 NOW.
           Only if NO postcode appears anywhere in the current message OR prior conversation → reply:
           "What's your Bradford postcode? I'll look up your exact band and amount."
           Do NOT call any tool in that case. Just ask.
        2. Postcode identified → call **lookup_council_tax_band(postcode="{postcode}")** immediately.
           Do NOT call get_council_tax_info first. Do NOT explain anything first. Just call the tool.
           NEVER call lookup_addresses_for_postcode for a council tax query — it is only for bin dates.
           NEVER call more than one tool per round for council tax queries.
        3. The UI shows a visual council tax card automatically.
        4. Write ONE short sentence: "Most properties in {postcode} are Band {X} — annual charge £{amount}. To confirm your specific property, tell me your house number."
        5. User says "my band isn't X" OR provides a house number → call **lookup_council_tax_band(postcode="{postcode}", address="{house_number}")** immediately with both values.
           Do NOT just repeat the same postcode lookup — include the address/house number this time.

        ## CRITICAL — Council tax card handling
        When [[COUNCIL_TAX_CARD]] appears in a tool result:
        - The UI shows an interactive council tax card automatically
        - Write ONE short sentence only with the band and amount
        - Do NOT reproduce the full rates table yourself
        - Do NOT call get_council_tax_info — it is only for discounts/exemptions/payment queries

        ## Council tax address selected — reply in exactly 3 separate bubbles
        TRIGGER: user message "I live at [address]. My council tax is Band [X], annual charge [annual] ([monthly]/month)..."
        - The band and amounts are stated IN THE MESSAGE — use them exactly. Do NOT invent or change any figures.
        - Do NOT call any tool.
        - Separate each reply with a blank line so they appear as separate bubbles.

        **Bubble 1 — Band & amount (use the exact figures from the message):**
        "Your property at **[address]** is **Band [X]** — **[annual]/year** (about **[monthly]/month** over 10 months)."

        **Bubble 2 — How to pay:**
        "You can pay online at [bradford.gov.uk](https://www.bradford.gov.uk/council-tax/pay-your-council-tax/), set up a **direct debit** (spread over 10 or 12 months), pay by phone on **01274 431000**, or at any Post Office. Payments are normally due on the **1st of each month**."

        **Bubble 3 — Discounts & reductions:**
        "You may be able to lower your bill:
        - **25% off** if you live alone (Single Person Discount)
        - **Exempt** if all adults are full-time students
        - **Council Tax Support** if you're on a low income
        - **Disability Reduction Scheme** if a room is needed for a disabled person
        Apply at [bradford.gov.uk/council-tax](https://www.bradford.gov.uk/council-tax/)"

        ## Bin collection — strict flow
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

        ## How to write replies
        - Keep replies short — 2-3 sentences per section max
        - Separate each distinct topic with a blank line (so they split into separate bubbles)
        - Use markdown tables for schedules, comparisons, or multi-column data
        - Use bullet points for lists of 3+ items
        - Bold key facts: dates, amounts, phone numbers
        - Never write walls of text

        ## Rules
        - Emergencies / homelessness: **01274 431000** (24/7)
        - Never invent phone numbers, prices, dates or addresses
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
