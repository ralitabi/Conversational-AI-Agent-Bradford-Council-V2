using Bradford.Core.Interfaces;
using Bradford.Core.Models;
using Microsoft.Extensions.Logging;

namespace Bradford.Core.Services;

public class IngestionService : IIngestionService
{
    private readonly IWebCrawlerService _crawler;
    private readonly IEmbeddingService _embeddings;
    private readonly IVectorStoreService _vectorStore;
    private readonly ILogger<IngestionService> _logger;

    // Bradford Council pages to index — sourced from https://www.bradford.gov.uk/
    private static readonly string[] DefaultCouncilUrls =
    {
        // Home
        "https://www.bradford.gov.uk/",

        // Bins & Recycling
        "https://www.bradford.gov.uk/bins-and-recycling/",
        "https://www.bradford.gov.uk/bins-and-recycling/find-your-bin-collection-day/",
        "https://www.bradford.gov.uk/bins-and-recycling/what-goes-in-which-bin/",
        "https://www.bradford.gov.uk/bins-and-recycling/missed-bin-collections/",
        "https://www.bradford.gov.uk/bins-and-recycling/recycling-centres/",
        "https://www.bradford.gov.uk/bins-and-recycling/bulky-waste-collections/",
        "https://www.bradford.gov.uk/bins-and-recycling/garden-waste-collections/",

        // Council Tax
        "https://www.bradford.gov.uk/council-tax/",
        "https://www.bradford.gov.uk/council-tax/paying-your-council-tax/",
        "https://www.bradford.gov.uk/council-tax/council-tax-discounts-and-exemptions/",
        "https://www.bradford.gov.uk/council-tax/council-tax-support/",
        "https://www.bradford.gov.uk/council-tax/moving-home/",
        "https://www.bradford.gov.uk/council-tax/council-tax-bands-and-rates/",

        // Housing
        "https://www.bradford.gov.uk/housing/",
        "https://www.bradford.gov.uk/housing/council-housing/",
        "https://www.bradford.gov.uk/housing/homelessness/",
        "https://www.bradford.gov.uk/housing/housing-benefit/",
        "https://www.bradford.gov.uk/housing/home-improvements-and-adaptations/",
        "https://www.bradford.gov.uk/housing/private-rented-housing/",

        // Planning & Building
        "https://www.bradford.gov.uk/planning-and-building-control/",
        "https://www.bradford.gov.uk/planning-and-building-control/planning-applications/",
        "https://www.bradford.gov.uk/planning-and-building-control/building-regulations/",
        "https://www.bradford.gov.uk/planning-and-building-control/permitted-development/",

        // Benefits & Financial Support
        "https://www.bradford.gov.uk/benefits-and-financial-support/",
        "https://www.bradford.gov.uk/benefits-and-financial-support/free-school-meals/",
        "https://www.bradford.gov.uk/benefits-and-financial-support/discretionary-housing-payments/",
        "https://www.bradford.gov.uk/benefits-and-financial-support/crisis-support/",

        // Roads & Transport
        "https://www.bradford.gov.uk/roads-and-transport/",
        "https://www.bradford.gov.uk/roads-and-transport/potholes-and-road-defects/",
        "https://www.bradford.gov.uk/roads-and-transport/parking/",
        "https://www.bradford.gov.uk/roads-and-transport/roadworks/",
        "https://www.bradford.gov.uk/roads-and-transport/travel-bradford/",
        "https://www.bradford.gov.uk/roads-and-transport/blue-badge/",

        // Education
        "https://www.bradford.gov.uk/education-and-learning/",
        "https://www.bradford.gov.uk/education-and-learning/school-admissions/",
        "https://www.bradford.gov.uk/education-and-learning/schools-in-bradford/",
        "https://www.bradford.gov.uk/education-and-learning/early-years-and-childcare/",
        "https://www.bradford.gov.uk/education-and-learning/special-educational-needs/",

        // Health & Social Care
        "https://www.bradford.gov.uk/health-and-wellbeing/",
        "https://www.bradford.gov.uk/health-and-wellbeing/adult-social-care/",
        "https://www.bradford.gov.uk/health-and-wellbeing/childrens-social-care/",
        "https://www.bradford.gov.uk/health-and-wellbeing/mental-health/",

        // Parks & Leisure
        "https://www.bradford.gov.uk/parks-and-open-spaces/",
        "https://www.bradford.gov.uk/parks-and-open-spaces/parks-in-bradford/",
        "https://www.bradford.gov.uk/sport-and-leisure/",
        "https://www.bradford.gov.uk/libraries/",

        // Life Events
        "https://www.bradford.gov.uk/births-deaths-marriages/",
        "https://www.bradford.gov.uk/births-deaths-marriages/register-a-birth/",
        "https://www.bradford.gov.uk/births-deaths-marriages/register-a-death/",
        "https://www.bradford.gov.uk/births-deaths-marriages/marriage-and-civil-partnership/",

        // Business & Planning
        "https://www.bradford.gov.uk/business/",
        "https://www.bradford.gov.uk/business/licensing/",
        "https://www.bradford.gov.uk/business/environmental-health/",

        // Contact & About
        "https://www.bradford.gov.uk/contact-us/",
        "https://www.bradford.gov.uk/about-the-council/",
        "https://www.bradford.gov.uk/about-the-council/councillors/",
    };

