using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Bradford.Core.Models;

namespace Bradford.Infrastructure.Tools;

// ── Internal school data model ────────────────────────────────────────────────
internal sealed class SchoolData
{
    public string Urn          { get; set; } = "";
    public string Name         { get; set; } = "";
    public string Address      { get; set; } = "";
    public string Phone        { get; set; } = "";
    public string Website      { get; set; } = "";
    public string Phase        { get; set; } = "";
    public string Type         { get; set; } = "";
    public string OfstedRating { get; set; } = "";
    public string OfstedDate   { get; set; } = "";
    public string Pupils       { get; set; } = "";
    public string AgeRange     { get; set; } = "";
    public string Distance     { get; set; } = "";
    public string DetailsUrl   { get; set; } = "";
}

public partial class CouncilToolService
{
    private const string SchoolFinderBase = "https://bso.bradford.gov.uk/council/schools/schoolfinder.aspx";

    // ── School finder (postcode search) ──────────────────────────────────────
    private async Task<string> FindSchoolsNearPostcodeAsync(string postcode, string phase, CancellationToken ct)
    {
        postcode = postcode.Trim().ToUpper();

        // 1. Scrape BSO — distances are already included in the BSO response
        var schools = await ScrapeBradfordSchoolsAsync(postcode, schoolName: null, phase, ct);

        if (schools.Count == 0)
            return $"No schools found near {postcode}. " +
                   "Check Bradford's school finder at https://www.bradford.gov.uk/education-and-skills/find-a-school/schools-finder/";

        // BSO already provides distance in each row — no postcodes.io lookup needed.
        // 3. Sort by distance and apply phase filter

        var sorted = schools
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .OrderBy(s => ParseDistanceValue(s.Distance))
            .ToList();

        if (!string.IsNullOrWhiteSpace(phase))
            sorted = sorted.Where(s =>
                s.Phase.Contains(phase, StringComparison.OrdinalIgnoreCase) ||
                phase.Contains(s.Phase, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(phase, StringComparison.OrdinalIgnoreCase)).ToList();

        if (sorted.Count == 0)
            return $"No {phase?.ToLower()} schools found near {postcode}.";

        var phaseLabel = string.IsNullOrWhiteSpace(phase) ? "" : $"{phase.Trim()} ";
        var closest    = sorted.FirstOrDefault();

        var options = sorted.Select((s, i) => new SchoolOption
        {
            Number       = i + 1,
            Name         = s.Name,
            Address      = s.Address,
            Phase        = s.Phase,
            Type         = s.Type,
            OfstedRating = s.OfstedRating,
            Distance     = s.Distance,
            Urn          = s.Urn,
            Website      = s.Website,
            Phone        = s.Phone
        }).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("[[SCHOOL_LIST]]");
        sb.AppendLine(JsonSerializer.Serialize(options));
        sb.AppendLine("[[/SCHOOL_LIST]]");
        sb.AppendLine();
        sb.AppendLine($"SCHOOL_INSTRUCTION: Found {options.Count} {phaseLabel}schools near {postcode}, sorted nearest first. " +
                      $"Closest: {closest?.Name} ({closest?.Distance}). " +
                      "The UI shows the full scrollable list with distance and Ofsted rating on each row. " +
                      "Briefly name the closest 2-3 schools and their Ofsted ratings. " +
                      "End: \"Scroll through all {options.Count} schools or tap one for full details.\"");
        return sb.ToString();
    }

    // ── Bulk postcode → lat/lon lookup (postcodes.io) ─────────────────────────
    private async Task<Dictionary<string, (double lat, double lon)>> BulkLookupPostcodesAsync(
        List<string> postcodes, CancellationToken ct)
    {
        var result = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);
        if (postcodes.Count == 0) return result;

        _logger.LogInformation("SchoolDist: bulk lookup {N} postcodes: {P}",
            postcodes.Count, string.Join(", ", postcodes.Take(5)));

        try
        {
            // Use a fresh HttpClient — the Crawler client has HTML-specific headers
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            foreach (var batch in postcodes.Chunk(100))
            {
                var body    = JsonSerializer.Serialize(new { postcodes = batch });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp    = await client.PostAsync("https://api.postcodes.io/postcodes", content, ct);

                _logger.LogInformation("SchoolDist: postcodes.io status={S}", resp.StatusCode);
                if (!resp.IsSuccessStatusCode) continue;

                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("result", out var arr)) continue;

                foreach (var item in arr.EnumerateArray())
                {
                    var query = item.TryGetProperty("query",  out var q) ? q.GetString() ?? "" : "";
                    var res   = item.TryGetProperty("result", out var r) ? r : default;
                    if (res.ValueKind != JsonValueKind.Object) continue;

                    if (res.TryGetProperty("latitude",  out var lat) &&
                        res.TryGetProperty("longitude", out var lon))
                        result[query] = (lat.GetDouble(), lon.GetDouble());
                }
            }

            _logger.LogInformation("SchoolDist: resolved {N}/{T} postcodes",
                result.Count, postcodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Bulk postcode lookup failed: {M}", ex.Message);
        }

        return result;
    }

    private static string? ExtractPostcode(string address)
    {
        if (string.IsNullOrEmpty(address)) return null;
        var m = Regex.Match(address,
            @"\b([A-Z]{1,2}\d{1,2}[A-Z]?\s*\d[A-Z]{2})\b", RegexOptions.IgnoreCase);
        return m.Success ? Regex.Replace(m.Value, @"\s+", " ").Trim().ToUpper() : null;
    }

