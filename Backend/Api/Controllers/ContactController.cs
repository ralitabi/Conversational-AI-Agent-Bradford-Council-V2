using Bradford.Core.Models;
using Bradford.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bradford.Api.Controllers;

[ApiController]
[Route("api/contact")]
[Produces("application/json")]
public class ContactController : ControllerBase
{
    private readonly AgentDbContext _db;
    private readonly AdminAuthService _auth;
    private readonly ILogger<ContactController> _logger;

    public ContactController(AgentDbContext db, AdminAuthService auth, ILogger<ContactController> logger)
    {
        _db    = db;
        _auth  = auth;
        _logger = logger;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private AdminSession? AdminSession()
    {
        var h = Request.Headers["Authorization"].FirstOrDefault() ?? "";
        var token = h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? h[7..].Trim() : null;
        return token != null ? _auth.Validate(token) : null;
    }

    private static string NewSessionId()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = new Random();
        return "BC-" + new string(Enumerable.Range(0, 6).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }

    // ── POST /api/contact/start  (citizen) ────────────────────────────────────
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartContactRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Subject))
            return BadRequest(new { error = "Name and subject are required." });

        var sessionId = NewSessionId();
        var session   = new ContactSession
        {
            Id        = sessionId,
            Name      = req.Name.Trim(),
            Email     = req.Email?.Trim(),
            Phone     = req.Phone?.Trim(),
            Subject   = req.Subject.Trim(),
            Status    = "waiting",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ContactSessions.Add(session);

        // Save the citizen's initial message
        if (!string.IsNullOrWhiteSpace(req.Message))
        {
            _db.ContactMessages.Add(new ContactMessage
            {
                SessionId  = sessionId,
                Sender     = "citizen",
                SenderName = req.Name.Trim(),
                Content    = req.Message.Trim(),
                Timestamp  = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("New contact session {Id} from {Name} — {Subject}", sessionId, req.Name, req.Subject);

        return Ok(new { sessionId, name = session.Name, subject = session.Subject });
    }

    // ── GET /api/contact/{id}/messages?after={lastId}  (citizen polls) ────────
    [HttpGet("{sessionId}/messages")]
    public async Task<IActionResult> GetMessages(string sessionId, [FromQuery] int after = 0, CancellationToken ct = default)
    {
        var session = await _db.ContactSessions.FindAsync(new object[] { sessionId }, ct);
        if (session is null) return NotFound();

        var messages = await _db.ContactMessages
            .Where(m => m.SessionId == sessionId && m.Id > after)
            .OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.Sender, m.SenderName, m.Content, m.Timestamp })
            .ToListAsync(ct);

        // Mark admin messages as read
        var unread = await _db.ContactMessages
            .Where(m => m.SessionId == sessionId && m.Sender == "admin" && !m.IsRead)
            .ToListAsync(ct);
        if (unread.Any())
        {
            unread.ForEach(m => m.IsRead = true);
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { status = session.Status, messages });
    }

    // ── POST /api/contact/{id}/message  (citizen sends) ───────────────────────
    [HttpPost("{sessionId}/message")]
    public async Task<IActionResult> CitizenSend(string sessionId, [FromBody] SendMessageRequest req, CancellationToken ct)
    {
        var session = await _db.ContactSessions.FindAsync(new object[] { sessionId }, ct);
        if (session is null) return NotFound();
        if (session.Status == "closed") return BadRequest(new { error = "This session has been closed." });
        if (string.IsNullOrWhiteSpace(req.Content)) return BadRequest(new { error = "Message cannot be empty." });

        _db.ContactMessages.Add(new ContactMessage
        {
            SessionId  = sessionId,
            Sender     = "citizen",
            SenderName = session.Name,
            Content    = req.Content.Trim(),
            Timestamp  = DateTime.UtcNow
        });
        session.Status    = session.Status == "waiting" ? "waiting" : "active";
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ADMIN ENDPOINTS (require Bearer token)
    // ═══════════════════════════════════════════════════════════════════════════

    // ── GET /api/contact/admin/sessions  (admin lists all sessions) ───────────
    [HttpGet("admin/sessions")]
    public async Task<IActionResult> AdminSessions([FromQuery] string? status = null, CancellationToken ct = default)
    {
        if (AdminSession() is null) return Unauthorized();

        var query = _db.ContactSessions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(s => s.Status == status);

        var sessions = await query.OrderByDescending(s => s.UpdatedAt).ToListAsync(ct);

        // Attach unread count per session
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var unreadCounts = await _db.ContactMessages
            .Where(m => sessionIds.Contains(m.SessionId) && m.Sender == "citizen" && !m.IsRead)
            .GroupBy(m => m.SessionId)
            .Select(g => new { sessionId = g.Key, count = g.Count() })
            .ToDictionaryAsync(x => x.sessionId, x => x.count, ct);

        // Last message per session
        var lastMessages = await _db.ContactMessages
            .Where(m => sessionIds.Contains(m.SessionId))
            .GroupBy(m => m.SessionId)
            .Select(g => new { sessionId = g.Key, preview = g.OrderByDescending(m => m.Timestamp).First().Content, lastAt = g.Max(m => m.Timestamp) })
            .ToDictionaryAsync(x => x.sessionId, ct);

        var result = sessions.Select(s => new
        {
            s.Id, s.Name, s.Email, s.Phone, s.Subject, s.Status, s.AssignedTo, s.CreatedAt, s.UpdatedAt,
            unread  = unreadCounts.TryGetValue(s.Id, out var u) ? u : 0,
            preview = lastMessages.TryGetValue(s.Id, out var lm) ? lm.preview.Substring(0, Math.Min(80, lm.preview.Length)) : "",
            lastAt  = lastMessages.TryGetValue(s.Id, out var la) ? la.lastAt : s.CreatedAt
        });

        return Ok(result);
    }

    // ── GET /api/contact/admin/{id}/messages  (admin gets chat history) ────────
    [HttpGet("admin/{sessionId}/messages")]
    public async Task<IActionResult> AdminGetMessages(string sessionId, [FromQuery] int after = 0, CancellationToken ct = default)
    {
        if (AdminSession() is null) return Unauthorized();

        var session = await _db.ContactSessions.FindAsync(new object[] { sessionId }, ct);
        if (session is null) return NotFound();

        var messages = await _db.ContactMessages
            .Where(m => m.SessionId == sessionId && m.Id > after)
            .OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.Sender, m.SenderName, m.Content, m.Timestamp })
            .ToListAsync(ct);

        // Mark citizen messages as read
        var unread = await _db.ContactMessages
            .Where(m => m.SessionId == sessionId && m.Sender == "citizen" && !m.IsRead)
            .ToListAsync(ct);
        if (unread.Any())
        {
            unread.ForEach(m => m.IsRead = true);
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { status = session.Status, messages, session = new { session.Id, session.Name, session.Email, session.Phone, session.Subject } });
    }

    // ── POST /api/contact/admin/{id}/message  (admin replies) ─────────────────
    [HttpPost("admin/{sessionId}/message")]
    public async Task<IActionResult> AdminSend(string sessionId, [FromBody] SendMessageRequest req, CancellationToken ct)
    {
        var admin = AdminSession();
        if (admin is null) return Unauthorized();

        var session = await _db.ContactSessions.FindAsync(new object[] { sessionId }, ct);
        if (session is null) return NotFound();
        if (session.Status == "closed") return BadRequest(new { error = "Session is closed." });
        if (string.IsNullOrWhiteSpace(req.Content)) return BadRequest(new { error = "Message cannot be empty." });

        _db.ContactMessages.Add(new ContactMessage
        {
            SessionId  = sessionId,
            Sender     = "admin",
            SenderName = admin.Name,
            Content    = req.Content.Trim(),
            Timestamp  = DateTime.UtcNow
        });
        session.Status    = "active";
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin [{Name}] replied to contact session {SessionId}", admin.Name, sessionId);
        return Ok(new { success = true });
    }

    // ── POST /api/contact/admin/{id}/transfer  (admin transfers session) ──────
    [HttpPost("admin/{sessionId}/transfer")]
    public async Task<IActionResult> Transfer(string sessionId, [FromBody] TransferRequest req, CancellationToken ct)
    {
        var admin = AdminSession();
        if (admin is null) return Unauthorized();

        var session = await _db.ContactSessions.FindAsync(new object[] { sessionId }, ct);
        if (session is null) return NotFound();

        var targetUser = await _db.AdminUsers
            .FirstOrDefaultAsync(u => u.Username == req.ToUsername && u.IsActive, ct);
        if (targetUser is null) return BadRequest(new { error = "Target user not found or inactive." });

        session.AssignedTo = req.ToUsername;
        session.UpdatedAt  = DateTime.UtcNow;

        _db.ContactMessages.Add(new ContactMessage
        {
            SessionId  = sessionId,
            Sender     = "system",
            SenderName = "System",
            Content    = $"Session transferred from {admin.Name} to {targetUser.Name}.",
            Timestamp  = DateTime.UtcNow,
            IsRead     = false
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Admin [{Name}] transferred session {SessionId} to {Target}", admin.Name, sessionId, req.ToUsername);
        return Ok(new { success = true, assignedTo = targetUser.Name });
    }

    // ── GET /api/contact/admin/staff  (list active admins for transfer dropdown) ─
    [HttpGet("admin/staff")]
    public async Task<IActionResult> GetStaff(CancellationToken ct)
    {
        if (AdminSession() is null) return Unauthorized();
        var users = await _db.AdminUsers
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new { u.Username, u.Name, u.Role })
            .ToListAsync(ct);
        return Ok(users);
    }

    // ── PATCH /api/contact/admin/{id}/status  (admin closes/reopens) ──────────
    [HttpPatch("admin/{sessionId}/status")]
    public async Task<IActionResult> UpdateStatus(string sessionId, [FromBody] UpdateStatusRequest req, CancellationToken ct)
    {
        var admin = AdminSession();
        if (admin is null) return Unauthorized();

        var session = await _db.ContactSessions.FindAsync(new object[] { sessionId }, ct);
        if (session is null) return NotFound();

        session.Status    = req.Status;
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Admin [{Name}] set session {SessionId} to {Status}", admin.Name, sessionId, req.Status);
        return Ok(new { success = true });
    }
}

// DTOs
public sealed class StartContactRequest  { public string? Name { get; set; } public string? Email { get; set; } public string? Phone { get; set; } public string? Subject { get; set; } public string? Message { get; set; } }
public sealed class SendMessageRequest   { public string? Content { get; set; } }
public sealed class UpdateStatusRequest  { public string Status { get; set; } = "closed"; }
public sealed class TransferRequest      { public string? ToUsername { get; set; } }
