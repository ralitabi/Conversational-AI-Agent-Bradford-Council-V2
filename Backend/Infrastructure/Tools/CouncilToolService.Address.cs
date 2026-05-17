using System.Net;
using System.Text;
using System.Text.Json;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Bradford.Core.Models;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    // ── Address Lookup ────────────────────────────────────────────────────────
    private async Task<string> LookupAddressesAsync(string postcode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(postcode))
            return "NEEDS_POSTCODE: Please ask the user for their Bradford postcode.";

        postcode = postcode.Trim().ToUpper();
        if (!postcode.Contains(' ') && postcode.Length >= 5)
            postcode = postcode[..^3] + " " + postcode[^3..];

        // Validate postcode first
        var postcodeData = await ValidatePostcodeAsync(postcode, ct);
        if (postcodeData == null)
            return $"INVALID_POSTCODE: '{postcode}' does not appear to be a valid UK postcode. Ask the user to double-check it.";

        var terminatedNote = postcodeData.IsTerminated
            ? $" Note: {postcode} is a retired postcode — addresses may not be found."
            : "";

        List<AddressOption> addresses = new();

        // 1st choice: getAddress.io — real Royal Mail PAF data, exact postcode match
        if (!string.IsNullOrWhiteSpace(_getAddressApiKey))
        {
            addresses = await GetAddressIoAsync(postcode, _getAddressApiKey, ct);
            _logger.LogInformation("getAddress.io returned {Count} for {Postcode}", addresses.Count, postcode);
        }

        // 2nd choice: Bradford Council form scraper (proper session + cookies)
        if (addresses.Count == 0)
        {
            addresses = await TryBradfordFormAddressesAsync(postcode, ct);
            _logger.LogInformation("Bradford form returned {Count} for {Postcode}", addresses.Count, postcode);
        }

        // 3rd choice: OpenStreetMap Overpass (parallelised passes)
        if (addresses.Count == 0)
        {
            addresses = await GetAddressesFromOverpassAsync(postcode, ct);
            _logger.LogInformation("Overpass returned {Count} for {Postcode}", addresses.Count, postcode);
        }

        // 4th choice: Nominatim street-level fallback
        if (addresses.Count == 0)
        {
            addresses = await GetAddressesFromNominatimAsync(postcode, ct);
            _logger.LogInformation("Nominatim returned {Count} for {Postcode}", addresses.Count, postcode);
        }

        var sb = new StringBuilder();
        sb.AppendLine("[[ADDRESS_LIST]]");
        sb.AppendLine(JsonSerializer.Serialize(addresses));
        sb.AppendLine("[[/ADDRESS_LIST]]");

        // Do NOT include individual address details in text — the UI shows the interactive
        // picker automatically. AI must only write one short sentence.
        if (addresses.Count == 0)
            sb.AppendLine($"ADDRESS_INSTRUCTION: No properties found for {postcode}.{terminatedNote} Tell the user the postcode is valid but I couldn't find individual addresses — they can type theirs manually using the box that appeared below.");
        else
            sb.AppendLine($"ADDRESS_INSTRUCTION: {addresses.Count} addresses found for {postcode}. The UI shows an interactive address picker. Write ONLY: \"I found {addresses.Count} addresses for {postcode} — please tap yours from the list below.\" Do not list or mention any addresses.");

        return sb.ToString();
    }

    // ── getAddress.io API ─────────────────────────────────────────────────────
    // Real Royal Mail PAF data — guaranteed correct postcode match
    private async Task<List<AddressOption>> GetAddressIoAsync(string postcode, string apiKey, CancellationToken ct)
    {
        try
        {
            var encoded = Uri.EscapeDataString(postcode);
            var url     = $"https://api.getaddress.io/find/{encoded}?api-key={apiKey}&expand=true";

            using var req  = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("getAddress.io {Status} for {Postcode}", resp.StatusCode, postcode);
                return new();
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("addresses", out var arr))
                return new();

            var list = new List<AddressOption>();
            int num  = 1;

            foreach (var item in arr.EnumerateArray())
            {
                var subBuilding = item.TryGetProperty("sub_building_name",  out var sb)  ? sb.GetString()  ?? "" : "";
                var buildingName= item.TryGetProperty("building_name",       out var bn)  ? bn.GetString()  ?? "" : "";
                var line1       = item.TryGetProperty("line_1",              out var l1)  ? l1.GetString()  ?? "" : "";
                var line2       = item.TryGetProperty("line_2",              out var l2)  ? l2.GetString()  ?? "" : "";
                var city        = item.TryGetProperty("town_or_city",        out var tc)  ? tc.GetString()  ?? "Bradford" : "Bradford";

                // Build the most descriptive line1 possible
                if (string.IsNullOrWhiteSpace(line1))
                {
                    // Fall back to formatted_address array
                    if (item.TryGetProperty("formatted_address", out var fa) && fa.ValueKind == JsonValueKind.Array)
                    {
                        var parts = fa.EnumerateArray()
                                      .Select(p => p.GetString()?.Trim())
                                      .Where(p => !string.IsNullOrEmpty(p))
                                      .ToList();
                        line1 = parts.FirstOrDefault() ?? "";
                    }
                }

                if (string.IsNullOrWhiteSpace(line1)) continue;

                // Prefix sub_building or building_name when not already in line1
                var prefix = string.Join(", ", new[]{ subBuilding, buildingName }
                                               .Where(s => !string.IsNullOrWhiteSpace(s) &&
                                                           !line1.Contains(s, StringComparison.OrdinalIgnoreCase)));
                var full = string.Join(", ", new[]{ prefix, line1, line2 }.Where(s => !string.IsNullOrWhiteSpace(s)));

                list.Add(new AddressOption
                {
                    Number   = num++,
                    Line1    = full,
                    City     = city,
                    Postcode = postcode
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("getAddress.io failed: {Msg}", ex.Message);
            return new();
        }
    }

    // ── Bradford Council form — address lookup via session + cookies ──────────
    // Form URL pattern: https://onlineforms.bradford.gov.uk/ufs/collectiondates.eb?ebd=0&ebp=10&ebz=1_<timestamp>
    private async Task<List<AddressOption>> TryBradfordFormAddressesAsync(string postcode, CancellationToken ct)
    {
        try
        {
            var cookies = new CookieContainer();
            using var handler = new HttpClientHandler
            {
                UseCookies        = true,
                CookieContainer   = cookies,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-GB,en;q=0.9");

            // Generate a fresh timestamp-based session token (same pattern the real form uses: 1_<ms>)
            var sessionToken = $"1_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var baseUrl      = "https://onlineforms.bradford.gov.uk/ufs/collectiondates.eb";
            var initUrl      = $"{baseUrl}?ebd=0&ebp=10&ebz={sessionToken}";

            // Step 1: GET the form page to capture hidden fields + actual session URL
            var initResp = await client.GetAsync(initUrl, ct);
            if (!initResp.IsSuccessStatusCode) return new();

            var initHtml   = await initResp.Content.ReadAsStringAsync(ct);
            var sessionUrl = initResp.RequestMessage?.RequestUri?.ToString() ?? initUrl;

            var initDoc = new HtmlDocument();
            initDoc.LoadHtml(initHtml);

            // Use the <form action="..."> URL if present (eForms sometimes has a different POST target)
            var formActionRaw = initDoc.DocumentNode.SelectSingleNode("//form")
                                       ?.GetAttributeValue("action", "") ?? "";
            if (!string.IsNullOrWhiteSpace(formActionRaw))
            {
                // Resolve relative URLs properly (e.g. "collectiondates.eb" → base + "/ufs/collectiondates.eb")
                if (Uri.TryCreate(formActionRaw, UriKind.Absolute, out var absUri))
                    sessionUrl = absUri.ToString();
                else if (Uri.TryCreate(new Uri(sessionUrl), formActionRaw, out var relUri))
                    sessionUrl = relUri.ToString();
            }

            // Collect ALL hidden inputs — the Bradford eForms system uses these for session state
            var formData = initDoc.DocumentNode
                .SelectNodes("//input[@type='hidden']")
                ?.Select(n => new KeyValuePair<string, string>(
                    n.GetAttributeValue("name",  ""),
                    n.GetAttributeValue("value", "")))
                .Where(p => !string.IsNullOrEmpty(p.Key))
                .ToList() ?? new();

            // Find the postcode field
            var postcodeNode = initDoc.DocumentNode
                .SelectNodes("//input[@type='text' or @type='search']")
                ?.FirstOrDefault(n =>
                {
                    var id   = n.GetAttributeValue("id",   "").ToLower();
                    var name = n.GetAttributeValue("name", "").ToLower();
                    return id.Contains("post") || name.Contains("post") ||
                           id.Contains("code") || name.Contains("code");
                })
                ?? initDoc.DocumentNode.SelectSingleNode("//input[@type='text']");

            var postcodeFieldName = postcodeNode?.GetAttributeValue("name", "eb_postcode") ?? "eb_postcode";
            formData.Add(new KeyValuePair<string, string>(postcodeFieldName, postcode));

            // Include the submit button's name/value (eForms needs this to advance to next page)
            var submit = initDoc.DocumentNode
                .SelectSingleNode("//input[@type='submit'] | //button[@type='submit']");
            if (submit != null)
            {
                var btnName  = submit.GetAttributeValue("name",  "");
                var btnValue = submit.GetAttributeValue("value", "Find address");
                if (!string.IsNullOrEmpty(btnName))
                    formData.Add(new KeyValuePair<string, string>(btnName, btnValue));
            }

            // Step 2: POST with Referer + Origin (councils use these for CSRF validation)
            using var postMsg = new HttpRequestMessage(HttpMethod.Post, sessionUrl)
            {
                Content = new FormUrlEncodedContent(formData)
            };
            postMsg.Headers.Add("Referer", initUrl);
            postMsg.Headers.Add("Origin",  "https://onlineforms.bradford.gov.uk");

            var postResp = await client.SendAsync(postMsg, ct);
            if (!postResp.IsSuccessStatusCode) return new();

            var resultHtml = await postResp.Content.ReadAsStringAsync(ct);
            var addresses  = ParseBradfordAddressPage(resultHtml, postcode);

            _logger.LogInformation("Bradford form returned {Count} addresses for {Postcode}", addresses.Count, postcode);
            return addresses;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Bradford form scrape failed: {Msg}", ex.Message);
            return new();
        }
    }

    // Parse the Bradford form's address results page.
    // The form returns addresses as clickable links with text: "813,MANCHESTER ROAD,BRADFORD,BD5 8LT"
    private List<AddressOption> ParseBradfordAddressPage(string html, string postcode)
    {
        var doc  = new HtmlDocument();
        doc.LoadHtml(html);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<AddressOption>();
        int num  = 1;

        // ── Strategy 1: Bradford-format comma-separated addresses ──
        // "813,MANCHESTER ROAD,BRADFORD,BD5 8LT"
        var nodes = doc.DocumentNode.SelectNodes("//a | //td | //li") ?? new HtmlNodeCollection(null);

        foreach (var node in nodes)
        {
            // Only look at the direct text of this node, not all descendants
            var raw = System.Net.WebUtility.HtmlDecode(
                          string.Concat(node.ChildNodes
                              .Where(n => n.NodeType == HtmlAgilityPack.HtmlNodeType.Text)
                              .Select(n => n.InnerText))
                      ).Trim();

            // Addresses are short: "813,MANCHESTER ROAD,BRADFORD,BD5 8LT" ~ 35 chars
            if (raw.Length < 8 || raw.Length > 120 || !raw.Contains(',')) continue;

            var parts = raw.Split(',');
            if (parts.Length < 2) continue;

            var descriptor = parts[0].Trim();                           // "813"
            var street     = parts[1].Trim();                           // "MANCHESTER ROAD"
            var cityRaw    = parts.Length > 2 ? parts[2].Trim() : "";  // "BRADFORD"
            var pcRaw      = (parts.Length > 3 ? parts[3] : "").Trim();// "BD5 8LT"

            // Validate: descriptor and street must be non-empty
            if (descriptor.Length == 0 || street.Length < 3) continue;

            // Validate: postcode part must match the outward code (e.g. "BD5") if present
            if (!string.IsNullOrEmpty(pcRaw))
            {
                var outward = postcode.Split(' ')[0];
                if (!pcRaw.StartsWith(outward, StringComparison.OrdinalIgnoreCase) &&
                    !pcRaw.Equals(postcode, StringComparison.OrdinalIgnoreCase)) continue;
            }

            // Descriptor must start with a digit or be a flat/unit/name (not city/county words)
            var skipWords = new[]{"ENGLAND","UNITED KINGDOM","WEST YORKSHIRE","YORKSHIRE","UK"};
            if (skipWords.Any(w => descriptor.Equals(w, StringComparison.OrdinalIgnoreCase))) continue;

            var line1 = ToTitleCase($"{descriptor} {street}");
            var city  = ToTitleCase(cityRaw.Length > 0 ? cityRaw : "Bradford");

            if (!seen.Add(line1.ToLowerInvariant())) continue;

            list.Add(new AddressOption
            {
                Number   = num++,
                Line1    = line1,
                City     = city,
                Postcode = postcode,
                Uprn     = (node.GetAttributeValue("href", "") + node.GetAttributeValue("value", "")).TrimStart('#')
            });
        }

        if (list.Count > 0) return list;

        // ── Strategy 2: <select> / <option> dropdown ──
        var select = doc.DocumentNode.SelectSingleNode("//select");
        if (select != null)
        {
            foreach (var opt in select.SelectNodes(".//option") ?? Enumerable.Empty<HtmlNode>())
            {
                var text = CleanText(System.Net.WebUtility.HtmlDecode(opt.InnerText));
                var val  = opt.GetAttributeValue("value", "");
                if (text.Length < 5 || text.ToLower().Contains("select") || text.ToLower().Contains("choose")) continue;
                if (seen.Add(text.ToLowerInvariant()))
                    list.Add(new AddressOption { Number = num++, Line1 = ToTitleCase(text), City = "Bradford", Postcode = postcode, Uprn = val });
            }
            if (list.Count > 0) return list;
        }

        // ── Strategy 3: radio buttons ──
        foreach (var radio in doc.DocumentNode.SelectNodes("//input[@type='radio']") ?? Enumerable.Empty<HtmlNode>())
        {
            var id    = radio.GetAttributeValue("id", "");
            var label = doc.DocumentNode.SelectSingleNode($"//label[@for='{id}']");
            var text  = CleanText(label?.InnerText ?? "");
            if (text.Length < 5) continue;
            if (seen.Add(text.ToLowerInvariant()))
                list.Add(new AddressOption { Number = num++, Line1 = ToTitleCase(text), City = "Bradford", Postcode = postcode, Uprn = radio.GetAttributeValue("value", "") });
        }

        return list;
    }

    // ── Overpass 3-pass address lookup (pass 1+2 run in parallel) ────────────
    private async Task<List<AddressOption>> GetAddressesFromOverpassAsync(string postcode, CancellationToken ct)
    {
        var ll = await GetPostcodeLatLngAsync(postcode, ct);
        if (!ll.HasValue) return new();
        var (lat, lon) = ll.Value;

        // Pass 1 and Pass 2 run in parallel to halve latency
        var pass1Task = OverpassQueryAsync(
            $"[out:json][timeout:18];(node[\"addr:postcode\"=\"{postcode}\"][\"addr:housenumber\"];way[\"addr:postcode\"=\"{postcode}\"][\"addr:housenumber\"];);out center;",
            postcode, ct);

        var pass2Task = OverpassQueryAsync(
            $"[out:json][timeout:18];(node[\"addr:housenumber\"](around:200,{lat},{lon});way[\"addr:housenumber\"](around:200,{lat},{lon}););out center;",
            postcode, ct);

        await Task.WhenAll(pass1Task, pass2Task);

        var merged  = MergeAddressLists(postcode, pass1Task.Result, pass2Task.Result);
        var streets = merged
            .Select(a => a.Line1.Contains(' ') ? string.Join(" ", a.Line1.Split(' ').Skip(1)) : "")
            .Where(s => s.Length > 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Pass 3: fetch ALL houses on discovered streets (fills gaps)
        var pass3Seen = new HashSet<string>(merged.Select(a => a.Line1.ToLowerInvariant()));
        var pass3Tasks = streets.Take(3).Select(street =>
            OverpassQueryAsync(
                $"[out:json][timeout:18];" +
                $"(node[\"addr:street\"=\"{street}\"][\"addr:housenumber\"](around:300,{lat},{lon});" +
                $"way[\"addr:street\"=\"{street}\"][\"addr:housenumber\"](around:300,{lat},{lon}););" +
                $"out center;",
                postcode, ct)).ToList();

        if (pass3Tasks.Count > 0)
        {
            await Task.WhenAll(pass3Tasks);
            foreach (var task in pass3Tasks)
                foreach (var a in task.Result.Where(a => pass3Seen.Add(a.Line1.ToLowerInvariant())))
                    merged.Add(a);
        }

        var sorted = merged
            .OrderBy(a =>
            {
                var parts = a.Line1.Split(' ');
                return int.TryParse(parts[0], out var n) ? n : int.MaxValue;
            })
            .ThenBy(a => a.Line1)
            .Select((a, i) => { a.Number = i + 1; return a; })
            .Take(100)
            .ToList();

        _logger.LogInformation("Overpass final {Count} addresses for {Postcode}", sorted.Count, postcode);
        return sorted;
    }

    private static List<AddressOption> MergeAddressLists(string postcode, params List<AddressOption>[] lists)
    {
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<AddressOption>();
        foreach (var list in lists)
            foreach (var a in list)
                if (seen.Add(a.Line1.ToLowerInvariant()))
                { a.Postcode = postcode; merged.Add(a); }
        return merged;
    }

    private async Task<List<AddressOption>> OverpassQueryAsync(string query, string postcode, CancellationToken ct)
    {
        try
        {
            var url = $"https://overpass-api.de/api/interpreter?data={Uri.EscapeDataString(query)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "BradfordCouncilAI/1.0 (council service)");
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return new();

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("elements", out var elements))
                return new();

            var seen    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list    = new List<AddressOption>();
            var outward = postcode.Split(' ')[0];

            foreach (var el in elements.EnumerateArray())
            {
                if (!el.TryGetProperty("tags", out var tags)) continue;
                var houseNum  = tags.TryGetProperty("addr:housenumber", out var hn) ? hn.GetString() : null;
                var street    = tags.TryGetProperty("addr:street",      out var st) ? st.GetString() : null;
                var city      = tags.TryGetProperty("addr:city",        out var ci) ? ci.GetString()
                              : tags.TryGetProperty("addr:town",        out var to) ? to.GetString() : "Bradford";
                var elPostcode = tags.TryGetProperty("addr:postcode",   out var pc) ? pc.GetString() : null;

                if (string.IsNullOrEmpty(houseNum) || string.IsNullOrEmpty(street)) continue;

                // If the element has a postcode tag, ensure it matches — prevents cross-postcode bleed
                if (!string.IsNullOrEmpty(elPostcode) &&
                    !elPostcode.Equals(postcode, StringComparison.OrdinalIgnoreCase) &&
                    !elPostcode.StartsWith(outward, StringComparison.OrdinalIgnoreCase)) continue;

                var line1 = $"{houseNum} {street}";
                if (!seen.Add(line1.ToLowerInvariant())) continue;

                list.Add(new AddressOption { Number = list.Count + 1, Line1 = line1, City = city ?? "Bradford", Postcode = postcode });
            }
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Overpass query failed: {Msg}", ex.Message);
            return new();
        }
    }

    private async Task<(double lat, double lon)?> GetPostcodeLatLngAsync(string postcode, CancellationToken ct)
    {
        try
        {
            using var req  = new HttpRequestMessage(HttpMethod.Get, $"https://api.postcodes.io/postcodes/{Uri.EscapeDataString(postcode)}");
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("result", out var result)) return null;
            return (result.GetProperty("latitude").GetDouble(), result.GetProperty("longitude").GetDouble());
        }
        catch { return null; }
    }

    // ── Nominatim fallback ────────────────────────────────────────────────────
    private async Task<List<AddressOption>> GetAddressesFromNominatimAsync(string postcode, CancellationToken ct)
    {
        try
        {
            var url = $"https://nominatim.openstreetmap.org/search?format=json&postalcode={Uri.EscapeDataString(postcode)}&countrycodes=gb&addressdetails=1&limit=20";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "BradfordCouncilAI/1.0 (council service)");
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return new();

            var json    = await resp.Content.ReadAsStringAsync(ct);
            var results = JsonSerializer.Deserialize<List<NominatimResult>>(json);
            if (results == null) return new();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<AddressOption>();

            foreach (var r in results.Take(20))
            {
                var addr  = r.Address;
                if (addr == null) continue;
                var line1 = BuildAddressLine(addr);
                if (string.IsNullOrWhiteSpace(line1) || !seen.Add(line1.ToLowerInvariant())) continue;
                list.Add(new AddressOption
                {
                    Number   = list.Count + 1,
                    Line1    = line1,
                    City     = addr.City ?? addr.Town ?? addr.Village ?? "Bradford",
                    Postcode = postcode
                });
            }
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Nominatim failed: {Msg}", ex.Message);
            return new();
        }
    }

    // ── Postcode Validation ───────────────────────────────────────────────────
    private async Task<PostcodeData?> ValidatePostcodeAsync(string postcode, CancellationToken ct)
    {
        try
        {
            // Try live postcode first
            using var req  = new HttpRequestMessage(HttpMethod.Get, $"https://api.postcodes.io/postcodes/{Uri.EscapeDataString(postcode)}");
            using var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var json   = await resp.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<PostcodeResponse>(json);
                if (result?.Status == 200) return result.Result;
            }

            // Try terminated postcodes — real Bradford postcodes that have been retired.
            // Users may have old letters/documents with these. Accept them but addresses may be empty.
            using var termReq  = new HttpRequestMessage(HttpMethod.Get, $"https://api.postcodes.io/terminated_postcodes/{Uri.EscapeDataString(postcode)}");
            using var termResp = await _http.SendAsync(termReq, ct);
            if (termResp.IsSuccessStatusCode)
            {
                var termJson = await termResp.Content.ReadAsStringAsync(ct);
                using var termDoc = JsonDocument.Parse(termJson);
                if (termDoc.RootElement.TryGetProperty("status", out var st) && st.GetInt32() == 200)
                {
                    // Synthesise minimal PostcodeData so the rest of the flow continues
                    return new PostcodeData
                    {
                        Postcode       = postcode,
                        AdminDistrict  = "Bradford",
                        IsTerminated   = true
                    };
                }
            }

            return null;
        }
        catch { return null; }
    }
}
