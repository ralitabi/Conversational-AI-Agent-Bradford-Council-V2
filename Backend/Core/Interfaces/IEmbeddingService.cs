namespace Bradford.Core.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<List<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default);
    int Dimensions { get; }
}
