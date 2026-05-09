using System.Text;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    // ── Council website search ────────────────────────────────────────────────
    private async Task<string> SearchCouncilAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return "No query provided.";
        var url  = $"https://www.bradford.gov.uk/search-results/?query={Uri.EscapeDataString(query)}";
        var html = await FetchHtmlAsync(url, ct);
        if (html == null) return "Could not connect to Bradford Council website.";

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var sb    = new StringBuilder($"Bradford Council search: \"{query}\"\n\n");
        var items = doc.DocumentNode.SelectNodes("//div[contains(@class,'result')]//a")
                 ?? doc.DocumentNode.SelectNodes("//h2/a | //h3/a")
                 ?? doc.DocumentNode.SelectNodes("//article//a");

        int count = 0;
        if (items != null)
        {
            foreach (var item in items.Take(6))
            {
                var title = CleanText(item.InnerText);
                var href  = item.GetAttributeValue("href", "");
                if (title.Length < 5) continue;
                if (!href.StartsWith("http")) href = "https://www.bradford.gov.uk" + href;
                sb.AppendLine($"• {title}\n  {href}");
                count++;
            }
        }
        if (count == 0)
        {
            var body = doc.DocumentNode.SelectSingleNode("//main") ?? doc.DocumentNode.SelectSingleNode("//body");
            if (body != null) sb.AppendLine(TruncateText(CleanText(body.InnerText), 600));
        }
        return sb.ToString();
    }

    private async Task<string> FetchPageAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return "No URL provided.";
        if (!url.StartsWith("http")) url = "https://www.bradford.gov.uk" + url;

        var html = await FetchHtmlAsync(url, ct);
        if (html == null) return $"Could not fetch: {url}";

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        foreach (var tag in new[] { "nav", "header", "footer", "script", "style", "aside", "noscript" })
        {
            var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
            if (nodes != null) foreach (var n in nodes.ToList()) n.Remove();
        }

        var title = CleanText(doc.DocumentNode.SelectSingleNode("//h1")?.InnerText ?? "");
        var main  = doc.DocumentNode.SelectSingleNode("//main")
                 ?? doc.DocumentNode.SelectSingleNode("//article")
                 ?? doc.DocumentNode.SelectSingleNode("//body");
        var text  = main != null ? TruncateText(CleanText(main.InnerText), 2000) : "";
        return string.IsNullOrEmpty(title) ? text : $"PAGE: {title}\nURL: {url}\n\n{text}";
    }

    private async Task<string> CheckPlanningAsync(string reference, string address, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(reference))
        {
            var url = $"https://planning.bradford.gov.uk/online-applications/applicationDetails.do?activeTab=summary&keyVal={Uri.EscapeDataString(reference)}";
            return await FetchPageAsync(url, ct);
        }
        if (!string.IsNullOrEmpty(address))
        {
            var searchUrl = $"https://planning.bradford.gov.uk/online-applications/search.do?action=simple&searchType=Property&searchText={Uri.EscapeDataString(address)}";
            return $"**Bradford Planning Portal search for {address}:**\n{searchUrl}\n\n" + await SearchCouncilAsync($"planning application {address}", ct);
        }
        return await FetchPageAsync("https://www.bradford.gov.uk/planning-and-building-control/planning-applications/", ct);
    }
}
