using Bradford.Core.Interfaces;
using Bradford.Core.Models;
using Microsoft.Extensions.Logging;

namespace Bradford.Core.Services;

public class ConversationService : IConversationService
{
    private readonly IConversationRepository _repo;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(IConversationRepository repo, ILogger<ConversationService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<List<LlmMessage>> GetHistoryAsync(string sessionId, int maxTurns = 20, CancellationToken ct = default)
    {
        var turns = await _repo.GetRecentTurnsAsync(sessionId, maxTurns * 2, ct);
        return turns.Select(t => new LlmMessage { Role = t.Role, Content = t.Content }).ToList();
    }

    public async Task SaveTurnAsync(string sessionId, string role, string content, CancellationToken ct = default)
    {
        await _repo.AddTurnAsync(new ConversationTurn
        {
            SessionId = sessionId,
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        }, ct);
    }

    public async Task ClearSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await _repo.ClearSessionAsync(sessionId, ct);
        _logger.LogInformation("Cleared session {SessionId}", sessionId);
    }
}

public interface IConversationRepository
{
    Task<List<ConversationTurn>> GetRecentTurnsAsync(string sessionId, int limit, CancellationToken ct);
    Task AddTurnAsync(ConversationTurn turn, CancellationToken ct);
    Task ClearSessionAsync(string sessionId, CancellationToken ct);
}
