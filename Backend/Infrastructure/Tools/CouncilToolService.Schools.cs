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
    public string Urn         { get; set; } = "";
    public string Name        { get; set; } = "";
    public string Address     { get; set; } = "";
    public string Phone       { get; set; } = "";
    public string Website     { get; set; } = "";
    public string Phase       { get; set; } = "";
    public string Type        { get; set; } = "";
    public string OfstedRating{ get; set; } = "";
    public string OfstedDate  { get; set; } = "";
    public string Pupils      { get; set; } = "";
    public string AgeRange    { get; set; } = "";
    public double Lat         { get; set; }
    public double Lon         { get; set; }
    public double DistanceMiles { get; set; }
}

public partial class CouncilToolService
{
    // ── School finder ─────────────────────────────────────────────────────────
    private async Task<string> FindSchoolsNearPostcodeAsync(string postcode, string phase, CancellationToken ct)
    {
        var (lat, lon) = await GetLatLonAsync(postcode, ct);
        var all = await FetchBradfordSchoolsAsync(ct);

        // Phase filter
        var filtered = string.IsNullOrWhiteSpace(phase)
            ? all
            : all.Where(s => s.Phase.Contains(phase, StringComparison.OrdinalIgnoreCase) ||
                             phase.Contains(s.Phase, StringComparison.OrdinalIgnoreCase)).ToList();

        // Distance + sort
        if (lat != 0)
        {
            foreach (var s in filtered)
                s.DistanceMiles = HaversineDistanceMi(lat, lon, s.Lat, s.Lon);
            filtered = filtered
                .Where(s => s.DistanceMiles is > 0 and < 5)
                .OrderBy(s => s.DistanceMiles)
                .Take(8)
                .ToList();
        }
        else
        {
            filtered = filtered.Take(8).ToList();
        }

        if (filtered.Count == 0)
        {
            return $"No {(string.IsNullOrWhiteSpace(phase) ? "" : phase.ToLower() + " ")}schools found near {postcode}. " +
                   "Try the Bradford school finder at https://www.bradford.gov.uk/education-and-skills/schools/school-admissions/";
        }

        var options = filtered.Select((s, i) => new SchoolOption
        {
            Number       = i + 1,
            Name         = s.Name,
            Address      = s.Address,
            Phase        = s.Phase,
            Type         = s.Type,
            OfstedRating = NormaliseOfstedLabel(s.OfstedRating),
            Distance     = s.DistanceMiles > 0 ? $"{s.DistanceMiles:F1} mi" : "",
            Urn          = s.Urn,
            Website      = s.Website,
            Phone        = s.Phone
        }).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("[[SCHOOL_LIST]]");
        sb.AppendLine(JsonSerializer.Serialize(options));
        sb.AppendLine("[[/SCHOOL_LIST]]");
        sb.AppendLine();
        sb.AppendLine($"SCHOOL_INSTRUCTION: Found {options.Count} schools near {postcode}. The UI shows the list. Summarise briefly — name, phase, Ofsted rating, distance. End with: \"Tap a school or tell me its name for full details, admissions info, and a comparison.\"");
        return sb.ToString();
    }

