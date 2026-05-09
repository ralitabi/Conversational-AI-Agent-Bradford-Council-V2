using Bradford.Core.Models;

namespace Bradford.Core.Interfaces;

public interface IWebCrawlerService
{
    Task<CrawledPage?> CrawlPageAsync(string url, CancellationToken ct = default);
    Task<List<CrawledPage>> CrawlSiteAsync(string baseUrl, int maxPages = 50, CancellationToken ct = default);
}
