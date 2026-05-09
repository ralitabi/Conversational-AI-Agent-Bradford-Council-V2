using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Bradford.Core.Models;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    // ── Bradford library catalogue (name, address, phone, lat, lon, page slug) ──
    private static readonly (string Name, string Address, string Phone, double Lat, double Lon, string Slug)[] BradfordLibraries =
    {
        ("Bradford Central Library", "Princes Way, Bradford, BD1 1NN",         "01274 433600", 53.7946, -1.7523, "bradford-central-library"),
        ("Bingley Library",          "Myrtle Place, Bingley, BD16 2EQ",        "01274 433600", 53.8484, -1.8380, "bingley-library"),
        ("Clayton Library",          "Clayton Lane, Clayton, BD14 6AH",         "01274 433600", 53.7826, -1.8065, "clayton-library"),
        ("Eccleshill Library",       "Harrogate Road, Eccleshill, BD2 3JG",     "01274 433600", 53.8155, -1.7323, "eccleshill-library"),
        ("Great Horton Library",     "Cemetery Road, Great Horton, BD7 2DR",    "01274 433600", 53.7768, -1.7873, "great-horton-library"),
        ("Haworth Library",          "Main Street, Haworth, BD22 8DU",          "01274 433600", 53.8333, -1.9586, "haworth-library"),
        ("Idle Library",             "Albion Road, Idle, Bradford, BD10 9PY",   "01274 433600", 53.8227, -1.7547, "idle-library"),
        ("Ilkley Library",           "Station Road, Ilkley, LS29 8HA",          "01274 433600", 53.9252, -1.8214, "ilkley-library"),
        ("Keighley Library",         "North Street, Keighley, BD21 3SX",        "01274 433600", 53.8676, -1.9075, "keighley-library"),
        ("Laisterdyke Library",      "Rooley Lane, Bradford, BD4 8HG",          "01274 433600", 53.7857, -1.7193, "laisterdyke-library"),
        ("Manningham Library",       "Carlisle Road, Manningham, BD8 8AU",      "01274 433600", 53.8017, -1.7747, "manningham-library"),
        ("Oakworth Library",         "Slaymaker Lane, Oakworth, BD22 7PZ",      "01274 433600", 53.8494, -1.9417, "oakworth-library"),
        ("Otley Library",            "Nelson Street, Otley, LS21 1EX",          "01274 433600", 53.9043, -1.6895, "otley-library"),
        ("Queensbury Library",       "Queensbury Square, Queensbury, BD13 1AD", "01274 433600", 53.7682, -1.8398, "queensbury-library"),
        ("Saltaire Library",         "Victoria Road, Shipley, BD18 4PS",        "01274 433600", 53.8378, -1.7894, "saltaire-library"),
        ("Shipley Library",          "Wellcroft, Shipley, BD18 3QH",            "01274 433600", 53.8332, -1.7745, "shipley-library"),
        ("Silsden Library",          "Elliott Street, Silsden, BD20 0DP",       "01274 433600", 53.9143, -1.9348, "silsden-library"),
        ("Thornton Library",         "Thornton Road, Thornton, BD13 3NJ",       "01274 433600", 53.7959, -1.8481, "thornton-library"),
        ("Wibsey Library",           "Wibsey Park Avenue, Wibsey, BD6 3QZ",     "01274 433600", 53.7634, -1.7801, "wibsey-library"),
        ("Wyke Library",             "Appleton Road, Wyke, BD12 9AH",           "01274 433600", 53.7594, -1.7616, "wyke-library"),
    };

    // Haversine formula — returns distance in miles
    private static double DistanceMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3958.8; // Earth radius in miles
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a    = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private async Task<string> FindLocalServicesAsync(string service, string location, CancellationToken ct)
    {
        var isLibrary = service.Contains("library", StringComparison.OrdinalIgnoreCase)
                     || service.Contains("libraries", StringComparison.OrdinalIgnoreCase);

        // Library with postcode → fetch nearest libraries with distances
        if (isLibrary && !string.IsNullOrWhiteSpace(location))
        {
            var results = await FetchLibrariesNearPostcodeAsync(location.Trim(), ct);
            if (!string.IsNullOrEmpty(results))
                return results;
        }

        // Direct URL map for other services
        var directUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["library"]   = "https://www.bradford.gov.uk/libraries/find-your-local-library/find-your-local-library/",
            ["leisure"]   = "https://www.bradford.gov.uk/sport-and-leisure/",
            ["park"]      = "https://www.bradford.gov.uk/parks-and-open-spaces/",
            ["school"]    = "https://www.bradford.gov.uk/education-and-skills/schools/",
            ["recycling"] = "https://www.bradford.gov.uk/recycling-and-waste/recycling-centres/"
        };

        var directUrl = directUrls.FirstOrDefault(kv =>
            service.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)).Value;

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(directUrl))
        {
            sb.AppendLine($"Direct link: {directUrl}");
            sb.AppendLine(await FetchPageAsync(directUrl, ct));
        }
        else
        {
            var query = string.IsNullOrEmpty(location) ? service : $"{service} near {location} Bradford";
            sb.AppendLine(await SearchCouncilAsync(query, ct));
        }
        return sb.ToString();
    }

    // ── Library finder — all libraries sorted by distance from postcode ─────
    private async Task<string> FetchLibrariesNearPostcodeAsync(string postcode, CancellationToken ct)
    {
        postcode = postcode.Trim().ToUpper();
        if (!postcode.Contains(' ') && postcode.Length >= 5)
            postcode = postcode[..^3] + " " + postcode[^3..];

        const string finderUrl = "https://www.bradford.gov.uk/libraries/find-your-local-library/find-your-local-library/";

        var ll = await GetPostcodeLatLngAsync(postcode, ct);
        if (!ll.HasValue)
            return $"Could not locate {postcode}. Search for your nearest library at: {finderUrl}";

        var (userLat, userLon) = ll.Value;

        // All Bradford libraries sorted nearest first
        var sorted = BradfordLibraries
            .Select((lib, idx) => (lib, num: idx + 1, miles: DistanceMiles(userLat, userLon, lib.Lat, lib.Lon)))
            .OrderBy(x => x.miles)
            .Select((x, i) => (x.lib, num: i + 1, x.miles))
            .ToList();

        // Build structured library list for UI bubble buttons
        var libraryOptions = sorted.Select((x, i) => new Bradford.Core.Models.LibraryOption
        {
            Number   = i + 1,
            Name     = x.lib.Name,
            Address  = x.lib.Address,
            Distance = x.miles < 0.1 ? "< 0.1 mi" : $"{x.miles:F1} mi",
            Phone    = x.lib.Phone,
            Slug     = x.lib.Slug
        }).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("[[LIBRARY_LIST]]");
        sb.AppendLine(JsonSerializer.Serialize(libraryOptions));
        sb.AppendLine("[[/LIBRARY_LIST]]");
        sb.AppendLine();
        sb.AppendLine($"All {sorted.Count} Bradford libraries sorted by distance from {postcode} are shown as clickable buttons.");
        sb.AppendLine("INSTRUCTION: Write ONE short sentence only, e.g. \"Here are all Bradford libraries nearest to BD5 8LT — tap one to see full details, facilities and how to join.\" Do NOT list them.");

        return sb.ToString();
    }

    // ── Library details — facilities, hours, how to join ─────────────────────
    private async Task<string> GetLibraryDetailsAsync(string libraryName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(libraryName))
            return "Please provide a library name.";

        // Fuzzy-match the library name
        var match = BradfordLibraries
            .Select(lib => (lib, score: LibraryMatchScore(lib.Name, libraryName)))
            .OrderByDescending(x => x.score)
            .FirstOrDefault();

        if (match.lib.Name == null || match.score == 0)
            return $"Library '{libraryName}' not found. Try a name like 'Wyke Library' or 'Bradford Central Library'.";

        var lib = match.lib;
        var pageUrl = $"https://www.bradford.gov.uk/libraries/find-your-local-library/{lib.Slug}/";
        const string joinUrl = "https://www.bradford.gov.uk/libraries/join-the-library/";

        // Fetch library page and join page in parallel
        var pageTask = FetchPageTextAsync(pageUrl, ct);
        var joinTask = FetchPageTextAsync(joinUrl, ct);
        await Task.WhenAll(pageTask, joinTask);

        var pageContent = pageTask.Result;
        var joinContent = joinTask.Result;

        var sb = new StringBuilder();
        sb.AppendLine($"LIBRARY_DETAILS:{lib.Name}");
        sb.AppendLine();
        sb.AppendLine($"**{lib.Name}**");
        sb.AppendLine($"Address: {lib.Address}");
        sb.AppendLine($"Phone: {lib.Phone}");
        sb.AppendLine($"Page: {pageUrl}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(pageContent))
        {
            sb.AppendLine("## From Bradford Council website:");
            sb.AppendLine(TruncateText(pageContent, 1800));
        }
        else
        {
            // Hardcoded standard facilities all Bradford libraries offer
            sb.AppendLine("## Standard facilities at Bradford libraries:");
            sb.AppendLine("- Free public Wi-Fi");
            sb.AppendLine("- Public computers with internet access");
            sb.AppendLine("- Printing, photocopying and scanning");
            sb.AppendLine("- Free book lending — fiction, non-fiction, children's");
            sb.AppendLine("- E-books and audiobooks via the BorrowBox app (free with library card)");
            sb.AppendLine("- Newspapers and magazines");
            sb.AppendLine("- Accessible facilities (step-free access at most branches)");
            sb.AppendLine("- Study/quiet reading space");
            sb.AppendLine("- Children's section and storytime sessions");
            sb.AppendLine("- DVD and CD lending");
            sb.AppendLine("- Self-service checkout machines");
        }

        sb.AppendLine();
        sb.AppendLine("## How to join / apply for a library card:");
        if (!string.IsNullOrWhiteSpace(joinContent))
            sb.AppendLine(TruncateText(joinContent, 800));

        sb.AppendLine();
        sb.AppendLine("**Ways to join:**");
        sb.AppendLine("- Online: complete the form at https://www.bradford.gov.uk/libraries/join-the-library/");
        sb.AppendLine("- In person: visit any Bradford library with proof of address and ID");
        sb.AppendLine("- Membership is FREE for all Bradford residents");
        sb.AppendLine("- Children under 16 can join with a parent or guardian's signature");
        sb.AppendLine("- Non-residents can join for a small annual fee");
        sb.AppendLine();
        sb.AppendLine($"For current opening hours visit: {pageUrl}");

        return sb.ToString();
    }

    private static int LibraryMatchScore(string candidate, string query)
    {
        var c = candidate.ToLowerInvariant().Replace("library", "").Trim();
        var q = query.ToLowerInvariant().Replace("library", "").Trim();
        if (c == q) return 100;
        if (c.Contains(q) || q.Contains(c)) return 80;
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Count(w => candidate.Contains(w, StringComparison.OrdinalIgnoreCase)) * 20;
    }
}