    private async Task<string> GetSchoolDetailsAsync(string nameOrUrn, CancellationToken ct)
    {
        var all = await FetchBradfordSchoolsAsync(ct);

        var school = all.FirstOrDefault(s =>
                         s.Urn.Equals(nameOrUrn, StringComparison.OrdinalIgnoreCase) ||
                         s.Name.Equals(nameOrUrn, StringComparison.OrdinalIgnoreCase))
                  ?? all.OrderByDescending(s => ScoreAddressMatch(s.Name, nameOrUrn)).FirstOrDefault();

        if (school == null)
            return $"Could not find school details for '{nameOrUrn}'. Try searching by full name.";

        var card = new SchoolCard
        {
            Name         = school.Name,
            Address      = school.Address,
            Phone        = school.Phone,
            Website      = school.Website,
            Phase        = school.Phase,
            Type         = school.Type,
            OfstedRating = NormaliseOfstedLabel(school.OfstedRating),
            OfstedDate   = school.OfstedDate,
            Pupils       = school.Pupils,
            AgeRange     = school.AgeRange,
            Urn          = school.Urn,
            AdmissionsUrl = "https://www.bradford.gov.uk/education-and-skills/schools/school-admissions/",
            OfstedUrl    = string.IsNullOrEmpty(school.Urn)
                ? "https://reports.ofsted.gov.uk/"
                : $"https://reports.ofsted.gov.uk/provider/21/{school.Urn}"
        };

        var sb = new StringBuilder();
        sb.AppendLine("[[SCHOOL_CARD]]");
        sb.AppendLine(JsonSerializer.Serialize(card));
        sb.AppendLine("[[/SCHOOL_CARD]]");
        sb.AppendLine();
        sb.AppendLine($"SCHOOL_DETAIL_INSTRUCTION: Full details for {school.Name}. " +
                      $"Phase: {school.Phase}. Type: {school.Type}. " +
                      $"Ofsted: {NormaliseOfstedLabel(school.OfstedRating)} ({school.OfstedDate}). " +
                      $"Pupils: {school.Pupils}. Age range: {school.AgeRange}. " +
                      "Include: admissions via Bradford Council, what to look for in school visits (leadership, culture, SEND support, results). " +
                      "For requirements: Bradford state schools must follow the national curriculum. " +
                      "Admissions are managed by Bradford Council for community schools, by the school itself for academies.");
        return sb.ToString();
    }

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
            ? $"Education info from Bradford Council for topic '{topic}':\nhttps://www.bradford.gov.uk/education-and-skills/"
            : text;
    }

    // ── Bradford schools — GIAS fetch with 24h cache ─────────────────────────
    private static List<SchoolData>? _schoolCache;
    private static DateTime _schoolCacheExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _schoolCacheLock = new(1, 1);

    private async Task<List<SchoolData>> FetchBradfordSchoolsAsync(CancellationToken ct)
    {
        if (_schoolCache != null && DateTime.UtcNow < _schoolCacheExpiry)
            return _schoolCache;

        await _schoolCacheLock.WaitAsync(ct);
        try
        {
            if (_schoolCache != null && DateTime.UtcNow < _schoolCacheExpiry)
                return _schoolCache;

            var schools = await TryGiasApiAsync(ct) ?? new List<SchoolData>();
            _logger.LogInformation("Schools cache loaded: {N} entries", schools.Count);
            _schoolCache = schools;
            _schoolCacheExpiry = DateTime.UtcNow.AddHours(24);
            return schools;
        }
        finally
        {
            _schoolCacheLock.Release();
        }
    }

    private async Task<List<SchoolData>?> TryGiasApiAsync(CancellationToken ct)
    {
        // Bradford LA code = 380; try multiple known GIAS/Edubase endpoints
        var endpoints = new[]
        {
            "https://api.get-information-schools.service.gov.uk/Establishments/search?la=380&StatusFrom=1&StatusTo=1&format=json",
            "https://ea-edubase-api-prod.azurewebsites.net/edubase/api/v1/establishment?la=380&openOnly=true&format=json"
        };

        foreach (var url in endpoints)
        {
            try
            {
                var json = await FetchHtmlAsync(url, ct);
                if (string.IsNullOrEmpty(json)) continue;
                var s = json.TrimStart();
                if (!s.StartsWith("[") && !s.StartsWith("{")) continue;

                var schools = ParseGiasResponse(json);
                if (schools?.Count > 0)
                {
                    _logger.LogInformation("GIAS: fetched {N} Bradford schools", schools.Count);
                    return schools;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("GIAS endpoint {U} failed: {M}", url, ex.Message);
            }
        }
        return null;
    }

    private static List<SchoolData>? ParseGiasResponse(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Handle both array root and object root with Establishments/schools property
            JsonElement arr;
            if (root.ValueKind == JsonValueKind.Array)
                arr = root;
            else if (root.TryGetProperty("Establishments", out var e) && e.ValueKind == JsonValueKind.Array)
                arr = e;
            else if (root.TryGetProperty("schools", out var s) && s.ValueKind == JsonValueKind.Array)
                arr = s;
            else
                return null;

            var list = new List<SchoolData>();
            foreach (var item in arr.EnumerateArray())
            {
                var name = JStr(item, "EstablishmentName", "name", "Name");
                if (string.IsNullOrEmpty(name)) continue;

                var school = new SchoolData
                {
                    Urn          = JStr(item, "URN", "urn"),
                    Name         = name,
                    Phase        = NormalisePhase(JStr(item, "PhaseOfEducation", "phase")),
                    Type         = NormaliseType(JStr(item, "TypeOfEstablishment", "type")),
                    Phone        = JStr(item, "TelephoneNum", "phone"),
                    Website      = JStr(item, "SchoolWebsite", "website"),
                    OfstedRating = JStr(item, "OfstedRating", "ofstedRating"),
                    OfstedDate   = JStr(item, "OfstedInspectionDate", "ofstedDate"),
                    Pupils       = JStr(item, "NumberOfPupils", "pupils")
                };

                // Address
                var parts = new[] {
                    JStr(item, "Street","street"), JStr(item, "Locality","locality"),
                    JStr(item, "Town","town"),     JStr(item, "Postcode","postcode")
                }.Where(p => !string.IsNullOrEmpty(p));
                school.Address = string.Join(", ", parts);

                // Age range
                var lo = JStr(item, "StatutoryLowAge","minAge");
                var hi = JStr(item, "StatutoryHighAge","maxAge");
                if (!string.IsNullOrEmpty(lo) && !string.IsNullOrEmpty(hi))
                    school.AgeRange = $"{lo}–{hi}";

                // Lat/lon (try direct then easting/northing)
                if (item.TryGetProperty("Latitude",  out var latEl) &&
                    item.TryGetProperty("Longitude", out var lonEl))
                {
                    school.Lat = ParseDouble(latEl);
                    school.Lon = ParseDouble(lonEl);
                }
                else if (item.TryGetProperty("Easting",  out var east) &&
                         item.TryGetProperty("Northing", out var north))
                {
                    (school.Lat, school.Lon) = OsGridToLatLon(ParseDouble(east), ParseDouble(north));
                }

                list.Add(school);
            }

            return list;
        }
        catch { return null; }
    }

    // ── Lat/lon helpers ───────────────────────────────────────────────────────
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

    // Simplified OS National Grid (OSGB36) → WGS84 via Helmert
    private static (double lat, double lon) OsGridToLatLon(double E, double N)
    {
        const double a = 6377563.396, b = 6356256.910, F0 = 0.9996012717;
        const double lat0 = 49 * Math.PI / 180, lon0 = -2 * Math.PI / 180;
        const double N0 = -100000, E0 = 400000;
        double e2 = 1 - b * b / (a * a);
        double n = (a - b) / (a + b), n2 = n * n, n3 = n * n2;

        double lat = lat0, M = 0;
        for (int i = 0; i < 100; i++)
        {
            double prev = lat;
            M = b * F0 * ((1 + n + 5.0/4*n2 + 5.0/4*n3)*(lat - lat0)
                - (3*n + 3*n2 + 21.0/8*n3)*Math.Sin(lat - lat0)*Math.Cos(lat + lat0)
                + (15.0/8*n2 + 15.0/8*n3)*Math.Sin(2*(lat - lat0))*Math.Cos(2*(lat + lat0))
                - 35.0/24*n3*Math.Sin(3*(lat - lat0))*Math.Cos(3*(lat + lat0)));
            lat = (N - N0 - M) / (a * F0) + lat;
            if (Math.Abs(lat - prev) < 1e-12) break;
        }
        double nu = a * F0 / Math.Sqrt(1 - e2 * Math.Pow(Math.Sin(lat), 2));
        double rho = a * F0 * (1 - e2) / Math.Pow(1 - e2 * Math.Pow(Math.Sin(lat), 2), 1.5);
        double eta2 = nu / rho - 1;
        double tanLat = Math.Tan(lat), dE = E - E0;

        double latR = lat
            - tanLat / (2 * rho * nu) * dE * dE
            + tanLat / (24 * rho * Math.Pow(nu, 3)) * (5 + 3*tanLat*tanLat + eta2 - 9*tanLat*tanLat*eta2) * Math.Pow(dE, 4)
            - tanLat / (720 * rho * Math.Pow(nu, 5)) * (61 + 90*tanLat*tanLat + 45*Math.Pow(tanLat, 4)) * Math.Pow(dE, 6);
        double cosLat = Math.Cos(lat);
        double lonR = lon0
            + dE / (cosLat * nu)
            - Math.Pow(dE, 3) / (cosLat * 6 * Math.Pow(nu, 3)) * (nu / rho + 2*tanLat*tanLat)
            + Math.Pow(dE, 5) / (cosLat * 120 * Math.Pow(nu, 5)) * (5 + 28*tanLat*tanLat + 24*Math.Pow(tanLat, 4));
        return (latR * 180 / Math.PI, lonR * 180 / Math.PI);
    }

    // ── JSON / string helpers ─────────────────────────────────────────────────
    private static string JStr(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var v))
                return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.GetRawText().Trim('"');
        return "";
    }

    private static double ParseDouble(JsonElement el) =>
        el.ValueKind == JsonValueKind.Number ? el.GetDouble() :
        double.TryParse(el.GetString(), out var d) ? d : 0;

    private static string NormalisePhase(string raw) => raw.Trim() switch
    {
        "4" or "Primary"          => "Primary",
        "5" or "Middle deemed primary" => "Middle (Primary)",
        "3" or "Secondary"        => "Secondary",
        "6" or "Middle deemed secondary" => "Middle (Secondary)",
        "7" or "16 plus"          => "Sixth Form / 16+",
        "0" or "Not applicable"   => "",
        "All-through"             => "All-through",
        var s when s.Contains("Nursery") => "Nursery",
        var s                     => s
    };

    private static string NormaliseType(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        // Strip numeric codes GIAS sometimes returns
        if (int.TryParse(raw, out _)) return raw;
        return raw;
    }

    private static string NormaliseOfstedLabel(string raw) => raw.Trim() switch
    {
        "1" or "Outstanding"           => "Outstanding",
        "2" or "Good"                  => "Good",
        "3" or "Requires improvement"  => "Requires Improvement",
        "4" or "Inadequate"            => "Inadequate",
        "0" or "Not yet inspected" or "" => "Not yet inspected",
        var s                           => s
    };
}
