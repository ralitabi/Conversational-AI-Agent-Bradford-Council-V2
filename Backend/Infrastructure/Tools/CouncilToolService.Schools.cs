using System.Net;
using System.Text;
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
        var schools = await ScrapeBradfordSchoolsAsync(postcode, schoolName: null, phase, ct);

        if (schools.Count == 0)
        {
            return $"No schools found near {postcode}. " +
                   "Check Bradford's school finder at https://www.bradford.gov.uk/education-and-skills/find-a-school/schools-finder/";
        }

        var options = schools.Select((s, i) => new SchoolOption
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
        sb.AppendLine($"SCHOOL_INSTRUCTION: Found {options.Count} schools near {postcode}. The UI shows the list. " +
                      "Briefly list name, phase, Ofsted rating. End: \"Tap a school for full details or ask which is best.\"");
        return sb.ToString();
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

            _logger.LogInformation("SchoolFinder: found {N} schools", results.Count);
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

    // ── Parse Bradford school finder results HTML ────────────────────────────
    private static List<SchoolData> ParseSchoolResults(string html, string? postcode)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var schools = new List<SchoolData>();

        // Strategy 1: Look for results table (GridView output)
        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables != null)
        {
            foreach (var table in tables)
            {
                var rows = table.SelectNodes(".//tr");
                if (rows == null || rows.Count < 2) continue;

                // Detect header row to understand columns
                var headers = rows[0].SelectNodes(".//th | .//td")
                    ?.Select(h => CleanText(h.InnerText).ToLower())
                    .ToList() ?? new();

                if (!headers.Any(h => h.Contains("school") || h.Contains("name") || h.Contains("estab")))
                    continue;

                int nameIdx    = headers.FindIndex(h => h.Contains("school") || h.Contains("name"));
                int addrIdx    = headers.FindIndex(h => h.Contains("address") || h.Contains("postcode"));
                int typeIdx    = headers.FindIndex(h => h.Contains("type") || h.Contains("phase"));
                int ofstedIdx  = headers.FindIndex(h => h.Contains("ofsted") || h.Contains("rating"));
                int distIdx    = headers.FindIndex(h => h.Contains("distance") || h.Contains("dist") || h.Contains("mile"));

                for (int r = 1; r < rows.Count; r++)
                {
                    var cells = rows[r].SelectNodes(".//td");
                    if (cells == null || cells.Count == 0) continue;

                    var school = new SchoolData();

                    if (nameIdx >= 0 && nameIdx < cells.Count)
                    {
                        var nameCell = cells[nameIdx];
                        school.Name = CleanText(nameCell.InnerText);
                        var link = nameCell.SelectSingleNode(".//a");
                        if (link != null)
                        {
                            var href = link.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                                school.DetailsUrl = href.StartsWith("http")
                                    ? href
                                    : $"https://bso.bradford.gov.uk/council/schools/{href.TrimStart('/')}";
                        }
                    }
                    else if (cells.Count > 0)
                    {
                        school.Name = CleanText(cells[0].InnerText);
                        var link = cells[0].SelectSingleNode(".//a");
                        if (link != null)
                        {
                            var href = link.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                                school.DetailsUrl = href.StartsWith("http")
                                    ? href
                                    : $"https://bso.bradford.gov.uk/council/schools/{href.TrimStart('/')}";
                        }
                    }

                    if (addrIdx >= 0 && addrIdx < cells.Count)
                        school.Address = CleanText(cells[addrIdx].InnerText);
                    if (typeIdx >= 0 && typeIdx < cells.Count)
                        school.Type = CleanText(cells[typeIdx].InnerText);
                    if (ofstedIdx >= 0 && ofstedIdx < cells.Count)
                        school.OfstedRating = NormaliseOfstedLabel(CleanText(cells[ofstedIdx].InnerText));
                    if (distIdx >= 0 && distIdx < cells.Count)
                        school.Distance = CleanText(cells[distIdx].InnerText);

                    // Extract phase from type if combined
                    if (string.IsNullOrEmpty(school.Phase))
                        school.Phase = InferPhase(school.Type + " " + school.Name);

                    if (!string.IsNullOrEmpty(school.Name) && school.Name.Length > 3)
                        schools.Add(school);
                }

                if (schools.Count > 0) break;
            }
        }

        // Strategy 2: Look for repeated div/list patterns
        if (schools.Count == 0)
        {
            var items = doc.DocumentNode.SelectNodes(
                "//*[contains(@class,'school') or contains(@class,'result') or contains(@id,'school')]");
            if (items != null)
            {
                foreach (var item in items)
                {
                    var text = CleanText(item.InnerText);
                    if (text.Length < 10) continue;

                    var link  = item.SelectSingleNode(".//a");
                    var name  = link != null ? CleanText(link.InnerText) : ExtractFirstHeading(item);
                    if (string.IsNullOrEmpty(name)) continue;

                    schools.Add(new SchoolData
                    {
                        Name    = name,
                        Address = ExtractAddress(text),
                        Phase   = InferPhase(text + " " + name),
                        DetailsUrl = link?.GetAttributeValue("href", "") ?? ""
                    });
                }
            }
        }

        // Strategy 3: Text-based extraction — look for "School" / "Academy" in links
        if (schools.Count == 0)
        {
            var allLinks = doc.DocumentNode.SelectNodes(
                "//a[contains(translate(text(),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'school') or " +
                "contains(translate(text(),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'academy') or " +
                "contains(translate(text(),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'college')]");
            if (allLinks != null)
            {
                foreach (var link in allLinks.Take(20))
                {
                    var name = CleanText(link.InnerText);
                    if (name.Length < 5 || name.Length > 80) continue;
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
        }

        return schools.Where(s => !string.IsNullOrEmpty(s.Name)).DistinctBy(s => s.Name).Take(10).ToList();
    }

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
}
