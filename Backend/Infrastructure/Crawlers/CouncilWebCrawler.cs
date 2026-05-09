using Bradford.Core.Interfaces;
using Bradford.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Bradford.Infrastructure.Crawlers;

public class CouncilWebCrawler : IWebCrawlerService
{
    private readonly HttpClient _http;
    private readonly ILogger<CouncilWebCrawler> _logger;

    // HTML elements to strip entirely (navigation, boilerplate)
    private static readonly string[] SkipTags = { "nav", "header", "footer", "script", "style", "aside", "form" };

    public CouncilWebCrawler(IHttpClientFactory factory, ILogger<CouncilWebCrawler> logger)
    {
        _http = factory.CreateClient("Crawler");
        _logger = logger;
    }

    public async Task<CrawledPage?> CrawlPageAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var html = await _http.GetStringAsync(url, ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove unwanted elements
            foreach (var tag in SkipTags)
            {
                var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
                if (nodes != null)
                    foreach (var node in nodes) node.Remove();
            }

            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText.Trim()
                        ?? doc.DocumentNode.SelectSingleNode("//h1")?.InnerText.Trim()
                        ?? url;

            var mainContent = doc.DocumentNode.SelectSingleNode("//main")
                              ?? doc.DocumentNode.SelectSingleNode("//article")
                              ?? doc.DocumentNode.SelectSingleNode("//div[@class and contains(@class,'content')]")
                              ?? doc.DocumentNode.SelectSingleNode("//body");

            if (mainContent == null) return null;

            var plainText = CleanText(mainContent.InnerText);
            if (string.IsNullOrWhiteSpace(plainText)) return null;

            return new CrawledPage
            {
                Url = url,
                Title = HtmlEntity.DeEntitize(title),
                PlainText = plainText,
                Category = InferCategory(url),
                CrawledAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to crawl {Url}", url);
            return null;
        }
    }

    public async Task<List<CrawledPage>> CrawlSiteAsync(string baseUrl, int maxPages = 50, CancellationToken ct = default)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        var results = new List<CrawledPage>();

        queue.Enqueue(baseUrl);

        while (queue.Count > 0 && results.Count < maxPages && !ct.IsCancellationRequested)
        {
            var url = queue.Dequeue();
            if (!visited.Add(url)) continue;

            var page = await CrawlPageAsync(url, ct);
            if (page != null) results.Add(page);

            // Discover child links within same domain
            var links = await ExtractLinksAsync(url, baseUrl, ct);
            foreach (var link in links.Where(l => !visited.Contains(l)))
                queue.Enqueue(link);

            await Task.Delay(300, ct); // polite crawl delay
        }

        return results;
    }

    private async Task<List<string>> ExtractLinksAsync(string pageUrl, string baseUrl, CancellationToken ct)
    {
        try
        {
            var html = await _http.GetStringAsync(pageUrl, ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var baseUri = new Uri(baseUrl);
            return doc.DocumentNode
                .SelectNodes("//a[@href]")?
                .Select(a => a.GetAttributeValue("href", ""))
                .Where(href => !string.IsNullOrEmpty(href) && !href.StartsWith('#'))
                .Select(href => href.StartsWith("http") ? href : new Uri(baseUri, href).ToString())
                .Where(u => u.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .Take(20)
                .ToList() ?? new List<string>();
        }
        catch { return new List<string>(); }
    }

    private static string CleanText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 2);

        return string.Join('\n', lines);
    }

    private static string InferCategory(string url)
    {
        var lower = url.ToLowerInvariant();
        if (lower.Contains("bins") || lower.Contains("recycling")) return "bins-recycling";
        if (lower.Contains("planning") || lower.Contains("building")) return "planning";
        if (lower.Contains("housing")) return "housing";
        if (lower.Contains("council-tax")) return "council-tax";
        if (lower.Contains("benefits")) return "benefits";
        if (lower.Contains("roads") || lower.Contains("transport")) return "transport";
        if (lower.Contains("education") || lower.Contains("school")) return "education";
        if (lower.Contains("health")) return "health";
        if (lower.Contains("parks") || lower.Contains("leisure")) return "leisure";
        return "general";
    }
}
