using System.Net;
using System.Text;
using System.Text.Json;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    // ── Bradford 2026/27 council tax rates — Band A = £1,555.37 (excl. parish precepts) ──
    // Source: bradford.gov.uk/council-tax/council-tax-bills/council-tax-bands-and-amounts/
    // Scraped May 2026. Update each April when new financial year begins.
    // Ratios: A=6, B=7, C=8, D=9, E=11, F=13, G=15, H=18 ninths of Band D (=£2,333.06).
    private static readonly Dictionary<string, (decimal Annual, decimal Monthly)> BradfordRates2627 = new()
    {
        ["A"] = (1555.37m, 129.61m),
        ["B"] = (1814.60m, 151.22m),
        ["C"] = (2073.83m, 172.82m),
        ["D"] = (2333.06m, 194.42m),
        ["E"] = (2851.51m, 237.63m),
        ["F"] = (3369.97m, 280.83m),
        ["G"] = (3888.43m, 324.04m),
        ["H"] = (4666.10m, 388.84m),
    };

    // Standard band ratios (ninths): A=6, B=7, C=8, D=9, E=11, F=13, G=15, H=18
    private static readonly Dictionary<string, int> BandNinths = new()
    {
        ["A"]=6, ["B"]=7, ["C"]=8, ["D"]=9, ["E"]=11, ["F"]=13, ["G"]=15, ["H"]=18
    };

    // ── Council tax knowledge base ────────────────────────────────────────────
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] CouncilTaxKnowledgeMap =
    {
        // Pay council tax
        (new[]{"pay","payment","direct debit","paypoint","bank transfer","how to pay","online payment","phone payment","bacs","sort code"},
         new[]{"https://www.bradford.gov.uk/council-tax/pay-your-council-tax/pay-your-council-tax/",
               "https://www.bradford.gov.uk/paying-for-services/direct-debit-and-paperless-bills/direct-debit/"},
         "Pay your Council Tax",
         "Would you like to know if you qualify for any discounts or reductions on your council tax bill?"),

        // Bands and amounts
        (new[]{"band","bands","amount","amounts","rate","rates","how much is","what band","council tax rate","1991 valuation","parish precept"},
         new[]{"https://www.bradford.gov.uk/council-tax/council-tax-bills/council-tax-bands-and-amounts/"},
         "Council Tax bands and amounts 2026/27",
         "Would you like me to look up your specific band? Share your Bradford postcode and I can find it for you."),

        // Single person discount
        (new[]{"single person","live alone","living alone","on my own","one person","sole occupant","25%","single occupancy","one adult"},
         new[]{"https://www.bradford.gov.uk/council-tax/reduce-your-bill/single-person-discount-for-the-bradford-district/"},
         "Single Person Discount (25% off)",
         "Would you like to know about other discounts you might be entitled to?"),

        // Student discount
        (new[]{"student","university","college","full-time education","student discount","full time student","postgraduate","student exemption"},
         new[]{"https://www.bradford.gov.uk/council-tax/reduce-your-bill/council-tax-student-discount/"},
         "Council Tax Student Discount",
         "If every adult in the property is a full-time student, you pay no council tax at all. Would you like to apply?"),

        // Personal circumstance discounts (disability, carer, SMI, annexe, care leaver, etc.)
        (new[]{"disability","disabled","carer","caring","severe mental impairment","SMI","annexe","annex","care leaver","apprentice","diplomat","detained","prison","nurse","hostel"},
         new[]{"https://www.bradford.gov.uk/council-tax/reduce-your-bill/other-council-tax-discounts/"},
         "Council Tax discounts for personal circumstances",
         "Would you like to know about property-based discounts or how to challenge your council tax band?"),

        // Property-based discounts / exemptions / empty
        (new[]{"empty property","unoccupied","second home","holiday home","unfurnished","repossessed","probate","granny flat","annex exempt","property discount","property exempt","disrepair","renovation","uninhabited"},
         new[]{"https://www.bradford.gov.uk/council-tax/reduce-your-bill/apply-for-a-discount-based-on-the-status-of-your-property/",
               "https://www.bradford.gov.uk/council-tax/council-tax-bills/empty-properties-and-second-or-holiday-homes/"},
         "Council Tax discounts based on property status",
         "Would you like to know about personal circumstance discounts, or how to challenge your council tax band?"),

        // Reduce your bill (general)
        (new[]{"reduce","reduction","lower my bill","cut my bill","help with council tax","can I reduce","how to reduce","save on council tax"},
         new[]{"https://www.bradford.gov.uk/council-tax/reduce-your-bill/reduce-your-bill/"},
         "Ways to reduce your Council Tax bill",
         "Which type of reduction interests you — personal circumstances, property status, or the means-tested Council Tax Reduction scheme?"),

        // Problems paying / arrears
        (new[]{"can't pay","cannot pay","behind","arrears","struggling to pay","missed payment","payment difficulty","can't afford","in debt","hardship","money trouble"},
         new[]{"https://www.bradford.gov.uk/council-tax/problems-paying-your-bill/behind-on-your-council-tax-payments/",
               "https://www.bradford.gov.uk/council-tax/problems-paying-your-bill/problems-paying-your-bill/"},
         "Help if you're struggling to pay Council Tax",
         "Would you like to know about the Council Tax Reduction scheme or access free debt advice?"),

        // Debt advice
        (new[]{"debt advice","money advice","owe money","money problems","breathing space","citizens advice","debt help","financial support","StepChange"},
         new[]{"https://www.bradford.gov.uk/council-tax/problems-paying-your-bill/debt-advice-for-council-tax/"},
         "Council Tax debt advice",
         "Would you like to know about the Council Tax Collection Commitment and payment arrangement options?"),

        // Enforcement / what happens if you don't pay
        (new[]{"enforcement","bailiff","court","summons","liability order","attachment of earnings","charging order","bankruptcy","don't pay","not paying","legal action","fine"},
         new[]{"https://www.bradford.gov.uk/council-tax/problems-paying-your-bill/what-can-happen-if-you-don-t-pay-your-council-tax/"},
         "What can happen if you don't pay Council Tax",
         "It's never too late to contact the council before enforcement starts. Would you like debt advice or payment arrangement information?"),

        // Collection commitment / payment plan
        (new[]{"collection commitment","payment arrangement","instalments","spreading payments","repayment plan","payment schedule"},
         new[]{"https://www.bradford.gov.uk/council-tax/problems-paying-your-bill/council-tax-collection-commitment/"},
         "Council Tax Collection Commitment",
         "Would you like to set up a Direct Debit or speak to the team about a payment arrangement?"),

        // Change of address / circumstances
        (new[]{"change of address","moved house","new address","moving home","change of circumstances","name change","report a change","council tax refund","tenant moved"},
         new[]{"https://www.bradford.gov.uk/council-tax/report-a-change-of-address-or-circumstances/report-a-change-or-ask-a-question-about-your-council-tax/"},
         "Report a change to your Council Tax",
         "Remember to also notify the benefits team separately if you receive Council Tax Reduction."),

        // Reporting a death
        (new[]{"death","died","deceased","bereavement","passed away","estate","probate","executor","someone died"},
         new[]{"https://www.bradford.gov.uk/council-tax/report-a-change-of-address-or-circumstances/council-tax-tell-us-a-resident-or-owner-has-died/"},
         "Reporting a death for Council Tax",
         "Would you like to know about property exemptions or discounts available after a bereavement?"),

        // Appeals
        (new[]{"appeal","dispute","challenge","wrong band","disagree with","valuation tribunal","valuation office","challenge my band","band wrong"},
         new[]{"https://www.bradford.gov.uk/council-tax/general-council-tax-information/making-a-council-tax-appeal/"},
         "Making a Council Tax appeal",
         "Remember: you must keep paying your bill while appealing. Any overpayment will be refunded automatically."),

        // FAQs
        (new[]{"faq","frequently asked","question about","general query","how does council tax work","confused about"},
         new[]{"https://www.bradford.gov.uk/council-tax/general-council-tax-information/council-tax-faqs/"},
         "Council Tax frequently asked questions",
         "Is there a specific aspect of your council tax you'd like more detail on?"),

        // What is council tax
        (new[]{"what is council tax","what does council tax fund","what does it pay for","what is it used for","what does it cover"},
         new[]{"https://www.bradford.gov.uk/council-tax/general-council-tax-information/what-is-council-tax/"},
         "What is Council Tax?",
         "Would you like to know about your band, how to pay, or whether you qualify for any reductions?"),

        // Current bill / 2026-27
        (new[]{"my bill","2026","2027","annual bill","this year","paperless bill","large print","alternative format","braille","extra care"},
         new[]{"https://www.bradford.gov.uk/council-tax/council-tax-bills/council-tax-bills/",
               "https://www.bradford.gov.uk/council-tax/pay-your-council-tax/register-for-the-extra-care-scheme/"},
         "Your Council Tax bill 2026/27",
         "Would you like to set up Direct Debit or sign up for paperless billing?"),

        // Reduction letter explained
        (new[]{"reduction letter","council tax reduction letter","what does my letter mean","CTS letter","CTR letter","reduction notice"},
         new[]{"https://www.bradford.gov.uk/council-tax/general-council-tax-information/council-tax-reduction-letter-explained/"},
         "Understanding your Council Tax Reduction letter",
         "If you disagree with the reduction amount, you have the right to appeal. Would you like to know how?"),

        // Landlord
        (new[]{"landlord","letting","agent","HMO","house in multiple occupation","tenant liability","tenancy change","rental property"},
         new[]{"https://www.bradford.gov.uk/council-tax/information-for-landlords/information-for-landlords/"},
         "Council Tax information for landlords",
         "Would you like to know about empty property charges or how liability transfers between tenancies?"),

        // Contact
        (new[]{"contact","phone number","email council tax","get in touch","speak to someone","helpline","01274"},
         new[]{"https://www.bradford.gov.uk/council-tax/contact-the-council-tax-team/contact-the-council-tax-team/"},
         "Contact the Council Tax team",
         "You can call the Council Tax team directly on 01274 437792, Monday to Friday."),

        // General fallback
        (new[]{"council tax"},
         new[]{"https://www.bradford.gov.uk/council-tax/general-council-tax-information/general-council-tax-information/",
               "https://www.bradford.gov.uk/council-tax/general-council-tax-information/council-tax-faqs/"},
         "Bradford Council Tax information",
         "What would you like to know about your council tax? I can help with payments, bands, discounts, arrears, appeals, and more."),
    };

    private async Task<string> GetCouncilTaxAsync(string query, string address, CancellationToken ct)
    {
        var q    = query.ToLower();
        var urls = new List<string>();
        var title    = "";
        var followUp = "";

        foreach (var (keywords, pages, pageTitle, pageFollowUp) in CouncilTaxKnowledgeMap)
        {
            if (keywords.Any(k => q.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var u in pages)
                    if (!urls.Contains(u)) urls.Add(u);
                if (string.IsNullOrEmpty(title))
                {
                    title    = pageTitle;
                    followUp = pageFollowUp;
                }
                if (urls.Count >= 2) break;
            }
        }

        if (urls.Count == 0)
        {
            urls.Add("https://www.bradford.gov.uk/council-tax/general-council-tax-information/general-council-tax-information/");
            title    = "Bradford Council Tax information";
            followUp = "What would you like to know about your council tax? I can help with payments, bands, discounts, arrears, and more.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"BRADFORD COUNCIL TAX INFORMATION — query: \"{query}\"");
        sb.AppendLine();

        foreach (var url in urls.Take(2))
        {
            var html = await FetchHtmlAsync(url, ct);
            if (string.IsNullOrEmpty(html)) continue;

            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);
            foreach (var tag in new[] { "nav", "header", "footer", "script", "style", "aside", "noscript" })
            {
                var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
                if (nodes != null) foreach (var n in nodes.ToList()) n.Remove();
            }
            var main = doc.DocumentNode.SelectSingleNode("//main")
                    ?? doc.DocumentNode.SelectSingleNode("//article")
                    ?? doc.DocumentNode.SelectSingleNode("//body");
            if (main == null) continue;

            var text = CleanText(main.InnerText);
            sb.AppendLine($"--- SOURCE: {url} ---");
            sb.AppendLine(TruncateText(text, 3500));
            sb.AppendLine();
        }

        // Always include the VOA band checker link so users can look up their band directly
        sb.AppendLine($"VOA band checker (GOV.UK): https://www.tax.service.gov.uk/check-council-tax-band/search");

        sb.AppendLine($"OFFICIAL_BRADFORD_LINK: [{title}]({urls[0]})");
        if (!string.IsNullOrEmpty(followUp))
            sb.AppendLine($"FOLLOW_UP_SUGGESTION: {followUp}");

        return sb.ToString();
    }

    // ── Council tax band lookup — cached 5 min per postcode to avoid repeat GOV.UK scrapes ─
    private async Task<string> LookupCouncilTaxBandAsync(string postcode, string address, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(postcode))
            return "NEEDS_POSTCODE: Please ask the user for their full Bradford postcode (e.g. BD5 8LT).";

        // Normalize: uppercase, collapse all internal whitespace to single space
        postcode = System.Text.RegularExpressions.Regex.Replace(postcode.Trim().ToUpper(), @"\s+", " ");

        // Insert space before last 3 chars if missing (e.g. "BD58LT" → "BD5 8LT")
        if (!postcode.Contains(' ') && postcode.Length >= 5)
            postcode = postcode[..^3] + " " + postcode[^3..];

        // Reject obviously incomplete postcodes (outward-code only, e.g. "BD5" or "BD10")
        if (!postcode.Contains(' ') || postcode.Length < 6)
            return $"NEEDS_FULL_POSTCODE: '{postcode}' looks incomplete. Please ask the user for their full postcode — it should look like 'BD5 8LT' or 'BD10 9AB'.";

        // Validate format: must match standard UK postcode pattern
        if (!System.Text.RegularExpressions.Regex.IsMatch(postcode,
                @"^[A-Z]{1,2}\d{1,2}[A-Z]?\s\d[A-Z]{2}$"))
            return $"INVALID_POSTCODE: '{postcode}' does not look like a valid UK postcode. Please ask the user to double-check it — for example 'BD5 8LT'.";

        // Cache full results by postcode — avoids repeat GOV.UK scrapes within 5 minutes
        var cacheKey = $"ct:{postcode.Replace(" ", "")}:{address?.ToUpper() ?? ""}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null)
        {
            _logger.LogInformation("Council tax cache hit for {Postcode}", postcode);
            return cached;
        }

        // 1. Fetch from GOV.UK council tax band service
        var govResults = await ScrapeNecswsCouncilTaxAsync(postcode, ct);

        // Filter out any entries where band wasn't resolved
        govResults = govResults.Where(r => !string.IsNullOrEmpty(r.Band)).ToList();

        string? detectedBand    = null;
        string? detectedAddress = null;
        string? portalAmount    = null;
        bool    mixedBands      = false;

        // 2. Build property list with amounts from hardcoded rates table
        var bands   = BradfordRates2627;

        // Build the picker list — every property with its band and calculated amounts
        var propertyOptions = new List<Bradford.Core.Models.CouncilTaxPropertyOption>();
        int num = 1;
        foreach (var r in govResults)
        {
            var annual  = bands.TryGetValue(r.Band.ToUpper(), out var br) ? $"£{br.Annual:N2}" : "";
            var monthly = bands.TryGetValue(r.Band.ToUpper(), out var brm) ? $"£{brm.Monthly:N2}" : "";
            propertyOptions.Add(new Bradford.Core.Models.CouncilTaxPropertyOption
            {
                Number        = num++,
                Address       = r.Address,
                Band          = r.Band,
                AnnualAmount  = annual,
                MonthlyAmount = monthly,
                Postcode      = postcode
            });
        }

        // 3. Pick the representative result (address hint → best match; else most common band)
        if (govResults.Count > 0)
        {
            (string Address, string Band, string AnnualAmount) best;
            if (!string.IsNullOrWhiteSpace(address))
            {
                best = govResults.OrderByDescending(r => ScoreAddressMatch(r.Address, address)).First();
            }
            else
            {
                best = govResults
                    .GroupBy(r => r.Band)
                    .OrderByDescending(g => g.Count())
                    .First().First();

                mixedBands = govResults.Select(r => r.Band).Distinct().Count() > 1;
            }
            detectedBand    = best.Band;
            detectedAddress = best.Address;
            portalAmount    = best.AnnualAmount;
        }

        var allBandsList = bands.Select(kv => new Bradford.Core.Models.CouncilTaxBandRate
        {
            Band          = kv.Key,
            AnnualAmount  = $"£{kv.Value.Annual:N2}",
            MonthlyAmount = $"£{kv.Value.Monthly:N2}"
        }).ToList();

        bands.TryGetValue(detectedBand?.ToUpper() ?? "", out var selectedRate);

        var card = new Bradford.Core.Models.CouncilTaxCard
        {
            Address       = detectedAddress ?? (string.IsNullOrEmpty(address) ? postcode : $"{address}, {postcode}"),
            Band          = detectedBand ?? "",
            TaxYear       = "2026/27",
            AnnualAmount  = selectedRate.Annual > 0 ? $"£{selectedRate.Annual:N2}" : "",
            MonthlyAmount = selectedRate.Monthly > 0 ? $"£{selectedRate.Monthly:N2}" : "",
            AllBands      = allBandsList,
            PayUrl        = "https://www.bradford.gov.uk/council-tax/pay-your-council-tax/pay-your-council-tax/",
            BandLookupUrl = "https://www.tax.service.gov.uk/check-council-tax-band/search"
        };

        var sb = new StringBuilder();

        // Emit property picker list (shown as scrollable address picker in UI)
        if (propertyOptions.Count > 0)
        {
            sb.AppendLine("[[COUNCIL_TAX_PROPERTY_LIST]]");
            sb.AppendLine(System.Text.Json.JsonSerializer.Serialize(propertyOptions));
            sb.AppendLine("[[/COUNCIL_TAX_PROPERTY_LIST]]");
            sb.AppendLine();
        }

        // Only emit the summary card when a band was actually found
        if (detectedBand != null)
        {
            sb.AppendLine("[[COUNCIL_TAX_CARD]]");
            sb.AppendLine(System.Text.Json.JsonSerializer.Serialize(card));
            sb.AppendLine("[[/COUNCIL_TAX_CARD]]");
            sb.AppendLine();
        }

        if (propertyOptions.Count > 0)
            sb.AppendLine($"COUNCIL_TAX_INSTRUCTION: Found {propertyOptions.Count} properties at {postcode}. The UI shows a scrollable address picker — each property shows its band and annual amount. Write ONLY: \"I found {propertyOptions.Count} properties at {postcode} — tap yours to see your exact band and annual charge.\"");
        else if (detectedBand != null)
            sb.AppendLine($"COUNCIL_TAX_INSTRUCTION: Band {detectedBand} at {postcode}. Annual {card.AnnualAmount}. Write 1 sentence confirming this.");
        else
            sb.AppendLine($"COUNCIL_TAX_INSTRUCTION: No council tax band data found for {postcode}. This postcode may not exist or may not be in Bradford. Tell the user you couldn't find data for that postcode and suggest they check at bradford.gov.uk/council-tax or call 01274 431000.");

        var result = sb.ToString();
        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Size = 1
        });
        return result;
    }

    private static Dictionary<string, (decimal Annual, decimal Monthly)> RebuildRatesFromAnchor(
        string anchorBand, decimal anchorAnnual)
    {
        if (!BandNinths.TryGetValue(anchorBand.ToUpper(), out var anchorNinths) || anchorNinths == 0)
            return BradfordRates2627;

        // Band D value = anchor / (anchorNinths/9)
        var bandD = anchorAnnual * 9m / anchorNinths;
        return BandNinths.ToDictionary(
            kv => kv.Key,
            kv =>
            {
                var annual  = Math.Round(bandD * kv.Value / 9m, 2);
                var monthly = Math.Round(annual / 12m, 2);
                return (annual, monthly);
            });
    }

    // ── GOV.UK council tax band lookup ───────────────────────────────────────
    // Source: https://www.gov.uk/council-tax-bands → https://www.tax.service.gov.uk/check-council-tax-band/search
    private async Task<List<(string Address, string Band, string AnnualAmount)>> ScrapeNecswsCouncilTaxAsync(
        string postcode, CancellationToken ct)
    {
        // GOV.UK band lookup is a 3-step flow:
        //   1. POST postcode → redirect to property-list page (no bands shown yet)
        //   2. Parse property links from list page
        //   3. GET each property page → parse "Band X" from that page
        var results = new List<(string Address, string Band, string AnnualAmount)>();
        try
        {
            var cookies = new CookieContainer();
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect        = true,
                MaxAutomaticRedirections = 15,
                UseCookies               = true,
                CookieContainer          = cookies
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-GB,en;q=0.9");

            const string baseUrl    = "https://www.tax.service.gov.uk";
            const string searchPath = "/check-council-tax-band/search";

            // ── Step 1: GET search page to collect CSRF token ──
            var initResp = await client.GetAsync(baseUrl + searchPath, ct);
            if (!initResp.IsSuccessStatusCode) return results;

            var initHtml = await initResp.Content.ReadAsStringAsync(ct);
            var initUrl  = initResp.RequestMessage?.RequestUri?.ToString() ?? baseUrl + searchPath;

            var initDoc = new HtmlDocument();
            initDoc.LoadHtml(initHtml);

            // Collect all hidden fields (includes CSRF / session tokens)
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var inp in initDoc.DocumentNode
                .SelectNodes("//input[@type='hidden']") ?? Enumerable.Empty<HtmlNode>())
            {
                var n = inp.GetAttributeValue("name",  "");
                var v = inp.GetAttributeValue("value", "");
                if (!string.IsNullOrEmpty(n)) fields[n] = v;
            }

            // Find the postcode text field
            var pcNode = initDoc.DocumentNode
                .SelectNodes("//input[@type='text' or @type='search']")?
                .FirstOrDefault(n =>
                {
                    var id   = n.GetAttributeValue("id",   "").ToLower();
                    var nm   = n.GetAttributeValue("name", "").ToLower();
                    return id.Contains("post") || nm.Contains("post") ||
                           id.Contains("code") || nm.Contains("code");
                });
            var pcField = pcNode?.GetAttributeValue("name", "")
                       ?? pcNode?.GetAttributeValue("id",   "")
                       ?? "postcode";
            // GOV.UK accepts both "BD5 8LT" (with space) and "BD58LT" (without) — send with space
            fields[pcField] = postcode;

            var formAction = initDoc.DocumentNode
                .SelectSingleNode("//form[1]")?
                .GetAttributeValue("action", searchPath) ?? searchPath;
            if (!formAction.StartsWith("http"))
                formAction = baseUrl + formAction;

            _logger.LogInformation("GOV.UK CT step 1: POST {PC} → {A}", postcode, formAction);

            // ── Step 2: POST postcode → land on property-list page ──
            using var postMsg = new HttpRequestMessage(HttpMethod.Post, formAction)
            {
                Content = new FormUrlEncodedContent(
                    fields.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
            };
            postMsg.Headers.Referrer = new Uri(initUrl);

            var listResp = await client.SendAsync(postMsg, ct);
            var listHtml = await listResp.Content.ReadAsStringAsync(ct);
            var listUrl  = listResp.RequestMessage?.RequestUri?.ToString() ?? formAction;
            _logger.LogInformation("GOV.UK CT step 2: list page {Url} ({Status})",
                listUrl, listResp.StatusCode);

            // ── Step 3: Parse property links (with pagination) ──
            var propLinks = await CollectAllPropertyLinksAsync(client, listHtml, listUrl, baseUrl, ct);
            _logger.LogInformation("GOV.UK CT step 3: found {N} property links", propLinks.Count);

            if (propLinks.Count == 0) return results;

            // ── Step 4: Fetch all property pages in parallel batches ──
            const int batchSize = 5;
            for (int i = 0; i < propLinks.Count; i += batchSize)
            {
                var batch = propLinks.Skip(i).Take(batchSize);
                var bandTasks = batch.Select(async prop =>
                {
                    try
                    {
                        var propResp = await client.GetAsync(prop.Url, ct);
                        if (!propResp.IsSuccessStatusCode) return null;
                        var propHtml = await propResp.Content.ReadAsStringAsync(ct);
                        var band = ExtractBandFromGovUkPropertyPage(propHtml);
                        _logger.LogInformation("GOV.UK CT: {Addr} → Band {Band}", prop.Address, band ?? "?");
                        return band != null
                            ? ((string Address, string Band, string AnnualAmount)?)(prop.Address, band, "")
                            : null;
                    }
                    catch { return null; }
                });
                var batchResults = await Task.WhenAll(bandTasks);
                results.AddRange(batchResults.Where(r => r != null).Select(r => r!.Value));
            }

            _logger.LogInformation("GOV.UK CT: {Count} properties with bands found", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GOV.UK CT band scrape failed: {Msg}", ex.Message);
        }
        return results;
    }

    // Collect all property links, following GOV.UK pagination ("Next page" / page numbers)
    private static async Task<List<(string Address, string Url)>> CollectAllPropertyLinksAsync(
        HttpClient client, string firstPageHtml, string firstPageUrl, string baseUrl, CancellationToken ct)
    {
        var all   = new List<(string, string)>();
        var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var html  = firstPageHtml;
        var pageUrl = firstPageUrl;

        for (int page = 0; page < 10; page++) // max 10 pages safety limit
        {
            var links = ParseGovUkPropertyLinks(html, baseUrl);
            foreach (var l in links)
                if (seen.Add(l.Url)) all.Add(l);

            // Look for a "Next" / "Next page" link
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var nextLink = doc.DocumentNode.SelectNodes("//a")?
                .FirstOrDefault(a =>
                {
                    var t = CleanText(a.InnerText).ToLower();
                    return t == "next" || t == "next page" || t.Contains("next page");
                });

            if (nextLink == null) break;

            var nextHref = nextLink.GetAttributeValue("href", "");
            if (string.IsNullOrEmpty(nextHref)) break;
            if (!nextHref.StartsWith("http")) nextHref = baseUrl + nextHref;
            if (nextHref == pageUrl) break;

            try
            {
                var nextResp = await client.GetAsync(nextHref, ct);
                if (!nextResp.IsSuccessStatusCode) break;
                html    = await nextResp.Content.ReadAsStringAsync(ct);
                pageUrl = nextHref;
            }
            catch { break; }
        }

        return all;
    }

    // Parse the property-list page — returns hrefs + addresses
    private static List<(string Address, string Url)> ParseGovUkPropertyLinks(string html, string baseUrl)
    {
        var list = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(html)) return list;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // GOV.UK lists properties as <a> links whose href contains "property" or "uprn"
        var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchors != null)
        {
            foreach (var a in anchors)
            {
                var href = a.GetAttributeValue("href", "");
                if (!href.Contains("property") && !href.Contains("uprn")) continue;
                if (!href.StartsWith("http")) href = baseUrl + href;
                var addr = CleanText(a.InnerText);
                if (addr.Length > 5) list.Add((addr, href));
            }
        }

        // Fallback: radio buttons where label contains address text
        if (list.Count == 0)
        {
            var radios = doc.DocumentNode.SelectNodes("//input[@type='radio']");
            if (radios != null)
            {
                foreach (var radio in radios)
                {
                    var id    = radio.GetAttributeValue("id", "");
                    var val   = radio.GetAttributeValue("value", "");
                    var label = doc.DocumentNode.SelectSingleNode($"//label[@for='{id}']");
                    var addr  = label != null ? CleanText(label.InnerText) : "";
                    if (addr.Length > 5 && !string.IsNullOrEmpty(val))
                    {
                        // Radio-based list — construct submit URL using value as UPRN
                        list.Add((addr, $"{baseUrl}/check-council-tax-band/property/{val}"));
                    }
                }
            }
        }

        return list;
    }

    // Parse a single property detail page — returns the band letter or null
    private static string? ExtractBandFromGovUkPropertyPage(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var doc  = new HtmlDocument();
        doc.LoadHtml(html);
        var main = doc.DocumentNode.SelectSingleNode("//main")?.InnerText
                ?? doc.DocumentNode.SelectSingleNode("//body")?.InnerText ?? "";

        var ic = System.Text.RegularExpressions.RegexOptions.IgnoreCase;

        // Pass 1 — specific patterns
        foreach (var pat in new[]
        {
            @"[Cc]ouncil [Tt]ax [Bb]and[:\s]+([A-H])\b",
            @"\bband[:\s]+([A-H])\b",
            @"[Bb]and\s+([A-H])\b",
            @"\b([A-H])\s+band\b",
        })
        {
            var m = System.Text.RegularExpressions.Regex.Match(main, pat, ic);
            if (m.Success)
            {
                var l = m.Groups[1].Value.ToUpper();
                if (l[0] >= 'A' && l[0] <= 'H') return l;
            }
        }

        // Pass 2 — GOV.UK summary list / table cells
        var nodes = doc.DocumentNode.SelectNodes(
            "//dd | //td | //*[contains(@class,'govuk-summary-list__value')]"
            + " | //*[contains(@class,'band')]");
        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                var t = CleanText(node.InnerText).Trim().ToUpper();
                if (t.Length == 1 && t[0] >= 'A' && t[0] <= 'H') return t;
                var m2 = System.Text.RegularExpressions.Regex.Match(t, @"^BAND\s+([A-H])\b");
                if (m2.Success) return m2.Groups[1].Value;
                var m3 = System.Text.RegularExpressions.Regex.Match(t, @"^([A-H])\b");
                if (m3.Success && t.Length <= 40) return m3.Groups[1].Value;
            }
        }

        // Pass 3 — aggressive: "band" + up to 20 chars + letter A-H
        var agg = System.Text.RegularExpressions.Regex.Match(
            main, @"\bband\b.{0,20}?\b([A-H])\b", ic);
        if (agg.Success)
        {
            var l = agg.Groups[1].Value.ToUpper();
            if (l[0] >= 'A' && l[0] <= 'H') return l;
        }

        return null;
    }

}
