using Bradford.Core.Interfaces;
using Bradford.Core.Models;
using Microsoft.Extensions.Logging;

namespace Bradford.Core.Services;

public class RagService : IRagService
{
    private readonly IEmbeddingService _embeddings;
    private readonly IVectorStoreService _vectorStore;
    private readonly ILogger<RagService> _logger;

    public RagService(IEmbeddingService embeddings, IVectorStoreService vectorStore, ILogger<RagService> logger)
    {
        _embeddings = embeddings;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<RagContext> RetrieveAsync(string query, int topK = 5, CancellationToken ct = default)
    {
        _logger.LogInformation("RAG retrieval for query: {Query}", query);

        var queryVector = await _embeddings.EmbedAsync(query, ct);
        var chunks      = await _vectorStore.SearchAsync(queryVector, topK, ct);

        const float MinScore = 0.65f;
        var filtered = chunks.Where(c => c.Score >= MinScore).ToList();

        _logger.LogInformation(
            "RAG: {Total} chunks retrieved, {Kept} passed threshold ({Min}), top score: {Score:F3}",
            chunks.Count, filtered.Count, MinScore, chunks.FirstOrDefault()?.Score ?? 0);

        return new RagContext { Chunks = filtered };
    }
}