    public IngestionService(
        IWebCrawlerService crawler,
        IEmbeddingService embeddings,
        IVectorStoreService vectorStore,
        ILogger<IngestionService> logger)
    {
        _crawler = crawler;
        _embeddings = embeddings;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestAllCouncilPagesAsync(CancellationToken ct = default)
    {
        return await IngestUrlsAsync(new IngestionRequest
        {
            Urls = DefaultCouncilUrls.ToList(),
            ForceReindex = false
        }, ct);
    }

    public async Task<IngestionResult> IngestUrlsAsync(IngestionRequest request, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        await _vectorStore.EnsureCollectionAsync(ct);

        var result = new IngestionResult();

        foreach (var url in request.Urls)
        {
            try
            {
                _logger.LogInformation("Crawling {Url}", url);
                var page = await _crawler.CrawlPageAsync(url, ct);
                if (page == null || string.IsNullOrWhiteSpace(page.PlainText)) continue;

                if (request.ForceReindex)
                    await _vectorStore.DeleteByUrlAsync(url, ct);

                var chunks = ChunkText(page);
                if (chunks.Count == 0) continue;

                // Batch embed for efficiency
                var texts = chunks.Select(c => c.Content).ToList();
                var embeddings = await _embeddings.EmbedBatchAsync(texts, ct);

                for (int i = 0; i < chunks.Count; i++)
                    chunks[i].Embedding = embeddings[i];

                await _vectorStore.UpsertChunksAsync(chunks, ct);

                result.PagesProcessed++;
                result.ChunksIndexed += chunks.Count;
                _logger.LogInformation("Indexed {Count} chunks from {Url}", chunks.Count, url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest {Url}", url);
                result.Errors.Add($"{url}: {ex.Message}");
            }
        }

        result.Duration = DateTime.UtcNow - started;
        return result;
    }

    private List<DocumentChunk> ChunkText(CrawledPage page)
    {
        const int chunkSize = 600;     // characters
        const int overlap = 100;

        var text = page.PlainText.Trim();
        if (text.Length < 50) return new List<DocumentChunk>();

        var chunks = new List<DocumentChunk>();
        int start = 0;

        while (start < text.Length)
        {
            int end = Math.Min(start + chunkSize, text.Length);

            // Try to break at a sentence boundary
            if (end < text.Length)
            {
                int sentenceEnd = text.LastIndexOfAny(new[] { '.', '!', '?' }, end, Math.Min(100, end - start));
                if (sentenceEnd > start + overlap)
                    end = sentenceEnd + 1;
            }

            var chunkText = text[start..end].Trim();
            if (chunkText.Length > 30)
            {
                chunks.Add(new DocumentChunk
                {
                    Content = chunkText,
                    SourceUrl = page.Url,
                    Title = page.Title,
                    Category = page.Category
                });
            }

            start = end - overlap;
            if (start >= text.Length - overlap) break;
        }

        return chunks;
    }
}
