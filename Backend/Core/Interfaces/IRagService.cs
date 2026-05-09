using Bradford.Core.Models;

namespace Bradford.Core.Interfaces;

public interface IRagService
{
    Task<RagContext> RetrieveAsync(string query, int topK = 5, CancellationToken ct = default);
}
