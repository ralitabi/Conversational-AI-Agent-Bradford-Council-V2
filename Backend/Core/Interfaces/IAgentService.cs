using Bradford.Core.Models;

namespace Bradford.Core.Interfaces;

public interface IAgentService
{
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default);
    IAsyncEnumerable<string> ChatStreamAsync(ChatRequest request, CancellationToken ct = default);
}
