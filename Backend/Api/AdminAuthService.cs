using System.Collections.Concurrent;

/// <summary>
/// In-memory admin session store. Tokens expire after 8 hours (one working day).
/// Registered as a singleton so sessions survive across requests.
/// </summary>
public sealed class AdminAuthService
{
    private readonly ConcurrentDictionary<string, AdminSession> _sessions = new();

    public AdminSession? CreateSession(string username, string name, string role)
    {
        CleanupExpired();
        var token   = Guid.NewGuid().ToString("N");
        var session = new AdminSession(token, username, name, role, DateTime.UtcNow.AddHours(8));
        _sessions[token] = session;
        return session;
    }

    public AdminSession? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        if (!_sessions.TryGetValue(token, out var session)) return null;
        if (session.ExpiresAt <= DateTime.UtcNow) { _sessions.TryRemove(token, out _); return null; }
        return session;
    }

    public void Revoke(string token) => _sessions.TryRemove(token, out _);

    private void CleanupExpired()
    {
        foreach (var kv in _sessions)
            if (kv.Value.ExpiresAt <= DateTime.UtcNow)
                _sessions.TryRemove(kv.Key, out _);
    }
}

public sealed record AdminSession(
    string Token,
    string Username,
    string Name,
    string Role,
    DateTime ExpiresAt);
