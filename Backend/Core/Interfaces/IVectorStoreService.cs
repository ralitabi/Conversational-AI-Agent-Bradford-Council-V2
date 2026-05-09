using Bradford.Core.Models;

namespace Bradford.Core.Interfaces;

public interface IVectorStoreService
{
    Task EnsureCollectionAsync(CancellationToken ct = default);
    Task UpsertChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken ct = default);
    Task<List<RetrievedChunk>> SearchAsync(float[] queryVector, int topK = 5, CancellationToken ct = default);
    Task DeleteByUrlAsync(string sourceUrl, CancellationToken ct = default);
}
