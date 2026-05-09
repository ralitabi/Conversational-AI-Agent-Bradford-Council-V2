using Bradford.Core.Models;

namespace Bradford.Core.Interfaces;

public interface IConversationService
{
    Task<List<LlmMessage>> GetHistoryAsync(string sessionId, int maxTurns = 20, CancellationToken ct = default);
    Task SaveTurnAsync(string sessionId, string role, string content, CancellationToken ct = default);
    Task ClearSessionAsync(string sessionId, CancellationToken ct = default);
}
