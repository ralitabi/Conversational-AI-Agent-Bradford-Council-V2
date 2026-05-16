using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Bradford.Core.Models;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private const string BradfordHomesBase = "https://www.bradfordhomes.org.uk";

    private async Task<string> SearchBradfordHomesAsync(
        string location, string bedrooms, string maxRent, string propertyType,
        string radius, CancellationToken ct)
    {
        radius   = string.IsNullOrWhiteSpace(radius)   ? "10"  : radius;
        location = string.IsNullOrWhiteSpace(location) ? "Bradford" : location.Replace(" ", "").ToUpper();

        var url = $"{BradfordHomesBase}/PropertySearch/Results" +
                  $"?AdvertTypes=21" +
                  $"&Location.Name={Uri.EscapeDataString(location)}" +
                  $"&SearchRadius={Uri.EscapeDataString(radius)}" +
                  $"&SortOrder=0";

        if (!string.IsNullOrWhiteSpace(bedrooms) && int.TryParse(bedrooms, out _))
            url += $"&AccommodationTypes={Uri.EscapeDataString(bedrooms)}";
        if (!string.IsNullOrWhiteSpace(maxRent))
            url += $"&MaxRentalCharge={Uri.EscapeDataString(maxRent)}";
        if (!string.IsNullOrWhiteSpace(propertyType))
        {
            var typeCode = propertyType.ToLower() switch
            {
                "house"      => "11",
                "flat"       => "12",
                "bungalow"   => "13",
                "maisonette" => "14",
                _            => ""
            };
            if (!string.IsNullOrEmpty(typeCode))
                url += $"&ConfigurablePanelFilter={typeCode}";
        }

        var properties = new List<BradfordHomesProperty>();
        var totalFound = 0;

        // Scrape up to 2 pages (20 results max)
        for (int page = 1; page <= 2; page++)
        {
            var pageUrl = page == 1 ? url : url.Replace("/Results?", $"/Results/{page}?");
            var html = await FetchHtmlAsync(pageUrl, ct);
            if (string.IsNullOrEmpty(html)) break;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract total count from page (e.g. "Showing 1-10 of 99 properties")
            if (page == 1 && totalFound == 0)
            {
                var countNode = doc.DocumentNode.SelectSingleNode(
                    "//*[contains(@class,'search-results-count') or contains(@class,'results-count') or contains(text(),'properties')]");
                if (countNode != null)
                {
                    var m = Regex.Match(countNode.InnerText, @"(\d+)\s+propert", RegexOptions.IgnoreCase);
                    if (m.Success) int.TryParse(m.Groups[1].Value, out totalFound);
                }
            }

            var adverts = doc.DocumentNode.SelectNodes("//div[contains(@class,'propertyAdvert')]");
            if (adverts == null || adverts.Count == 0) break;

            foreach (var advert in adverts)
            {
                var prop = ParsePropertyAdvert(advert);
                if (prop != null) properties.Add(prop);
            }

            // Stop if fewer than 10 results (last page)
            if (adverts.Count < 10) break;
        }

        if (totalFound == 0) totalFound = properties.Count;

        var result = new BradfordHomesResult
        {
            Items      = properties,
            TotalFound = totalFound,
            SearchUrl  = url,
            Location   = location
        };

        var sb = new StringBuilder();
        sb.AppendLine("[[BRADFORD_HOMES_RESULT]]");
        sb.AppendLine(JsonSerializer.Serialize(result));
        sb.AppendLine("[[/BRADFORD_HOMES_RESULT]]");
        sb.AppendLine();

        if (properties.Count == 0)
        {
            sb.AppendLine($"PROPERTY_INSTRUCTION: No properties found near {location} within {radius} miles. Tell the user no properties are currently available matching their criteria and suggest they try: [Search Bradford Homes]({url}) or broaden their search area.");
        }
        else
        {
            sb.AppendLine($"PROPERTY_INSTRUCTION: Found {properties.Count} properties near {location} (of {totalFound} total). The UI shows a property card grid automatically. Write ONE sentence: \"I found {properties.Count} available properties near {location} — they're shown in the cards below.\" Then add: \"For full details on any property, tap View Property. [See all {totalFound} results on Bradford Homes]({url})\"");
        }

        sb.AppendLine($"OFFICIAL_BRADFORD_LINK: [Bradford Homes — Find a home]({BradfordHomesBase})");
        sb.AppendLine("FOLLOW_UP_SUGGESTION: Would you like to filter by number of bedrooms, property type, or maximum rent?");

        return sb.ToString();
    }

    private static BradfordHomesProperty? ParsePropertyAdvert(HtmlNode advert)
    {
        try
        {
            // Title + detail URL
            var titleLink = advert.SelectSingleNode(".//h4[contains(@class,'advert-heading')]//a")
                         ?? advert.SelectSingleNode(".//a[contains(@class,'advert-heading')]");
            var title     = titleLink != null ? System.Net.WebUtility.HtmlDecode(CleanText(titleLink.InnerText)) : "";
            var href      = titleLink?.GetAttributeValue("href", "") ?? "";
            var id        = Regex.Match(href, @"/Property/(\d+)").Groups[1].Value;
            var detailUrl = string.IsNullOrEmpty(id) ? "" : $"{BradfordHomesBase}/Property/{id}";

            // Address
            var addrNode = advert.SelectSingleNode(".//*[contains(@class,'advert-address')]");
            var address  = addrNode != null ? System.Net.WebUtility.HtmlDecode(CleanText(addrNode.InnerText)) : "";

            // Image — look in propertyMediaContainer for the first img that isn't NoPhoto
            var mediaContainer = advert.SelectSingleNode(".//*[contains(@class,'propertyMediaContainer')]");
            var imgNode        = mediaContainer?.SelectSingleNode(".//img");
            var imageUrl       = imgNode?.GetAttributeValue("src", "") ?? "";
            if (imageUrl.StartsWith("//")) imageUrl = "https:" + imageUrl;
            if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http"))
                imageUrl = BradfordHomesBase + imageUrl;
            if (imageUrl.Contains("NoPhoto") || imageUrl.Contains("CBLIcon")) imageUrl = "";

            // Control values — values are in .controls sibling div inside each .AdvertDisplayFieldControl group
            var rent     = "";
            var landlord = "";
            var distance = "";
            var propRef  = "";

            var groups = advert.SelectNodes(".//*[contains(@class,'AdvertDisplayFieldControl')]");
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    var labelNode    = group.SelectSingleNode(".//*[contains(@class,'control-label')]");
                    var controlsNode = group.SelectSingleNode(".//*[contains(@class,'controls')]");
                    if (labelNode == null || controlsNode == null) continue;

                    // Strip sr-only spans before reading text
                    foreach (var sr in controlsNode.SelectNodes(".//*[contains(@class,'sr-only')]")?.ToList()
                             ?? new System.Collections.Generic.List<HtmlNode>())
                        sr.Remove();

                    var labelText = CleanText(labelNode.InnerText).ToLower();
                    var value     = System.Net.WebUtility.HtmlDecode(controlsNode.InnerText).Trim();

                    if (labelText.Contains("total charge"))
                        rent = value.TrimStart('£').TrimStart().Insert(0, "£");
                    else if (labelText.Contains("landlord"))
                        landlord = value;
                    else if (labelText.Contains("distance") && value != "unknown")
                        distance = value;
                    else if (labelText.Contains("property ref"))
                        propRef = value;
                }
            }

            // Bedrooms + features from icon-summary-container (skip advert-type-icon-container)
            var iconSection = advert.SelectSingleNode(".//*[contains(@class,'advert-icons-section')]");
            var icons       = iconSection?.SelectNodes(".//img[@alt]");
            var bedrooms    = "";
            var features    = new System.Collections.Generic.List<string>();

            // Feature alt texts to skip
            var skipAlts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Property Shop", "No photo is available for this property" };

            if (icons != null)
            {
                foreach (var icon in icons)
                {
                    var alt = icon.GetAttributeValue("alt", "").Trim();
                    if (string.IsNullOrWhiteSpace(alt) || skipAlts.Contains(alt)) continue;

                    // Bedrooms
                    if (string.IsNullOrEmpty(bedrooms))
                    {
                        var bm = Regex.Match(alt, @"(\d+)\s*bedroom", RegexOptions.IgnoreCase);
                        if (bm.Success) { bedrooms = bm.Groups[1].Value + " bed"; continue; }
                        if (alt.Equals("Studio", StringComparison.OrdinalIgnoreCase)) { bedrooms = "Studio"; continue; }
                    }

                    // Features — skip very long or very short alt texts
                    if (alt.Length < 3 || alt.Length > 55) continue;
                    if (!features.Contains(alt) && features.Count < 4)
                        features.Add(alt);
                }
            }

            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(address)) return null;

            return new BradfordHomesProperty
            {
                Id          = id,
                Title       = title,
                Address     = address,
                Rent        = rent,
                Landlord    = landlord,
                Bedrooms    = bedrooms,
                Distance    = distance,
                ImageUrl    = imageUrl,
                DetailUrl   = detailUrl,
                PropertyRef = propRef,
                Features    = features
            };
        }
        catch { return null; }
    }
}