    // ── School details (fetch BSO detail page) ───────────────────────────────
    private async Task<string> GetSchoolDetailsAsync(string schoolName, string urn, CancellationToken ct)
    {
        string detailUrl   = "";
        string resolvedUrn = urn.Trim();
        string? detailHtml = null;

        if (!string.IsNullOrEmpty(resolvedUrn))
        {
            // Path 1: URN known — build URL directly
            var slug = Regex.Replace(
                Regex.Replace(schoolName.ToLower(), @"[^a-z0-9]+", "-").Trim('-'),
                @"-{2,}", "-");
            detailUrl = $"https://bso.bradford.gov.uk/school-detail/{resolvedUrn}-{slug}";
        }
        else
        {
            // Path 2: BSO name search — redirects to the detail page when name is unique
            var (nameHtml, finalUrl) = await SearchBsoByNameAsync(schoolName, ct);
            if (!string.IsNullOrEmpty(nameHtml) && finalUrl.Contains("/school-detail/"))
            {
                detailHtml   = nameHtml;
                detailUrl    = finalUrl;
                var urnMatch = Regex.Match(finalUrl, @"/school-detail/(\d+)");
                resolvedUrn  = urnMatch.Success ? urnMatch.Groups[1].Value : "";
                _logger.LogInformation("SchoolDetail: name search redirected to {U}", finalUrl);
            }
            else
            {
                // Path 3: fallback — search multiple postcodes across Bradford for the school
                foreach (var pc in new[] { "BD7 1DP", "BD1 1HY", "BD18 3EH", "BD2 4BJ", "BD4 0RN" })
                {
                    var schools = await ScrapeBradfordSchoolsAsync(pc, null, null, ct);
                    var match   = schools.FirstOrDefault(s =>
                                      s.Name.Equals(schoolName, StringComparison.OrdinalIgnoreCase));
                    if (match != null && !string.IsNullOrEmpty(match.DetailsUrl))
                    {
                        detailUrl   = match.DetailsUrl;
                        resolvedUrn = match.Urn;
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(detailUrl))
            return $"Could not find details for '{schoolName}'. " +
                   "Try: https://bso.bradford.gov.uk/council/schools/schoolfinder.aspx";

        // Fetch the detail page (reuse if already fetched by name search)
        var html = detailHtml ?? await FetchHtmlAsync(detailUrl, ct);
        if (string.IsNullOrEmpty(html))
            return $"Could not load details for '{schoolName}'. " +
                   $"Visit: {detailUrl}";

        // Strip scripts/styles and collapse whitespace
        html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style[^>]*>.*?</style>",   "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var text = Regex.Replace(html, @"<[^>]+>", " ");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim();

        // Parse structured fields with regex
        string ParseField(string label, string stopAt = @"(?:Phase|Control|Headteacher|Address|Chair|Tel|Fax|Website|Estab|Ofsted|Map|School|Department|Opening|Start|Finish)")
        {
            var m = Regex.Match(text, $@"{label}\s*:?\s*([^\n]+?)(?:\s+{stopAt}|\s*$)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        var name        = ParseField("(Primary|Secondary|Nursery|Special|Academy|College) School Name",
                                     @"Phase") is { Length: > 0 } n ? n : schoolName;
        var phase       = ParseField("Phase");
        var control     = ParseField("Control");   // Academy / Community / Foundation / Voluntary Aided
        var headteacher = Regex.Match(text, @"Headteacher\s*:?\s*([^:]+?)(?:\s+Chair|\s+Address)", RegexOptions.IgnoreCase) is { Success: true } hm
                          ? hm.Groups[1].Value.Trim() : "";
        var address     = Regex.Match(text, @"Address\s+(.*?BD\d{1,2}\s*\d[A-Z]{2})", RegexOptions.IgnoreCase | RegexOptions.Singleline) is { Success: true } am
                          ? Regex.Replace(am.Groups[1].Value.Trim(), @"\s+", " ") : "";
        var tel         = ParseField("Tel",     @"(?:Fax|Website|Estab|Map)");
        var website     = ParseField("Website", @"(?:Estab|Map|School|Department)");
        var estab       = Regex.Match(text, @"Estab\s*no\s*:?\s*(\d+)", RegexOptions.IgnoreCase) is { Success: true } em
                          ? em.Groups[1].Value : "";

        // Normalise website URL
        if (!string.IsNullOrEmpty(website) && !website.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            website = "https://" + website.TrimStart('/');

        // Determine school type from Control field
        var schoolType = control switch
        {
            var c when c.Contains("Academy",    StringComparison.OrdinalIgnoreCase) => "Academy",
            var c when c.Contains("Community",  StringComparison.OrdinalIgnoreCase) => "Community School",
            var c when c.Contains("Foundation", StringComparison.OrdinalIgnoreCase) => "Foundation School",
            var c when c.Contains("Voluntary",  StringComparison.OrdinalIgnoreCase) => "Voluntary Aided",
            var c when c.Contains("Free",       StringComparison.OrdinalIgnoreCase) => "Free School",
            var c when string.IsNullOrEmpty(c)                                       => "",
            _                                                                        => control
        };

        var isAcademy  = schoolType.Contains("Academy", StringComparison.OrdinalIgnoreCase) ||
                         schoolType.Contains("Free",    StringComparison.OrdinalIgnoreCase);
        var termPeriods = BuildBradfordTermDates();

        // Fetch Ofsted rating and enrich school data
        var schoolTermUrl  = string.Empty;
        var facilities     = new List<string>();
        var ofstedRating   = string.Empty;
        var ofstedUrn      = resolvedUrn;
        var ageRange       = InferAgeRange(phase, string.IsNullOrEmpty(name) ? schoolName : name);

        if (!string.IsNullOrEmpty(website))
        {
            var (termUrl2, facs) = await FetchSchoolWebsiteDataAsync(website, ct);
            schoolTermUrl = termUrl2;
            facilities    = facs;
        }

        // Try to fetch Ofsted rating from reports.ofsted.gov.uk search
        var (fetchedRating, fetchedOfstedUrn) = await FetchOfstedRatingAsync(
            string.IsNullOrEmpty(name) ? schoolName : name, ct);
        if (!string.IsNullOrEmpty(fetchedRating))   ofstedRating  = fetchedRating;
        if (!string.IsNullOrEmpty(fetchedOfstedUrn)) ofstedUrn     = fetchedOfstedUrn;

        var resolvedOfstedUrl = string.IsNullOrEmpty(ofstedUrn)
            ? "https://reports.ofsted.gov.uk/"
            : $"https://reports.ofsted.gov.uk/provider/21/{ofstedUrn}";

        var card = new SchoolCard
        {
            Name          = string.IsNullOrEmpty(name) ? schoolName : name,
            Address       = address,
            Phone         = tel,
            Website       = website,
            Phase         = phase,
            Type          = schoolType,
            Headteacher   = headteacher,
            OfstedRating  = ofstedRating,
            OfstedDate    = "",
            Pupils        = "",
            AgeRange      = ageRange,
            Urn           = resolvedUrn,
            AdmissionsUrl = "https://www.bradford.gov.uk/education-and-skills/school-admissions/apply-for-a-place-at-one-of-bradford-districts-schools/",
            OfstedUrl     = resolvedOfstedUrl,
            TransportUrl  = "https://www.bradford.gov.uk/education-and-skills/travel-assistance/assistance-with-travel-to-home-school-and-college/",
            FreeMealsUrl  = "https://www.bradford.gov.uk/education-and-skills/school-meals/paying-for-school-meals/",
            TermDatesUrl  = !string.IsNullOrEmpty(schoolTermUrl) ? schoolTermUrl
                            : "https://www.bradford.gov.uk/education-and-skills/school-holidays-and-term-dates/school-holidays-and-term-dates/",
            TermPeriods   = termPeriods,
            AcademicYear  = "2025/26 & 2026/27",
            IsAcademy     = isAcademy,
            Facilities    = facilities
        };

        _logger.LogInformation("SchoolDetail: {N} | head={H} | facs={F} | periods={P}",
            card.Name, card.Headteacher, string.Join(",", facilities.Take(3)), card.TermPeriods.Count);

        var sb = new StringBuilder();
        sb.AppendLine("[[SCHOOL_CARD]]");
        sb.AppendLine(JsonSerializer.Serialize(card));
        sb.AppendLine("[[/SCHOOL_CARD]]");
        sb.AppendLine();
        sb.AppendLine("SCHOOL_DETAIL_INSTRUCTION — critical rules:");
        sb.AppendLine("The card already shows: school name, phase, type, Ofsted rating, age range, address, headteacher, phone, website.");
        sb.AppendLine("NEVER mention any of those facts in your text — they are already visible.");
        sb.AppendLine();
        sb.AppendLine("Write exactly 4 short paragraphs. Use ## headings so each becomes a separate chat bubble. Keep each to 1-2 sentences:");
        sb.AppendLine();
        sb.AppendLine($"## About {card.Name}");
        sb.AppendLine("One warm sentence about the school's ethos or character — what makes it distinctive. Do NOT mention rating, age range, address, or headteacher.");
        if (facilities.Count > 0)
        {
            // Strip emoji characters before including in text instruction
            var facLabels = facilities.Take(2)
                .Select(f => System.Text.RegularExpressions.Regex.Replace(f, @"\p{Cs}|\p{So}|\p{Sm}", "").Trim());
            sb.AppendLine($"You may mention: {string.Join(", ", facLabels)}.");
        }
        sb.AppendLine();
        sb.AppendLine("## How to Apply");
        sb.AppendLine("Bradford uses a central admissions system — applications open every September for the following September entry, with a **closing date in January**. Tap 'Apply for a school place' in the card.");
        sb.AppendLine();
        sb.AppendLine("## Support for Families");
        sb.AppendLine("Mention **free school meals** eligibility (button in the card), **school transport** if over the qualifying distance, and **SEND support** from the school and Bradford Council.");
        sb.AppendLine();
        sb.AppendLine("## Term Dates");
        sb.AppendLine("Say exactly: 'Would you like to see the **term dates and holiday schedule** for this school? Just say **yes** and I'll show you the full calendar.'");
        return sb.ToString();
    }

    // ── BSO name search → returns (detailHtml, detailUrl).
    //    Single match  → BSO redirects to /school-detail/{urn}-{slug} directly.
    //    Multiple match → BSO goes to schoolsearchresults.aspx; we pick the best
    //                     <td class="schoolName"> link and follow it.
    private async Task<(string html, string finalUrl)> SearchBsoByNameAsync(string name, CancellationToken ct)
    {
        try
        {
            using var handler = new System.Net.Http.HttpClientHandler
            {
                UseCookies = true, AllowAutoRedirect = true, MaxAutomaticRedirections = 5
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,*/*");

            var getHtml = await (await client.GetAsync(SchoolFinderBase, ct)).Content.ReadAsStringAsync(ct);
            var getDoc  = new HtmlDocument();
            getDoc.LoadHtml(getHtml);

            var fields = ExtractHiddenFields(getDoc);
            fields["ctl00$ContentPlaceHolder1$tbxSchoolName"] = name;
            fields["ctl00$ContentPlaceHolder1$tbxPostcode"]   = "";
            fields["ctl00$ContentPlaceHolder1$btnSearch"]     = "Search";
            client.DefaultRequestHeaders.Add("Referer", SchoolFinderBase);

            var postResp = await client.PostAsync(SchoolFinderBase,
                new FormUrlEncodedContent(fields.Select(kv =>
                    new KeyValuePair<string, string>(kv.Key, kv.Value))), ct);
            var html     = await postResp.Content.ReadAsStringAsync(ct);
            var finalUrl = postResp.RequestMessage?.RequestUri?.ToString() ?? "";

            // Single match: BSO redirected directly to the detail page
            if (finalUrl.Contains("/school-detail/"))
                return (html, finalUrl);

            // Multiple matches: schoolsearchresults.aspx — find best link
            if (finalUrl.Contains("schoolsearchresults"))
            {
                var resultsDoc = new HtmlDocument();
                resultsDoc.LoadHtml(html);

                // Links are in <td class="schoolName"><a href="/school-detail/...">Name</a></td>
                var links = resultsDoc.DocumentNode.SelectNodes("//td[contains(@class,'schoolName')]//a");
                if (links != null && links.Count > 0)
                {
                    var best = links.FirstOrDefault(a =>
                                   CleanText(a.InnerText).Equals(name, StringComparison.OrdinalIgnoreCase))
                            ?? links.OrderByDescending(a =>
                                   ScoreAddressMatch(CleanText(a.InnerText), name)).First();

                    var href = best.GetAttributeValue("href", "").Trim();
                    if (!string.IsNullOrEmpty(href))
                    {
                        var detailUrl = href.StartsWith("http")
                            ? href
                            : "https://bso.bradford.gov.uk" + (href.StartsWith("/") ? href : "/" + href);
                        var detailResp    = await client.GetAsync(detailUrl, ct);
                        var detailHtml    = await detailResp.Content.ReadAsStringAsync(ct);
                        var detailFinalUrl = detailResp.RequestMessage?.RequestUri?.ToString() ?? detailUrl;
                        _logger.LogInformation("SchoolDetail: multi-result → {U}", detailFinalUrl);
                        return (detailHtml, detailFinalUrl);
                    }
                }
            }

            // No results with full name — try progressively shorter terms (avoids recursion)
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (int take in new[] { 2, 1 })
            {
                if (words.Length <= take) continue;
                var shortName = string.Join(" ", words.Take(take));
                _logger.LogInformation("SchoolDetail: retrying with first {T} words: '{S}'", take, shortName);
                var shortFields = new Dictionary<string, string>(fields)
                {
                    ["ctl00$ContentPlaceHolder1$tbxSchoolName"] = shortName
                };
                var shortContent = new FormUrlEncodedContent(shortFields.Select(kv =>
                    new KeyValuePair<string, string>(kv.Key, kv.Value)));
                var shortResp    = await client.PostAsync(SchoolFinderBase, shortContent, ct);
                var shortHtml    = await shortResp.Content.ReadAsStringAsync(ct);
                var shortUrl     = shortResp.RequestMessage?.RequestUri?.ToString() ?? "";

                if (shortUrl.Contains("/school-detail/"))
                    return (shortHtml, shortUrl);

                if (shortUrl.Contains("schoolsearchresults"))
                {
                    var sDoc = new HtmlDocument(); sDoc.LoadHtml(shortHtml);
                    var sLinks = sDoc.DocumentNode.SelectNodes("//td[contains(@class,'schoolName')]//a");
                    if (sLinks != null && sLinks.Count > 0 && sLinks.Count <= 8)
                    {
                        // Only pick if exact name match or high-confidence score (>= 2)
                        var sBest = sLinks.FirstOrDefault(a =>
                                        CleanText(a.InnerText).Equals(name, StringComparison.OrdinalIgnoreCase));
                        if (sBest == null)
                        {
                            var scored = sLinks
                                .Select(a => (node: a, score: ScoreAddressMatch(CleanText(a.InnerText), name)))
                                .OrderByDescending(x => x.score).First();
                            if (scored.score >= 2) sBest = scored.node;
                        }
                        if (sBest != null)
                        {
                            var sHref = sBest.GetAttributeValue("href", "").Trim();
                            if (!string.IsNullOrEmpty(sHref))
                            {
                                var dUrl  = sHref.StartsWith("http") ? sHref
                                            : "https://bso.bradford.gov.uk" + (sHref.StartsWith("/") ? sHref : "/" + sHref);
                                var dResp = await client.GetAsync(dUrl, ct);
                                var dHtml = await dResp.Content.ReadAsStringAsync(ct);
                                return (dHtml, dResp.RequestMessage?.RequestUri?.ToString() ?? dUrl);
                            }
                        }
                    }
                }
            }

            return ("", "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("SearchBsoByName failed for '{N}': {M}", name, ex.Message);
            return ("", "");
        }
    }

    // ── Bradford school finder scraper ────────────────────────────────────────
    private async Task<List<SchoolData>> ScrapeBradfordSchoolsAsync(
        string? postcode, string? schoolName, string? phase, CancellationToken ct)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                UseCookies               = true,
                CookieContainer          = new CookieContainer(),
                AllowAutoRedirect        = true,
                MaxAutomaticRedirections = 5
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,*/*");

            // ── Step 1: GET the form to extract ASP.NET ViewState ──
            var getResp = await client.GetAsync(SchoolFinderBase, ct);
            if (!getResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("SchoolFinder: GET failed {S}", getResp.StatusCode);
                return new();
            }
            var getHtml = await getResp.Content.ReadAsStringAsync(ct);
            var getDoc  = new HtmlDocument();
            getDoc.LoadHtml(getHtml);

            // ── Step 2: Build POST fields ─────────────────────────────────────
            var fields = ExtractHiddenFields(getDoc);
            bool byPostcode = !string.IsNullOrWhiteSpace(postcode);

            if (byPostcode)
            {
                // Postcode search form
                fields["ctl00$ContentPlaceHolder1$tbPostcodeArea"] = postcode!.Trim().ToUpper();
                fields["ctl00$ContentPlaceHolder1$listRadius"]     = "3";  // 3-mile radius
                fields["ctl00$ContentPlaceHolder1$btnPostcodeSearch"] = "Search";
            }
            else
            {
                // School name search form
                fields["ctl00$ContentPlaceHolder1$tbxSchoolName"] = schoolName ?? "";
                fields["ctl00$ContentPlaceHolder1$tbxPostcode"]   = "";
                fields["ctl00$ContentPlaceHolder1$btnSearch"]     = "Search";
            }

            client.DefaultRequestHeaders.Add("Referer", SchoolFinderBase);
            _logger.LogInformation("SchoolFinder: posting {Mode} search for '{Q}'",
                byPostcode ? "postcode" : "name", byPostcode ? postcode : schoolName);

            var postContent = new FormUrlEncodedContent(fields.Select(kv =>
                new KeyValuePair<string, string>(kv.Key, kv.Value)));
            var postResp = await client.PostAsync(SchoolFinderBase, postContent, ct);
            var postHtml = await postResp.Content.ReadAsStringAsync(ct);

            _logger.LogInformation("SchoolFinder: POST {Status}, body length={L}",
                postResp.StatusCode, postHtml.Length);

            // ── Step 3: Parse results ─────────────────────────────────────────
            var results = ParseSchoolResults(postHtml, byPostcode ? postcode! : null);

            // Phase filter
            if (!string.IsNullOrWhiteSpace(phase))
                results = results
                    .Where(s => s.Phase.Contains(phase, StringComparison.OrdinalIgnoreCase) ||
                                phase.Contains(s.Phase, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var withDist = results.Count(s => !string.IsNullOrEmpty(s.Distance));
            _logger.LogInformation("SchoolFinder: {N} schools, {D} with BSO distance. Sample: [{A}]",
                results.Count, withDist,
                string.Join("; ", results.Take(3).Select(s => $"{s.Name}|addr={s.Address}|dist={s.Distance}")));
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("SchoolFinder failed: {M}", ex.Message);
            return new();
        }
    }

    // ── ASP.NET form hidden field extractor ───────────────────────────────────
    private static Dictionary<string, string> ExtractHiddenFields(HtmlDocument doc)
    {
        var fields = new Dictionary<string, string>();
        var inputs = doc.DocumentNode.SelectNodes("//input[@type='hidden']");
        if (inputs == null) return fields;
        foreach (var inp in inputs)
        {
            var name = inp.GetAttributeValue("name", "");
            var val  = inp.GetAttributeValue("value", "");
            if (!string.IsNullOrEmpty(name))
                fields[name] = val;
        }
        return fields;
    }

    // ── Parse Bradford BSO school finder results HTML ────────────────────────
    // BSO structure (confirmed):
    //   <tr><th>Schools in your search area</th><th>Distance (miles)</th></tr>  ← header
    //   <tr><th>Primary School</th></tr>                                         ← phase group
    //   <tr><th class="schoolName"><a href="/school-detail/URN-slug">Name</a></th>
    //       <td class="schoolDistance">0.37</td></tr>                            ← school row
    private List<SchoolData> ParseSchoolResults(string html, string? postcode)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var schools = new List<SchoolData>();

        var rows = doc.DocumentNode.SelectNodes("//tr");
        if (rows == null) return schools;

        string currentPhase = "";

        foreach (var row in rows)
        {
            var th = row.SelectSingleNode("th");
            var td = row.SelectSingleNode("td");

            // Phase group header: row has only <th>, no <td>
            if (th != null && td == null)
            {
                var label = CleanText(th.InnerText);
                var inferred = InferPhaseFromLabel(label);
                if (!string.IsNullOrEmpty(inferred))
                    currentPhase = inferred;
                continue;
            }

            // School data row: <th> (name) + <td> (distance)
            if (th == null || td == null) continue;

            var school = new SchoolData();

            // Name — from <a> inside <th>
            var link = th.SelectSingleNode(".//a");
            if (link != null)
            {
                school.Name = CleanText(link.InnerText);
                var href = link.GetAttributeValue("href", "").Trim();
                if (!string.IsNullOrEmpty(href))
                {
                    school.DetailsUrl = href.StartsWith("http")
                        ? href
                        : "https://bso.bradford.gov.uk" + (href.StartsWith("/") ? href : "/" + href);

                    // URN is the number before the first dash in the slug
                    // e.g. /school-detail/50082-copthorne-primary-school → URN=50082
                    var urnMatch = Regex.Match(href, @"/school-detail/(\d+)");
                    if (urnMatch.Success)
                        school.Urn = urnMatch.Groups[1].Value;
                }
            }
            else
            {
                school.Name = CleanText(th.InnerText);
            }

            if (string.IsNullOrEmpty(school.Name) || school.Name.Length < 3) continue;

            // Distance — BSO provides it directly in <td>
            var distRaw = CellText(td)
                .Replace("miles", "", StringComparison.OrdinalIgnoreCase)
                .Replace("mile",  "", StringComparison.OrdinalIgnoreCase)
                .Trim();
            if (double.TryParse(distRaw,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var distVal) && distVal >= 0 && distVal < 100)
                school.Distance = $"{distVal:F2} mi";

            // Phase — from current group header, or infer from name
            school.Phase = !string.IsNullOrEmpty(currentPhase)
                ? currentPhase
                : InferPhase(school.Name);

            schools.Add(school);
        }

        _logger.LogInformation("BSO parse: {N} schools, {D} with distance. Top 3: [{T}]",
            schools.Count,
            schools.Count(s => !string.IsNullOrEmpty(s.Distance)),
            string.Join("; ", schools.Take(3).Select(s => $"{s.Name}={s.Distance}")));

        return schools
            .Where(s => !string.IsNullOrEmpty(s.Name))
            .DistinctBy(s => s.Name)
            .OrderBy(s => ParseDistanceValue(s.Distance))
            .ToList();
    }

    // Raw cell text without CleanText's Length>2 filter (needed for short values like "1", "2")
    private static string CellText(HtmlNode cell) =>
        Regex.Replace(cell.InnerText, @"\s+", " ").Replace(" ", " ").Trim();

    private static string FormatDistance(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        raw = raw.Trim();
        if (Regex.IsMatch(raw, @"^\d+(\.\d+)?$"))   // bare number
            return raw + " mi";
        if (raw.EndsWith("miles", StringComparison.OrdinalIgnoreCase))
            return raw[..^5].Trim() + " mi";
        return raw;
    }

    private static double ParseDistanceValue(string dist)
    {
        if (string.IsNullOrEmpty(dist)) return 999;
        var m = Regex.Match(dist, @"[\d.]+");
        return m.Success && double.TryParse(m.Value, out var d) ? d : 999;
    }

    private static string InferPhaseFromLabel(string t) => t.ToLower() switch
    {
        var s when s.Contains("primary")   => "Primary",
        var s when s.Contains("secondary") => "Secondary",
        var s when s.Contains("nursery")   => "Nursery",
        var s when s.Contains("sixth")     => "Sixth Form",
        var s when s.Contains("infant")    => "Primary",
        var s when s.Contains("junior")    => "Primary",
        var s when s.Contains("all-through") || s.Contains("all through") => "All-through",
        _                                  => ""
    };

    // ── School transport / travel assistance ─────────────────────────────────
    private const string TransportUrl = "https://www.bradford.gov.uk/education-and-skills/travel-assistance/assistance-with-travel-to-home-school-and-college/";

    private async Task<string> GetSchoolTransportAsync(string query, CancellationToken ct)
    {
        var html = await FetchHtmlAsync(TransportUrl, ct);
        if (string.IsNullOrEmpty(html))
            return TransportFallback();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove nav / header / footer / scripts
        foreach (var tag in new[] { "nav", "header", "footer", "script", "style", "aside", "noscript" })
        {
            var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
            if (nodes != null) foreach (var n in nodes.ToList()) n.Remove();
        }

        // ── Extract document links ────────────────────────────────────────────
        var docLinks = doc.DocumentNode.SelectNodes("//a[@href]")
            ?.Where(a =>
            {
                var href = a.GetAttributeValue("href", "").ToLower();
                return href.Contains(".pdf") || href.Contains(".doc") || href.Contains(".docx");
            })
            .Select(a =>
            {
                var href = a.GetAttributeValue("href", "").Trim();
                var text = CleanText(a.InnerText);
                var full = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? href : "https://www.bradford.gov.uk" + href;
                return (text, full);
            })
            .Where(d => !string.IsNullOrWhiteSpace(d.text))
            .DistinctBy(d => d.full)
            .ToList() ?? new();

        // ── Full page text ────────────────────────────────────────────────────
        var main = doc.DocumentNode.SelectSingleNode("//main")
                ?? doc.DocumentNode.SelectSingleNode("//article")
                ?? doc.DocumentNode.SelectSingleNode("//body");
        var pageText = main == null ? "" : CleanText(main.InnerText);

        var sb = new StringBuilder();
        sb.AppendLine("BRADFORD COUNCIL — SCHOOL TRANSPORT & TRAVEL ASSISTANCE");
        sb.AppendLine($"Source: {TransportUrl}");
        sb.AppendLine();
        sb.AppendLine(TruncateText(pageText, 3800));
        sb.AppendLine();

        if (docLinks.Count > 0)
        {
            sb.AppendLine("DOWNLOADABLE FORMS & POLICY DOCUMENTS:");
            foreach (var (text, url) in docLinks)
                sb.AppendLine($"- {text}: {url}");
        }

        sb.AppendLine();
        sb.AppendLine("CONTACT — Travel Assistance Service:");
        sb.AppendLine("  Phone: 01274 439450");
        sb.AppendLine("  Email: schooltransport@bradford.gov.uk");
        sb.AppendLine($"  Apply online: {TransportUrl}");
        sb.AppendLine();
        sb.AppendLine($"OFFICIAL_BRADFORD_LINK: [School transport and travel assistance]({TransportUrl})");
        sb.AppendLine("FOLLOW_UP_SUGGESTION: Would you like help finding schools near your postcode, or information about school admissions?");

        return sb.ToString();
    }

    private static string TransportFallback() =>
        $"""
        BRADFORD SCHOOL TRANSPORT — KEY FACTS (source: {TransportUrl})

        WHO QUALIFIES:
        - Children aged 5–16 at their nearest qualifying school who live at least:
          • 2 miles away (age 5–7)
          • 3 miles away (age 8–16)
        - Children with SEN/EHCP attending their nearest suitable school
        - Routes officially deemed unsafe to walk

        APPLY BY: Friday 12 June 2026 for September 2026 start

        APPLICATION FORMS:
        - Ages 5–16: https://www.bradford.gov.uk/media/keth5ppv/application-for-travel-compulsory-school-age-5-16.docx
        - Post-16:   https://www.bradford.gov.uk/media/gwmjutma/application-for-travel-post-16.docx

        POLICY DOCUMENTS:
        - Home to school travel policy: https://www.bradford.gov.uk/media/zgzbvrrx/home-to-school-travel-and-transport-policy-for-children-of-compulsory-school-age.pdf
        - Unavailable walking routes:   https://www.bradford.gov.uk/media/2393/unvailable-walking-routes.pdf

        CONTACT:
        - Phone: 01274 439450
        - Email: schooltransport@bradford.gov.uk
        - Full details: {TransportUrl}
        """;

    // ── Education info scraper ────────────────────────────────────────────────
    private async Task<string> GetEducationInfoAsync(string topic, CancellationToken ct)
    {
        var t = topic.ToLower();
        var urls = new List<string>();

        if (t.Contains("admiss") || t.Contains("apply") || t.Contains("place") || t.Contains("start") || t.Contains("reception"))
        {
            urls.Add("https://www.bradford.gov.uk/education-and-skills/school-admissions/apply-for-a-place-at-one-of-bradford-districts-schools/");
            urls.Add("https://www.bradford.gov.uk/education-and-skills/school-admissions/about-school-admissions/");
        }
        if (t.Contains("term") || t.Contains("holiday") || t.Contains("dates") || t.Contains("closure"))
        {
            urls.Add("https://www.bradford.gov.uk/education-and-skills/school-holidays-and-term-dates/school-holidays-and-term-dates/");
            urls.Add("https://www.bradford.gov.uk/education-and-skills/school-holidays-and-term-dates/school-closures/");
        }
        if (t.Contains("meal") || t.Contains("free school") || t.Contains("lunch") || t.Contains("fsm"))
        {
            urls.Add("https://www.bradford.gov.uk/education-and-skills/school-meals/paying-for-school-meals/");
            urls.Add("https://www.bradford.gov.uk/education-and-skills/school-meals/school-meals/");
        }
        if (t.Contains("send") || t.Contains("special") || t.Contains("disability") || t.Contains("need") || t.Contains("support"))
        {
            urls.Add("https://www.bradford.gov.uk/education-and-skills/school-support-services/school-support-services/");
            urls.Add("https://www.bradford.gov.uk/education-and-skills/school-support-services/social-communication-interaction-and-learning-scil-team/");
        }
        if (t.Contains("exclus") || t.Contains("expel") || t.Contains("suspend"))
            urls.Add("https://www.bradford.gov.uk/education-and-skills/school-support-services/exclusion-from-school/");
        if (t.Contains("travel") || t.Contains("transport") || t.Contains("bus"))
            return await GetSchoolTransportAsync(topic, ct); // full scrape with docs
        if (t.Contains("uniform"))
            urls.Add("https://www.bradford.gov.uk/education-and-skills/school-support-services/school-support-services/");

        // Fallback: general education hub
        if (urls.Count == 0)
            urls.Add("https://www.bradford.gov.uk/education-and-skills/education-and-skills/");

        var tasks = urls.Distinct().Take(3).Select(u => FetchPageTextAsync(u, ct)).ToList();
        await Task.WhenAll(tasks);
        var text = string.Join("\n\n---\n\n", tasks.Select(t2 => t2.Result).Where(r => r?.Length > 100));

        var primaryUrl  = urls.FirstOrDefault() ?? "https://www.bradford.gov.uk/education-and-skills/education-and-skills/";
        var followUpMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "admiss",   "Would you like to find schools near your postcode, or learn about free school meals eligibility?" },
            { "term",     "Would you like to know about school admissions or find schools near your postcode?" },
            { "meal",     "Would you like to know about school admissions or find your nearest schools?" },
            { "send",     "Would you like help finding schools with SEND provision near your postcode?" },
            { "exclus",   "Would you like information about school support services or admissions?" },
            { "uniform",  "Would you like to know about free school meals or school admissions?" },
        };

        var followUp = "Would you like to find schools near your postcode or learn more about Bradford education services?";
        foreach (var kv in followUpMap)
            if (t.Contains(kv.Key)) { followUp = kv.Value; break; }

        if (string.IsNullOrEmpty(text))
            return $"For education information visit: https://www.bradford.gov.uk/education-and-skills/\n\nOFFICIAL_BRADFORD_LINK: [Bradford Council education and skills](https://www.bradford.gov.uk/education-and-skills/education-and-skills/)\nFOLLOW_UP_SUGGESTION: {followUp}";

        return text + $"\n\nOFFICIAL_BRADFORD_LINK: [Bradford Council — {topic}]({primaryUrl})\nFOLLOW_UP_SUGGESTION: {followUp}";
    }

    // ── Text helpers ──────────────────────────────────────────────────────────
    private static string InferPhase(string text)
    {
        var t = text.ToLower();
        if (t.Contains("nursery") || t.Contains("pre-school")) return "Nursery";
        if (t.Contains("infant"))                               return "Primary";
        if (t.Contains("junior"))                               return "Primary";
        if (t.Contains("primary"))                              return "Primary";
        if (t.Contains("secondary") || t.Contains("high school") || t.Contains("grammar")) return "Secondary";
        if (t.Contains("sixth form") || t.Contains("16+") || t.Contains("college"))        return "Sixth Form";
        if (t.Contains("all-through") || t.Contains("all through"))                        return "All-through";
        if (t.Contains("special"))                              return "Special";
        return "";
    }

    private static string ExtractFirstHeading(HtmlNode node)
    {
        var h = node.SelectSingleNode(".//h1 | .//h2 | .//h3 | .//h4 | .//strong");
        return h != null ? CleanText(h.InnerText) : "";
    }

    private static string ExtractAddress(string text)
    {
        // Look for postcode pattern in the text
        var match = Regex.Match(text, @"[A-Z0-9 ,]+,\s*BD\d{1,2}\s*\d[A-Z]{2}", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim() : "";
    }

    private static string NormaliseOfstedLabel(string raw) => raw.Trim().ToLower() switch
    {
        "1" or "outstanding"           => "Outstanding",
        "2" or "good"                  => "Good",
        "3" or "requires improvement"  => "Requires Improvement",
        "4" or "inadequate"            => "Inadequate",
        "0" or "not yet inspected" or "" or "n/a" => "",
        var s when s.Contains("outstanding")  => "Outstanding",
        var s when s.Contains("good")         => "Good",
        var s when s.Contains("requires")     => "Requires Improvement",
        var s when s.Contains("inadequate")   => "Inadequate",
        var s                                  => s
    };

    // ── Infer age range from school phase / name ──────────────────────────────
    private static string InferAgeRange(string phase, string name)
    {
        var p = (phase + " " + name).ToLower();
        if (p.Contains("nursery") || p.Contains("pre-school")) return "Ages 3–4";
        if (p.Contains("infant"))  return "Ages 4–7";
        if (p.Contains("junior"))  return "Ages 7–11";
        if (p.Contains("all-through") || p.Contains("all through")) return "Ages 3–18";
        if (p.Contains("primary") || p.Contains("first school"))    return "Ages 4–11";
        if (p.Contains("middle"))  return "Ages 9–13";
        if (p.Contains("secondary") || p.Contains("high school") || p.Contains("grammar")) return "Ages 11–16";
        if (p.Contains("sixth form") || p.Contains("16+") || p.Contains("post-16")) return "Ages 16–18";
        if (p.Contains("college")) return "Ages 16–18";
        if (p.Contains("special")) return "Ages 3–19";
        return "";
    }

    // ── Fetch Ofsted rating from reports.ofsted.gov.uk search ────────────────
    // Searches by school name and parses "Rating: Good/Outstanding/..." from results HTML.
    private async Task<(string rating, string ofstedUrn)> FetchOfstedRatingAsync(string schoolName, CancellationToken ct)
    {
        try
        {
            var q    = Uri.EscapeDataString(schoolName + " bradford");
            var url  = $"https://reports.ofsted.gov.uk/search?q={q}&level_1_types=1";
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible)");
            var html = await (await client.GetAsync(url, ct)).Content.ReadAsStringAsync(ct);

            // Strip tags
            html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[^>]*>.*?</style>",   "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var text = Regex.Replace(html, @"<[^>]+>", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();

            // Find the school entry in results (match by name proximity)
            var nameIdx = text.IndexOf(schoolName, StringComparison.OrdinalIgnoreCase);
            if (nameIdx < 0) return ("", "");

            var snippet = text.Substring(nameIdx, Math.Min(400, text.Length - nameIdx));

            // Extract rating
            var ratingMatch = Regex.Match(snippet, @"Rating\s*:\s*(Outstanding|Good|Requires\s+Improvement|Inadequate)",
                RegexOptions.IgnoreCase);
            var rating = ratingMatch.Success
                ? System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                    ratingMatch.Groups[1].Value.Trim().ToLower())
                : "";

            // Extract Ofsted URN
            var urnMatch = Regex.Match(snippet, @"URN\s*:\s*(\d+)", RegexOptions.IgnoreCase);
            var urn      = urnMatch.Success ? urnMatch.Groups[1].Value : "";

            _logger.LogInformation("Ofsted search: '{N}' → rating='{R}' urn='{U}'", schoolName, rating, urn);
            return (rating, urn);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("FetchOfstedRating failed: {M}", ex.Message);
            return ("", "");
        }
    }

    // ── Bradford standard term dates (maintained schools 2025/26 & 2026/27) ──
    // Academy schools set their own — noted on card. Source: Bradford Council.
    private static List<TermPeriod> BuildBradfordTermDates()
    {
        var today = DateTime.Today;
        var periods = new List<TermPeriod>
        {
            // ── 2025/26 ─────────────────────────────────────────────────────────
            new() { Label="Autumn Term 1",       Dates="3 Sep – 24 Oct 2025",       Type="term" },
            new() { Label="Autumn Half Term",    Dates="27–31 Oct 2025",             Type="halfterm" },
            new() { Label="Autumn Term 2",       Dates="3 Nov – 19 Dec 2025",        Type="term" },
            new() { Label="Christmas Holiday",   Dates="22 Dec 2025 – 2 Jan 2026",   Type="christmas" },
            new() { Label="Spring Term 1",       Dates="5 Jan – 13 Feb 2026",        Type="term" },
            new() { Label="Spring Half Term",    Dates="16–20 Feb 2026",             Type="halfterm" },
            new() { Label="Spring Term 2",       Dates="23 Feb – 1 Apr 2026",        Type="term" },
            new() { Label="Easter Holiday",      Dates="2–17 Apr 2026",              Type="easter" },
            new() { Label="Summer Term 1",       Dates="20 Apr – 22 May 2026",       Type="term" },
            new() { Label="Summer Half Term",    Dates="25–29 May 2026",             Type="halfterm" },
            new() { Label="Summer Term 2",       Dates="1 Jun – 17 Jul 2026",        Type="term" },
            new() { Label="Summer Holiday",      Dates="18 Jul – 2 Sep 2026",        Type="summer" },
            // ── 2026/27 ─────────────────────────────────────────────────────────
            new() { Label="Autumn Term 1",       Dates="3 Sep – 23 Oct 2026",        Type="term" },
            new() { Label="Autumn Half Term",    Dates="26–30 Oct 2026",             Type="halfterm" },
            new() { Label="Autumn Term 2",       Dates="2 Nov – 18 Dec 2026",        Type="term" },
            new() { Label="Christmas Holiday",   Dates="21 Dec 2026 – 4 Jan 2027",   Type="christmas" },
            new() { Label="Spring Term 1",       Dates="5 Jan – 12 Feb 2027",        Type="term" },
            new() { Label="Spring Half Term",    Dates="15–19 Feb 2027",             Type="halfterm" },
            new() { Label="Spring Term 2",       Dates="22 Feb – 26 Mar 2027",       Type="term" },
            new() { Label="Easter Holiday",      Dates="29 Mar – 13 Apr 2027",       Type="easter" },
            new() { Label="Summer Term 1",       Dates="14 Apr – 21 May 2027",       Type="term" },
            new() { Label="Summer Half Term",    Dates="24–28 May 2027",             Type="halfterm" },
            new() { Label="Summer Term 2",       Dates="31 May – 16 Jul 2027",       Type="term" },
            new() { Label="Summer Holiday",      Dates="from 17 Jul 2027",           Type="summer" },
        };

        // Mark periods as past based on today's date
        foreach (var p in periods)
        {
            var endMatch = Regex.Match(p.Dates, @"(\d{1,2})\s+(\w+)\s+(\d{4})$");
            if (!endMatch.Success) endMatch = Regex.Match(p.Dates, @"(\d{1,2})\s+(\w+)\s+(\d{4})");
            if (endMatch.Success &&
                DateTime.TryParse($"{endMatch.Groups[1].Value} {endMatch.Groups[2].Value} {endMatch.Groups[3].Value}", out var end))
                p.Past = end < today;
        }

        return periods;
    }

    // ── Fetch school website: term dates URL + facility list ─────────────────
    private async Task<(string termUrl, List<string> facilities)> FetchSchoolWebsiteDataAsync(
        string websiteBase, CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible)");
            var html = await (await client.GetAsync(websiteBase, ct)).Content.ReadAsStringAsync(ct);
            var doc  = new HtmlDocument();
            doc.LoadHtml(html);

            // ── Term dates URL ────────────────────────────────────────────────
            var termUrl    = string.Empty;
            var candidates = doc.DocumentNode.SelectNodes("//a[@href]");
            if (candidates != null)
            {
                foreach (var a in candidates)
                {
                    var href  = a.GetAttributeValue("href", "");
                    var text  = CleanText(a.InnerText).ToLower();
                    var lhref = href.ToLower();
                    if ((text.Contains("term") || text.Contains("holiday") || text.Contains("date") || text.Contains("calendar")) &&
                        (lhref.Contains("term") || lhref.Contains("holiday") || lhref.Contains("date") || lhref.Contains("calendar")))
                    {
                        termUrl = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            ? href
                            : Uri.TryCreate(new Uri(websiteBase), href, out var abs) ? abs.ToString() : "";
                        if (!string.IsNullOrEmpty(termUrl)) break;
                    }
                }
            }

            // ── Facilities: scan all link text + nav text for known keywords ──
            var allText = string.Join(" ", doc.DocumentNode.SelectNodes("//a | //li | //span | //p")
                ?.Select(n => CleanText(n.InnerText).ToLower())
                .Where(t => t.Length > 2)
                ?? Enumerable.Empty<string>());

            var facilityMap = new[]
            {
                (new[]{"breakfast club","breakfast"},            "🥣 Breakfast Club"),
                (new[]{"after school club","before/after","after-school","wraparound","care club"}, "🌙 After-School Club"),
                (new[]{"library","book corner"},                 "📚 Library"),
                (new[]{"sports hall","gymnasium","sports facil"},"⚽ Sports Hall"),
                (new[]{"swimming pool","swimming"},              "🏊 Swimming Pool"),
                (new[]{"computer suite","ict suite","computing room"}, "💻 Computer Suite"),
                (new[]{"nursery","early years"},                 "🌱 Nursery / EYFS"),
                (new[]{"music room","music studio"},             "🎵 Music Room"),
                (new[]{"forest school","outdoor classroom","forest garden"}, "🌳 Forest School"),
                (new[]{"canteen","dining hall","school kitchen"}, "🍽️ School Dining"),
                (new[]{"arabic","french","spanish","mandarin"},  "🌍 Languages"),
                (new[]{"send","special educational needs","special needs support"}, "♿ SEND Provision"),
            };

            var found = new List<string>();
            foreach (var (keywords, label) in facilityMap)
                if (keywords.Any(k => allText.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    found.Add(label);

            return (termUrl, found);
        }
        catch
        {
            return (string.Empty, new List<string>());
        }
    }

    // ── Geo helpers ───────────────────────────────────────────────────────────
    private async Task<(double lat, double lon)> GetLatLonAsync(string postcode, CancellationToken ct)
    {
        try
        {
            var json = await FetchHtmlAsync(
                $"https://api.postcodes.io/postcodes/{Uri.EscapeDataString(postcode)}", ct);
            if (json == null) return (0, 0);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("result", out var r))
                return (r.GetProperty("latitude").GetDouble(), r.GetProperty("longitude").GetDouble());
        }
        catch { }
        return (0, 0);
    }

    private static double HaversineDistanceMi(double lat1, double lon1, double lat2, double lon2)
    {
        if (lat2 == 0 && lon2 == 0) return 999;
        const double R = 3958.8;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
