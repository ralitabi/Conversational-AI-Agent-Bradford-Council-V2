using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Bradford.Core.Models;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    // ── Bin Dates for Selected Address ────────────────────────────────────────
    private async Task<string> GetBinDatesForAddressAsync(string postcode, string address, CancellationToken ct)
    {
        postcode = postcode.Trim().ToUpper();

        var scraped = await TryBradfordBinFormAsync(postcode, address, ct);
        if (scraped == null)
            scraped = await TryBradfordBinApiAsync(postcode, address, ct);

        var checkerUrl = "https://www.bradford.gov.uk/recycling-and-waste/bin-collections/check-your-bin-collection-dates/";

        BinDateCard card;
        bool hasRealDates = scraped?.HasDates == true;

        if (scraped != null)
        {
            card = scraped;
            card.CheckerUrl = checkerUrl;
        }
        else
        {
            card = new BinDateCard
            {
                Address    = $"{address}, {postcode}",
                GreyBin    = "Every 2 weeks (alternating with green bin)",
                GreenBin   = "Every 2 weeks (alternating with grey bin)",
                BrownBin   = "Weekly April–November · Every 2 weeks December–March",
                CheckerUrl = checkerUrl,
                HasDates   = false
            };
        }

        var sb = new StringBuilder();
        sb.AppendLine("[[BIN_DATE_CARD]]");
        sb.AppendLine(JsonSerializer.Serialize(card));
        sb.AppendLine("[[/BIN_DATE_CARD]]");
        sb.AppendLine();

        // IMPORTANT: keep the text reply very short — the card below already shows the full schedule.
        // Never repeat the schedule table or links in the text.
        if (hasRealDates)
        {
            var greyNext = card.GreyBinDates.FirstOrDefault() ?? card.GreyBin;
            sb.AppendLine($"BIN_INSTRUCTION: Write ONE sentence only: confirm the address ({address}, {postcode}) and say the collection schedule is shown in the card below. Mention grey bin next collects on **{greyNext}**. Do NOT list all bins, do NOT add a table, do NOT repeat links — the card already shows everything.");
        }
        else
        {
            sb.AppendLine($"BIN_INSTRUCTION: Write ONE sentence only: confirm the address ({address}, {postcode}) and say the collection schedule is shown in the card below. Do NOT add a schedule table, do NOT list links — the card shows the Grey/Green/Brown schedule and the 'Get exact dates' button already.");
        }

        return sb.ToString();
    }

    // ── Bin knowledge base ────────────────────────────────────────────────────
    // Maps every common bin/recycling topic to the authoritative Bradford Council page(s).
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] BinKnowledgeMap =
    {
        // What goes in each bin
        (new[]{"what goes","grey bin","green bin","recycle","recycling","plastic","cardboard","glass","paper","tin","can","yoghurt","bottle","carton","black tray","food tray","shredded paper","aerosol"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/wheeled-bins-and-recycling-containers/what-goes-in-your-bins/"},
         "What goes in your bins",
         "Would you like to know when your bins are collected? I can check your collection dates if you share your postcode."),

        // General bin schedule for Bradford district
        (new[]{"collection days","collection schedule","which day","what day","alternating","fortnightly","alternate week","general waste","grey bin schedule","green bin schedule","brown bin schedule","when are bins","bin day","collection frequency"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/bin-collections/bin-collections-in-the-bradford-district/"},
         "Bin collections in the Bradford district",
         "Would you like me to check your exact bin collection dates? Just share your postcode and I can look it up."),

        // Missed collection
        (new[]{"missed","not collected","wasn't collected","wasn't emptied","skip","forgot","didn't collect","report missed","delay","broken truck","waiting"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/bin-collections/report-a-missed-bin-collection/"},
         "Report a missed bin collection",
         "Would you like to check your upcoming bin collection dates to plan ahead?"),

        // Food waste
        (new[]{"food waste","food bin","caddy","kitchen waste","food scraps","simpler recycling","autumn 2026","food collection","organic"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/bin-collections/food-waste-collection/"},
         "Food waste collection",
         "Would you like to know what else can go in your recycling or general waste bin?"),

        // Assisted collections
        (new[]{"assisted","can't move","can not move","disabled","elderly","mobility","neighbour","help with bin","unable to move"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/bin-collections/assisted-collections-help-moving-your-bins/"},
         "Assisted bin collections",
         "Would you like to check your bin collection dates or find out about other support services?"),

        // Bad weather
        (new[]{"bad weather","snow","ice","frost","cancelled","disruption","disrupted","weather","delayed","winter","storm"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/bin-collections/bin-collections-in-bad-weather/"},
         "Bin collections in bad weather",
         "Would you like to check your regular bin collection schedule?"),

        // Garden waste / brown bin
        (new[]{"garden waste","brown bin","garden bin","subscribe","subscription","composting","grass cuttings","leaves","branches","hedge","garden service","renew","june 2026","cost of garden","garden waste cost","four weeks","garden collection"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/garden-waste-bin/subscribe-to-or-renew-the-garden-waste-collection-service/",
               "https://www.bradford.gov.uk/recycling-and-waste/garden-waste-bin/garden-waste-bin/"},
         "Garden waste bin subscription",
         "Would you like to know what else goes in the brown bin, or check your collection dates?"),

        // Bulky waste
        (new[]{"bulky","large items","furniture","sofa","mattress","fridge","washing machine","cooker","wardrobe","bed","chair","bulky waste","big item","white goods cost","collect large"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/bulky-waste/bulky-waste-collections/"},
         "Bulky waste collections",
         "Did you know Bradford also offers free collection for certain electrical items? Would you like details?"),

        // New or replacement bins
        (new[]{"new bin","replacement bin","extra bin","damaged bin","broken bin","bin repair","lost bin","stolen bin","bigger bin","smaller bin","bin cost","bin charge","140 litre","240 litre"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/wheeled-bins-and-recycling-containers/get-new-wheeled-bins-or-recycling-containers/"},
         "Get new or replacement bins",
         "Would you like to know what goes in each of your bins?"),

        // Rural collections
        (new[]{"rural","hard to reach","rural vehicle","smaller vehicle","access restriction","country","village"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/bin-collections/rural-collections/"},
         "Rural bin collections",
         "Would you like to check your bin collection dates for your specific address?"),

        // Household waste recycling centres / tips
        (new[]{"recycling centre","tip","household waste site","hwrc","skip","drop off","dispose","plasterboard","permit","van permit"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/household-waste-recycling-centres/search-household-waste-sites/"},
         "Household waste recycling centres",
         "Would you like to know about hazardous or electrical waste disposal options?"),

        // Electrical items
        (new[]{"electrical","electricals","tv","television","fridge","freezer","washing machine","computer","laptop","phone","small appliance","weee","e-waste","electronic"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/bulky-waste/getting-rid-of-electrical-items/"},
         "Getting rid of electrical items",
         "Would you like to know about bulky waste collections for large items like fridges or washing machines?"),

        // Clinical / medical / sharps
        (new[]{"clinical","medical waste","dialysis","sharps","needle","syringe","lancet","insulin","prescription"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/hazardous-waste/syringes-needles-and-sharps-bins/",
               "https://www.bradford.gov.uk/recycling-and-waste/hazardous-waste/clinical-collections/"},
         "Sharps and clinical waste disposal",
         "Would you like information about other hazardous waste disposal options?"),

        // Hazardous
        (new[]{"hazardous","chemical","asbestos","paint","oil","battery","batteries","solvent","pesticide","bleach","toxic"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/hazardous-waste/hazardous-waste/"},
         "Hazardous waste disposal",
         "Would you like to find your nearest household waste recycling centre to drop off hazardous items?"),

        // Aluminium foil
        (new[]{"aluminium foil","foil","tin foil","kitchen foil"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/wheeled-bins-and-recycling-containers/aluminium-foil/"},
         "Recycling aluminium foil",
         "Would you like to know what else can go in your recycling bin?"),

        // General fallback
        (new[]{"bin","recycling","waste","collection","rubbish","refuse","litter"},
         new[]{"https://www.bradford.gov.uk/recycling-and-waste/bin-collections/bin-collections-in-the-bradford-district/",
               "https://www.bradford.gov.uk/recycling-and-waste/wheeled-bins-and-recycling-containers/what-goes-in-your-bins/"},
         "Bin collections and recycling in Bradford",
         "Would you like to check your bin collection dates? Share your postcode and I can look it up for you."),
    };

    private async Task<string> GetBinInfoAsync(string query, CancellationToken ct)
    {
        var q = query.ToLower();

        // Match keywords to get the relevant URL(s), title, and follow-up
        var urls    = new List<string>();
        var title   = "";
        var followUp = "";

        foreach (var (keywords, pages, pageTitle, pageFollowUp) in BinKnowledgeMap)
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

        // Fallback if nothing matched
        if (urls.Count == 0)
        {
            urls.Add("https://www.bradford.gov.uk/recycling-and-waste/bin-collections/bin-collections-in-the-bradford-district/");
            title    = "Bin collections in Bradford";
            followUp = "Would you like to check your bin collection dates? Share your postcode and I can look it up for you.";
        }

        // Scrape up to 2 pages (keep total under ~6 000 chars)
        var sb = new StringBuilder();
        sb.AppendLine($"BRADFORD BIN & RECYCLING INFORMATION — query: \"{query}\"");
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

        // Append official link and follow-up suggestion
        sb.AppendLine($"OFFICIAL_BRADFORD_LINK: [{title}]({urls[0]})");
        if (!string.IsNullOrEmpty(followUp))
            sb.AppendLine($"FOLLOW_UP_SUGGESTION: {followUp}");

        return sb.ToString();
    }

    // ── Bradford bin form scraper ─────────────────────────────────────────────
    private async Task<BinDateCard?> TryBradfordBinFormAsync(string postcode, string address, CancellationToken ct)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                UseCookies               = true,
                CookieContainer          = new CookieContainer(),
                AllowAutoRedirect        = true,
                MaxAutomaticRedirections = 10
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-GB,en;q=0.9");

            // ── Step 1: load initial form page ──────────────────────────────
            const string formBase = "https://onlineforms.bradford.gov.uk/ufs/collectiondates.eb";
            var initResp = await client.GetAsync(formBase, ct);
            if (!initResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("BinForm: initial GET failed {S}", initResp.StatusCode);
                return null;
            }
            var initHtml   = await initResp.Content.ReadAsStringAsync(ct);
            var sessionUrl = initResp.RequestMessage?.RequestUri?.ToString() ?? formBase;

            var initDoc = new HtmlDocument();
            initDoc.LoadHtml(initHtml);

            var formAction      = ResolveFormAction(initDoc, sessionUrl);
            var postcodeField   = FindFirstTextInputName(initDoc) ?? "postcode";
            var step1Fields     = BuildBinFormPost(initDoc, new Dictionary<string, string>
            {
                [postcodeField] = postcode
            }, advancePage: true);

            _logger.LogInformation("BinForm init: action={A} postcodeField={F} fields=[{X}]",
                formAction, postcodeField,
                string.Join(", ", step1Fields.Select(f => $"{f.Key}={f.Value}")));

            client.DefaultRequestHeaders.Add("Referer", sessionUrl);
            client.DefaultRequestHeaders.Add("Origin", "https://onlineforms.bradford.gov.uk");

            var step1Resp = await client.PostAsync(formAction, new FormUrlEncodedContent(step1Fields), ct);
            var step1Html = await step1Resp.Content.ReadAsStringAsync(ct);
            var step1Url  = step1Resp.RequestMessage?.RequestUri?.ToString() ?? formAction;

            _logger.LogInformation("BinForm step1: url={U}", step1Url);

            if (step1Url.Contains("errorPage", StringComparison.OrdinalIgnoreCase))
            {
                // Extract text from error page to understand why it failed
                var errDoc = new HtmlDocument();
                errDoc.LoadHtml(step1Html);
                var errText = CleanText(errDoc.DocumentNode.SelectSingleNode("//body")?.InnerText ?? "");
                _logger.LogWarning("BinForm step1 error. Page says: {E}", errText.Length > 400 ? errText[..400] : errText);
                return null;
            }

            // Check if results arrived immediately (no address-selection step)
            var directCard = TryExtractBinDatesFromHtml(step1Html, address, postcode);
            if (directCard != null)
            {
                _logger.LogInformation("BinForm: direct dates extracted after step1");
                return directCard;
            }

            // ── Step 2: select address from list ────────────────────────────
            var step1Doc = new HtmlDocument();
            step1Doc.LoadHtml(step1Html);

            var uprn = FindUprnForAddress(step1Doc, address);
            if (string.IsNullOrEmpty(uprn))
            {
                _logger.LogWarning("BinForm: no UPRN found for '{A}' in step1 response", address);
                return null;
            }

            _logger.LogInformation("BinForm: UPRN={U} for '{A}'", uprn, address);

            var radioName = step1Doc.DocumentNode
                .SelectNodes("//input[@type='radio']")
                ?.FirstOrDefault()
                ?.GetAttributeValue("name", null);

            var step2Overrides = new Dictionary<string, string>
            {
                [radioName ?? "uprn"] = uprn
            };

            var step2Action = ResolveFormAction(step1Doc, step1Url);
            var step2Fields = BuildBinFormPost(step1Doc, step2Overrides, advancePage: true);

            client.DefaultRequestHeaders.Remove("Referer");
            client.DefaultRequestHeaders.Add("Referer", step1Url);

            var step2Resp = await client.PostAsync(step2Action, new FormUrlEncodedContent(step2Fields), ct);
            var step2Html = await step2Resp.Content.ReadAsStringAsync(ct);
            var step2Url  = step2Resp.RequestMessage?.RequestUri?.ToString() ?? step2Action;

            _logger.LogInformation("BinForm step2: url={U}", step2Url);

            if (step2Url.Contains("errorPage", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("BinForm step2 -> error page");
                return null;
            }

            return TryExtractBinDatesFromHtml(step2Html, address, postcode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("BinForm exception: {M}", ex.Message);
            return null;
        }
    }

    // ── Form helpers ──────────────────────────────────────────────────────────
    private static string ResolveFormAction(HtmlDocument doc, string baseUrl)
    {
        var action = doc.DocumentNode.SelectSingleNode("//form")
            ?.GetAttributeValue("action", "");
        if (string.IsNullOrWhiteSpace(action)) return baseUrl;
        if (action.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return action;
        try
        {
            var b = new Uri(baseUrl);
            return new Uri(b, action).ToString();
        }
        catch { return baseUrl; }
    }

    private static string? FindFirstTextInputName(HtmlDocument doc)
    {
        // Prefer input with "postcode" in name/id/placeholder
        var inputs = doc.DocumentNode.SelectNodes(
            "//input[@type='text' or not(@type) or @type='search']");
        if (inputs == null) return null;

        foreach (var inp in inputs)
        {
            var name  = inp.GetAttributeValue("name",        "");
            var id    = inp.GetAttributeValue("id",          "");
            var ph    = inp.GetAttributeValue("placeholder", "");
            if (name.Contains("postcode", StringComparison.OrdinalIgnoreCase) ||
                id  .Contains("postcode", StringComparison.OrdinalIgnoreCase) ||
                ph  .Contains("postcode", StringComparison.OrdinalIgnoreCase))
                return name;
        }

        // Try labels
        var labels = doc.DocumentNode.SelectNodes("//label");
        if (labels != null)
            foreach (var label in labels)
                if (label.InnerText.Contains("postcode", StringComparison.OrdinalIgnoreCase))
                {
                    var forId = label.GetAttributeValue("for", "");
                    if (!string.IsNullOrEmpty(forId))
                    {
                        var linked = doc.DocumentNode.SelectSingleNode($"//input[@id='{forId}']");
                        if (linked != null) return linked.GetAttributeValue("name", null);
                    }
                }

        // Fall back to first text input
        return inputs.FirstOrDefault()?.GetAttributeValue("name", null);
    }

    private static List<KeyValuePair<string, string>> BuildBinFormPost(
        HtmlDocument doc, Dictionary<string, string> overrides, bool advancePage)
    {
        var fields = new List<KeyValuePair<string, string>>();

        // Collect ALL hidden inputs
        var hidden = doc.DocumentNode.SelectNodes("//input[@type='hidden']");
        if (hidden != null)
            foreach (var n in hidden)
            {
                var name = n.GetAttributeValue("name",  "");
                var val  = n.GetAttributeValue("value", "");
                if (!string.IsNullOrEmpty(name))
                    fields.Add(new(name, val));
            }

        if (advancePage)
        {
            // Ensure PAGE:N.h = T (JS sets this for "Next" navigation)
            SetOrAdd(fields, "PAGE:N.h", "T");

            // Verj.io eForms: JavaScript also writes APAGE:* action-page fields from
            // the HID:inputs manifest before submit. Add them if not already present.
            var hidInputs = fields.FirstOrDefault(f =>
                f.Key.Equals("HID:inputs", StringComparison.OrdinalIgnoreCase)).Value ?? "";
            foreach (var token in hidInputs.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!token.TrimStart().StartsWith("APAGE:", StringComparison.OrdinalIgnoreCase)) continue;
                var apageKey = token.Trim(); // e.g. "APAGE:N.h"
                var pageKey  = apageKey[1..];  // strip leading 'A' → "PAGE:N.h"
                // Value = whatever PAGE:* is currently set to
                var pageVal  = fields.FirstOrDefault(f =>
                    f.Key.Equals(pageKey, StringComparison.OrdinalIgnoreCase)).Value ?? "";
                // Only add if not already in fields
                if (!fields.Any(f => f.Key.Equals(apageKey, StringComparison.OrdinalIgnoreCase)))
                    fields.Add(new(apageKey, pageVal));
            }

            // Simulate click coordinates (JS sets PAGE:X/Y to where the button was clicked)
            SetOrAdd(fields, "PAGE:X", "150");
            SetOrAdd(fields, "PAGE:Y", "20");
        }

        // Apply caller overrides
        foreach (var kv in overrides)
            SetOrAdd(fields, kv.Key, kv.Value);

        // Submit button
        var submit = doc.DocumentNode.SelectSingleNode(
            "//input[@type='submit'] | //button[@type='submit']");
        if (submit != null)
        {
            var sName = submit.GetAttributeValue("name",  "");
            var sVal  = submit.GetAttributeValue("value", "Next");
            if (!string.IsNullOrEmpty(sName))
                fields.Add(new(sName, sVal));
        }

        return fields;
    }

    private static void SetOrAdd(List<KeyValuePair<string, string>> fields, string key, string value)
    {
        var idx = fields.FindIndex(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
            fields[idx] = new(fields[idx].Key, value);
        else
            fields.Add(new(key, value));
    }

    // ── Date extraction from Bradford results page ────────────────────────────
    private static readonly Regex _binDateRx = new(
        @"(?:Mon|Tue|Wed|Thu|Fri|Sat|Sun)(?:day)?\s+\d{1,2}\s+(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\w*\s+\d{4}|" +
        @"(?:Mon|Tue|Wed|Thu|Fri|Sat|Sun)(?:day)?\s+(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\w*\s+\d{1,2}\s+\d{4}|" +
        @"(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)\s+\d{1,2}\s+\w+\s+\d{4}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static BinDateCard? TryExtractBinDatesFromHtml(string html, string address, string postcode)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var body = doc.DocumentNode.SelectSingleNode("//body");
        if (body == null) return null;

        var bodyText = CleanText(body.InnerText);
        if (bodyText.Length < 100) return null;

        if (!bodyText.Contains("collect", StringComparison.OrdinalIgnoreCase) &&
            !bodyText.Contains("bin",     StringComparison.OrdinalIgnoreCase))
            return null;

        // Split page text into per-bin sections
        var (greySection, greenSection, brownSection) = SplitBinSections(bodyText);

        var greyDates  = ExtractDatesFromSection(greySection);
        var greenDates = ExtractDatesFromSection(greenSection);
        var brownDates = ExtractDatesFromSection(brownSection);

        // Fallback: if no sections detected, try to grab all dates from the page
        if (greyDates.Count == 0 && greenDates.Count == 0 && brownDates.Count == 0)
        {
            var allDates = _binDateRx.Matches(bodyText)
                .Select(m => m.Value.Trim())
                .Distinct()
                .Take(30)
                .ToList();
            if (allDates.Count == 0) return null;

            // Heuristic split: grey and green alternate fortnightly, brown monthly
            greyDates  = allDates.Take(10).ToList();
            greenDates = allDates.Skip(10).Take(10).ToList();
            brownDates = allDates.Skip(20).Take(10).ToList();
        }

        if (greyDates.Count == 0 && greenDates.Count == 0) return null;

        return new BinDateCard
        {
            Address       = $"{address}, {postcode}",
            GreyBin       = greyDates.FirstOrDefault()  ?? "",
            GreenBin      = greenDates.FirstOrDefault() ?? "",
            BrownBin      = brownDates.FirstOrDefault() ?? "",
            GreyBinDates  = greyDates,
            GreenBinDates = greenDates,
            BrownBinDates = brownDates,
            HasDates      = true
        };
    }

    private static (string Grey, string Green, string Brown) SplitBinSections(string text)
    {
        // Find the start index of each bin-type section
        var greyIdx  = FindKeywordPos(text, "grey bin", "recycling waste", "grey (recycl", "grey bin (recycl");
        var greenIdx = FindKeywordPos(text, "green bin", "general waste",  "green (general", "green bin (general");
        var brownIdx = FindKeywordPos(text, "brown bin", "garden waste",   "brown (garden", "brown bin (garden");

        // Build ordered list of found sections
        var secs = new List<(int pos, string label)>();
        if (greyIdx  >= 0) secs.Add((greyIdx,  "grey"));
        if (greenIdx >= 0) secs.Add((greenIdx, "green"));
        if (brownIdx >= 0) secs.Add((brownIdx, "brown"));
        secs = secs.OrderBy(s => s.pos).ToList();

        string Slice(int idx) {
            if (idx < 0) return "";
            var si = secs.FindIndex(s => s.pos == idx);
            var start = secs[si].pos;
            var end   = si + 1 < secs.Count ? secs[si + 1].pos : text.Length;
            return text[start..end];
        }

        return (Slice(greyIdx), Slice(greenIdx), Slice(brownIdx));
    }

    private static int FindKeywordPos(string text, params string[] keywords)
    {
        foreach (var kw in keywords)
        {
            var i = text.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
            if (i >= 0) return i;
        }
        return -1;
    }

    private static List<string> ExtractDatesFromSection(string section)
    {
        if (string.IsNullOrWhiteSpace(section)) return new();
        return _binDateRx.Matches(section)
            .Select(m => m.Value.Trim())
            .Distinct()
            .Take(12)
            .ToList();
    }

    // ── Scraped comprehensive bin info (kept, not in main flow) ──────────────
    private async Task<string> ScrapeComprehensiveBinInfoAsync(CancellationToken ct)
    {
        var pages = new[]
        {
            "https://www.bradford.gov.uk/recycling-and-waste/bin-collections/",
            "https://www.bradford.gov.uk/recycling-and-waste/what-can-i-recycle/"
        };
        var tasks = pages.Select(url => FetchPageTextAsync(url, ct)).ToList();
        await Task.WhenAll(tasks);
        return string.Join("\n\n---\n\n", tasks.Select(t => t.Result).Where(r => !string.IsNullOrWhiteSpace(r)));
    }

    private async Task<string> FetchPageTextAsync(string url, CancellationToken ct)
    {
        var html = await FetchHtmlAsync(url, ct);
        if (html == null) return "";
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        foreach (var tag in new[] { "nav", "header", "footer", "script", "style", "aside", "noscript" })
        {
            var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
            if (nodes != null) foreach (var n in nodes.ToList()) n.Remove();
        }
        var main = doc.DocumentNode.SelectSingleNode("//main")
                ?? doc.DocumentNode.SelectSingleNode("//article")
                ?? doc.DocumentNode.SelectSingleNode("//body");
        return main == null ? "" : TruncateText(CleanText(main.InnerText), 1200);
    }

    // ── Bradford Bin Dates JSON API (fallback) ────────────────────────────────
    private async Task<BinDateCard?> TryBradfordBinApiAsync(string postcode, string address, CancellationToken ct)
    {
        var urls = new[]
        {
            $"https://www.bradford.gov.uk/api/bincollection?postcode={Uri.EscapeDataString(postcode)}&address={Uri.EscapeDataString(address)}",
            $"https://www.bradford.gov.uk/bins/dates?postcode={Uri.EscapeDataString(postcode)}"
        };
        foreach (var url in urls)
        {
            try
            {
                var html = await FetchHtmlAsync(url, ct);
                if (html == null) continue;
                if (!html.TrimStart().StartsWith("{") && !html.TrimStart().StartsWith("[")) continue;
                var card = TryParseBinDatesJson(html, address, postcode);
                if (card != null) return card;
            }
            catch { }
        }
        return null;
    }

    private static BinDateCard? TryParseBinDatesJson(string json, string address, string postcode)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new BinDateCard
            {
                Address  = $"{address}, {postcode}",
                GreyBin  = root.TryGetProperty("grey",         out var g)  ? g.GetString()  ?? "" :
                           root.TryGetProperty("generalWaste", out var gw) ? gw.GetString() ?? "" : "",
                GreenBin = root.TryGetProperty("green",     out var gr) ? gr.GetString() ?? "" :
                           root.TryGetProperty("recycling", out var r)  ? r.GetString()  ?? "" : "",
                BrownBin = root.TryGetProperty("brown",  out var br) ? br.GetString() ?? "" :
                           root.TryGetProperty("garden", out var gd) ? gd.GetString() ?? "" : ""
            };
        }
        catch { return null; }
    }

    // ── Address match helpers (also used by TryBradfordBinFormAsync) ──────────
    private static string? FindUprnForAddress(HtmlDocument doc, string address)
    {
        var options = doc.DocumentNode.SelectNodes("//select//option");
        if (options != null)
        {
            var best = options
                .Where(o => o.GetAttributeValue("value", "").Length > 0)
                .OrderByDescending(o => ScoreAddressMatch(CleanText(o.InnerText), address))
                .FirstOrDefault();
            if (best != null && ScoreAddressMatch(CleanText(best.InnerText), address) > 0)
                return best.GetAttributeValue("value", "");
        }

        var radios = doc.DocumentNode.SelectNodes("//input[@type='radio']");
        if (radios != null)
            foreach (var radio in radios)
            {
                var id    = radio.GetAttributeValue("id", "");
                var label = doc.DocumentNode.SelectSingleNode($"//label[@for='{id}']");
                if (label != null && ScoreAddressMatch(CleanText(label.InnerText), address) > 0)
                    return radio.GetAttributeValue("value", "");
            }

        return null;
    }
}
