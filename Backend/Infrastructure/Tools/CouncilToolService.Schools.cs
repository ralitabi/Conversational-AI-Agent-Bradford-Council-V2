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

        // 1. Get user lat/lon and all schools from BSO in parallel
        var latLonTask  = GetLatLonAsync(postcode, ct);
        var schoolsTask = ScrapeBradfordSchoolsAsync(postcode, schoolName: null, phase, ct);
        await Task.WhenAll(latLonTask, schoolsTask);

        var (userLat, userLon) = latLonTask.Result;
        var schools            = schoolsTask.Result;

        if (schools.Count == 0)
            return $"No schools found near {postcode}. " +
                   "Check Bradford's school finder at https://www.bradford.gov.uk/education-and-skills/find-a-school/schools-finder/";

        // 2. Calculate distances — BSO often omits them, so use postcodes.io bulk lookup
        if (userLat != 0)
        {
            // Collect unique school postcodes from address fields
            var schoolPostcodes = schools
                .Select(s => ExtractPostcode(s.Address))
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToList();

            Dictionary<string, (double lat, double lon)> pcMap = new();
            if (schoolPostcodes.Count > 0)
                pcMap = await BulkLookupPostcodesAsync(schoolPostcodes!, ct);

            foreach (var s in schools)
            {
                var pc = ExtractPostcode(s.Address);
                if (pc != null && pcMap.TryGetValue(pc, out var ll))
                {
                    var mi = HaversineDistanceMi(userLat, userLon, ll.lat, ll.lon);
                    s.Distance = $"{mi:F1} mi";
                }
                else if (string.IsNullOrEmpty(s.Distance))
                {
                    s.Distance = "";
                }
            }
        }

        // 3. Sort by distance and apply phase filter
        var withCalcDist = schools.Count(s => !string.IsNullOrEmpty(s.Distance));
        _logger.LogInformation("SchoolDist: {D}/{N} schools have distance after enrichment. Top 3: [{T}]",
            withCalcDist, schools.Count,
            string.Join("; ", schools.Where(s => !string.IsNullOrEmpty(s.Distance))
                                     .OrderBy(s => ParseDistanceValue(s.Distance))
                                     .Take(3).Select(s => $"{s.Name}={s.Distance}")));

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

    // ── School details (name search) ─────────────────────────────────────────
    private async Task<string> GetSchoolDetailsAsync(string nameOrUrn, CancellationToken ct)
    {
        var schools = await ScrapeBradfordSchoolsAsync(postcode: null, schoolName: nameOrUrn, phase: null, ct);

        var school = schools.FirstOrDefault(s =>
                         s.Urn.Equals(nameOrUrn, StringComparison.OrdinalIgnoreCase) ||
                         s.Name.Equals(nameOrUrn, StringComparison.OrdinalIgnoreCase))
                  ?? schools.OrderByDescending(s => ScoreAddressMatch(s.Name, nameOrUrn)).FirstOrDefault();

        if (school == null)
            return $"Could not find details for '{nameOrUrn}'. " +
                   "Try searching on https://bso.bradford.gov.uk/council/schools/schoolfinder.aspx";

        var card = new SchoolCard
        {
            Name          = school.Name,
            Address       = school.Address,
            Phone         = school.Phone,
            Website       = school.Website,
            Phase         = school.Phase,
            Type          = school.Type,
            OfstedRating  = school.OfstedRating,
            OfstedDate    = school.OfstedDate,
            Pupils        = school.Pupils,
            AgeRange      = school.AgeRange,
            Urn           = school.Urn,
            AdmissionsUrl = "https://www.bradford.gov.uk/education-and-skills/schools/school-admissions/",
            OfstedUrl     = string.IsNullOrEmpty(school.Urn)
                ? "https://reports.ofsted.gov.uk/"
                : $"https://reports.ofsted.gov.uk/provider/21/{school.Urn}"
        };

        var sb = new StringBuilder();
        sb.AppendLine("[[SCHOOL_CARD]]");
        sb.AppendLine(JsonSerializer.Serialize(card));
        sb.AppendLine("[[/SCHOOL_CARD]]");
        sb.AppendLine();
        sb.AppendLine($"SCHOOL_DETAIL_INSTRUCTION: Details for {school.Name}. " +
                      $"Phase: {school.Phase}. Type: {school.Type}. Ofsted: {school.OfstedRating}. " +
                      $"Pupils: {school.Pupils}. Age: {school.AgeRange}. " +
                      "Admissions: Bradford Council manages community school places; academies handle their own. " +
                      "Comparison tip: Outstanding > Good > Requires Improvement. Always recommend a school visit.");
        return sb.ToString();
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
    // The BSO site is ASP.NET GridView — school name is ALWAYS the linked cell.
    // Column order varies, so we detect by content not position.
    private List<SchoolData> ParseSchoolResults(string html, string? postcode)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var schools = new List<SchoolData>();

        // ── Strategy 1: GridView table ──────────────────────────────────────
        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables != null)
        {
            foreach (var table in tables)
            {
                var rows = table.SelectNodes(".//tr");
                if (rows == null || rows.Count < 2) continue;

                // Read header row (may use <th> or first <td> row)
                var headerCells = rows[0].SelectNodes(".//th | .//td");
                if (headerCells == null) continue;
                var headers = headerCells.Select(h => CleanText(h.InnerText).ToLower()).ToList();

                // Log headers so we can see what BSO returns
                _logger.LogInformation("BSO table headers: [{H}]", string.Join(" | ", headers));

                // Must look like a school table
                if (!headers.Any(h => h.Contains("school") || h.Contains("name") ||
                                      h.Contains("estab") || h.Contains("distance")))
                    continue;

                // Pre-map header indices (best-effort)
                int hName   = headers.FindIndex(h => h.Contains("school") || h.Contains("name") || h.Contains("estab"));
                int hAddr   = headers.FindIndex(h => h.Contains("address") || h.Contains("postcode") || h.Contains("town"));
                int hType   = headers.FindIndex(h => h.Contains("type") || h.Contains("category"));
                int hPhase  = headers.FindIndex(h => h.Contains("phase") || h.Contains("primary") || h.Contains("secondary"));
                int hOfsted = headers.FindIndex(h => h.Contains("ofsted") || h.Contains("inspection") || h.Contains("rating"));
                int hDist   = headers.FindIndex(h => h.Contains("distance") || h.Contains("dist") || h.Contains("mile"));
                int hAge    = headers.FindIndex(h => h.Contains("age") || h.Contains("range"));

                for (int r = 1; r < rows.Count; r++)
                {
                    var cells = rows[r].SelectNodes(".//td");
                    if (cells == null || cells.Count < 2) continue;

                    // Log first data row so we can see actual cell values
                    if (r == 1)
                        _logger.LogInformation("BSO row1 cells: [{C}]",
                            string.Join(" | ", cells.Select(c => CleanText(c.InnerText)).Take(8)));

                    var school = new SchoolData();

                    // PRIMARY RULE: the cell containing an <a> tag IS the school name
                    int linkCellIdx = -1;
                    for (int c = 0; c < cells.Count; c++)
                    {
                        var a = cells[c].SelectSingleNode(".//a");
                        if (a != null)
                        {
                            school.Name = CleanText(a.InnerText);
                            var href = a.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                                school.DetailsUrl = href.StartsWith("http")
                                    ? href
                                    : $"https://bso.bradford.gov.uk/council/schools/{href.TrimStart('/')}";
                            linkCellIdx = c;
                            break;
                        }
                    }
                    // Fallback: use header-mapped name column
                    if (string.IsNullOrEmpty(school.Name) && hName >= 0 && hName < cells.Count)
                        school.Name = CleanText(cells[hName].InnerText);
                    if (string.IsNullOrEmpty(school.Name)) continue;

                    // Fill other fields from header-mapped columns first
                    if (hAddr   >= 0 && hAddr   < cells.Count) school.Address      = CleanText(cells[hAddr].InnerText);
                    if (hType   >= 0 && hType   < cells.Count) school.Type         = CleanText(cells[hType].InnerText);
                    if (hPhase  >= 0 && hPhase  < cells.Count) school.Phase        = CleanText(cells[hPhase].InnerText);
                    if (hOfsted >= 0 && hOfsted < cells.Count) school.OfstedRating = NormaliseOfstedLabel(CleanText(cells[hOfsted].InnerText));
                    if (hAge    >= 0 && hAge    < cells.Count) school.AgeRange     = CleanText(cells[hAge].InnerText);
                    if (hDist   >= 0 && hDist   < cells.Count) school.Distance     = FormatDistance(CleanText(cells[hDist].InnerText));

                    // Content-scan remaining cells for any fields still missing
                    for (int c = 0; c < cells.Count; c++)
                    {
                        if (c == linkCellIdx) continue;
                        var t = CleanText(cells[c].InnerText);
                        if (string.IsNullOrEmpty(t)) continue;

                        // Distance: a bare decimal like "0.39" or "0.39 miles"
                        if (string.IsNullOrEmpty(school.Distance) &&
                            Regex.IsMatch(t, @"^\d{1,2}(\.\d{1,2})?(\s*miles?)?$", RegexOptions.IgnoreCase))
                        {
                            school.Distance = FormatDistance(t);
                            continue;
                        }
                        // Address: contains Bradford postcode
                        if (string.IsNullOrEmpty(school.Address) &&
                            Regex.IsMatch(t, @"BD\d{1,2}\s*\d[A-Z]{2}", RegexOptions.IgnoreCase))
                        {
                            school.Address = t;
                            continue;
                        }
                        // Ofsted: short text matching known ratings
                        if (string.IsNullOrEmpty(school.OfstedRating) && t.Length < 40)
                        {
                            var norm = NormaliseOfstedLabel(t);
                            if (!string.IsNullOrEmpty(norm)) { school.OfstedRating = norm; continue; }
                        }
                        // Type: long-ish label, doesn't look like a number or address
                        if (string.IsNullOrEmpty(school.Type) && t.Length is > 4 and < 60 &&
                            !Regex.IsMatch(t, @"^\d") &&
                            (t.Contains("Academy") || t.Contains("Community") || t.Contains("School") ||
                             t.Contains("Foundation") || t.Contains("Voluntary") || t.Contains("Free")))
                        {
                            school.Type = t;
                        }
                        // Phase
                        if (string.IsNullOrEmpty(school.Phase))
                        {
                            var p = InferPhaseFromLabel(t);
                            if (!string.IsNullOrEmpty(p)) school.Phase = p;
                        }
                        // Age range
                        if (string.IsNullOrEmpty(school.AgeRange) &&
                            Regex.IsMatch(t, @"\d+\s*[-–]\s*\d+"))
                            school.AgeRange = t;
                    }

                    // Final fallback phase from name
                    if (string.IsNullOrEmpty(school.Phase))
                        school.Phase = InferPhase(school.Name);

                    if (school.Name.Length > 3)
                        schools.Add(school);
                }

                if (schools.Count > 0) break;
            }
        }

        // ── Strategy 2: div/class patterns ─────────────────────────────────
        if (schools.Count == 0)
        {
            var items = doc.DocumentNode.SelectNodes(
                "//*[contains(@class,'school') or contains(@class,'result') or contains(@id,'school')]");
            if (items != null)
                foreach (var item in items)
                {
                    var link = item.SelectSingleNode(".//a");
                    var name = link != null ? CleanText(link.InnerText) : ExtractFirstHeading(item);
                    if (string.IsNullOrEmpty(name) || name.Length < 5) continue;
                    var text = CleanText(item.InnerText);
                    schools.Add(new SchoolData
                    {
                        Name       = name,
                        Address    = ExtractAddress(text),
                        Phase      = InferPhase(name),
                        DetailsUrl = link?.GetAttributeValue("href", "") ?? ""
                    });
                }
        }

        // ── Strategy 3: any link with school/academy in text ───────────────
        if (schools.Count == 0)
        {
            var allLinks = doc.DocumentNode.SelectNodes("//a");
            if (allLinks != null)
                foreach (var link in allLinks)
                {
                    var name = CleanText(link.InnerText);
                    if (name.Length < 5 || name.Length > 80) continue;
                    var nl = name.ToLower();
                    if (!nl.Contains("school") && !nl.Contains("academy") &&
                        !nl.Contains("college") && !nl.Contains("nursery")) continue;
                    var href = link.GetAttributeValue("href", "");
                    schools.Add(new SchoolData
                    {
                        Name       = name,
                        Phase      = InferPhase(name),
                        DetailsUrl = href.StartsWith("http") ? href
                            : string.IsNullOrEmpty(href) ? ""
                            : $"https://bso.bradford.gov.uk/council/schools/{href.TrimStart('/')}"
                    });
                }
        }

        // Sort by distance (closest first), remove duplicates, no arbitrary cap
        return schools
            .Where(s => !string.IsNullOrEmpty(s.Name))
            .DistinctBy(s => s.Name)
            .OrderBy(s => ParseDistanceValue(s.Distance))
            .ToList();
    }

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

    // ── Education info scraper ────────────────────────────────────────────────
    private async Task<string> GetEducationInfoAsync(string topic, CancellationToken ct)
    {
        var urls = new List<string>
        {
            "https://www.bradford.gov.uk/education-and-skills/education-and-skills/",
            "https://www.bradford.gov.uk/education-and-skills/schools/school-admissions/",
        };

        var t = topic.ToLower();
        if (t.Contains("send") || t.Contains("special") || t.Contains("disability") || t.Contains("need"))
            urls.Add("https://www.bradford.gov.uk/education-and-skills/special-educational-needs-and-disability-send/");
        if (t.Contains("meal") || t.Contains("free school"))
            urls.Add("https://www.bradford.gov.uk/education-and-skills/free-school-meals/");
        if (t.Contains("start") || t.Contains("age") || t.Contains("reception") || t.Contains("when"))
            urls.Add("https://www.bradford.gov.uk/education-and-skills/schools/starting-school/");
        if (t.Contains("term") || t.Contains("holiday") || t.Contains("dates"))
            urls.Add("https://www.bradford.gov.uk/education-and-skills/schools/term-dates/");
        if (t.Contains("uniform"))
            urls.Add("https://www.bradford.gov.uk/education-and-skills/schools/school-uniforms/");
        if (t.Contains("exclus") || t.Contains("expel") || t.Contains("suspend"))
            urls.Add("https://www.bradford.gov.uk/education-and-skills/schools/exclusions/");

        var tasks = urls.Distinct().Take(3).Select(u => FetchPageTextAsync(u, ct)).ToList();
        await Task.WhenAll(tasks);
        var text = string.Join("\n\n---\n\n", tasks.Select(t2 => t2.Result).Where(r => !string.IsNullOrEmpty(r)));
        return string.IsNullOrEmpty(text)
            ? $"Education info: https://www.bradford.gov.uk/education-and-skills/"
            : text;
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
