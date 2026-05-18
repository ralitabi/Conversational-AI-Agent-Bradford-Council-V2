using Bradford.Core.Models;
using Bradford.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bradford.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly AgentDbContext   _db;
    private readonly IConfiguration  _config;
    private readonly AdminAuthService _auth;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AgentDbContext db, IConfiguration config, AdminAuthService auth, ILogger<AdminController> logger)
    {
        _db     = db;
        _config = config;
        _auth   = auth;
        _logger = logger;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private string? BearerToken()
    {
        var h = Request.Headers["Authorization"].FirstOrDefault() ?? "";
        return h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? h[7..].Trim() : null;
    }
    private AdminSession? CurrentSession() => _auth.Validate(BearerToken() ?? "");

    private string ClientIp() =>
        Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? HttpContext.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    private async Task LogAsync(AdminSession s, string action, string? detail = null, CancellationToken ct = default)
    {
        _db.AdminActivities.Add(new AdminActivity
        {
            Username  = s.Username,
            Name      = s.Name,
            Action    = action,
            Detail    = detail,
            IpAddress = ClientIp(),
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    // ── POST /api/admin/auth ──────────────────────────────────────────────────
    [HttpPost("auth")]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest req, CancellationToken ct)
    {
        var users = _config.GetSection("AdminUsers").Get<List<AdminUserConfig>>() ?? new();
        var user  = users.FirstOrDefault(u =>
            u.Username.Equals(req.Username?.Trim(), StringComparison.OrdinalIgnoreCase) &&
            u.Password == req.Password);

        if (user is null)
        {
            _logger.LogWarning("Failed admin login for '{Username}' from {Ip}", req.Username, ClientIp());
            // Log failed attempt (no session yet — store directly)
            _db.AdminActivities.Add(new AdminActivity
            {
                Username  = req.Username?.Trim() ?? "unknown",
                Name      = "Unknown",
                Action    = "login_failed",
                Detail    = $"IP: {ClientIp()}",
                IpAddress = ClientIp(),
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { error = "Invalid username or password." });
        }

        var session = _auth.CreateSession(user.Username, user.Name, user.Role)!;

        _db.AdminActivities.Add(new AdminActivity
        {
            Username  = user.Username,
            Name      = user.Name,
            Action    = "login",
            Detail    = $"IP: {ClientIp()}",
            IpAddress = ClientIp(),
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Admin login: {Name} ({Username}) from {Ip}", user.Name, user.Username, ClientIp());

        return Ok(new { token = session.Token, name = session.Name, username = session.Username, role = session.Role, expiresAt = session.ExpiresAt });
    }

    // ── POST /api/admin/logout ────────────────────────────────────────────────
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var s = CurrentSession();
        if (s != null)
        {
            await LogAsync(s, "logout", null, ct);
            _auth.Revoke(BearerToken()!);
            _logger.LogInformation("Admin logout: {Name}", s.Name);
        }
        return Ok();
    }

    // ── GET /api/admin/me ─────────────────────────────────────────────────────
    [HttpGet("me")]
    public IActionResult Me()
    {
        var s = CurrentSession();
        if (s is null) return Unauthorized();
        return Ok(new { s.Name, s.Username, s.Role, s.ExpiresAt });
    }

    // ── GET /api/admin/stats ──────────────────────────────────────────────────
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        if (CurrentSession() is null) return Unauthorized();

        var today = DateTime.UtcNow.Date;
        var week  = DateTime.UtcNow.Date.AddDays(-6);

        var totalMessages = await _db.ConversationTurns.CountAsync(ct);
        var totalSessions = await _db.ConversationTurns.Select(t => t.SessionId).Distinct().CountAsync(ct);
        var msgsToday     = await _db.ConversationTurns.CountAsync(t => t.Timestamp >= today, ct);
        var sessionsToday = await _db.ConversationTurns.Where(t => t.Timestamp >= today).Select(t => t.SessionId).Distinct().CountAsync(ct);
        var msgsThisWeek  = await _db.ConversationTurns.CountAsync(t => t.Timestamp >= week, ct);
        var userMessages  = await _db.ConversationTurns.CountAsync(t => t.Role == "user", ct);

        return Ok(new
        {
            totalMessages, totalSessions, msgsToday, sessionsToday, msgsThisWeek, userMessages,
            avgMsgsPerSession = totalSessions > 0 ? Math.Round((double)userMessages / totalSessions, 1) : 0
        });
    }

    // ── GET /api/admin/sessions ───────────────────────────────────────────────
    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions([FromQuery] int page = 1, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var s = CurrentSession();
        if (s is null) return Unauthorized();
        await LogAsync(s, "view_conversations", search != null ? $"search={search}" : null, ct);

        const int pageSize = 25;
        var query = _db.ConversationTurns.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.SessionId.Contains(search) || t.Content.Contains(search));

        var sessions = await query
            .GroupBy(t => t.SessionId)
            .Select(g => new
            {
                sessionId    = g.Key,
                messageCount = g.Count(),
                userMessages = g.Count(t => t.Role == "user"),
                firstSeen    = g.Min(t => t.Timestamp),
                lastSeen     = g.Max(t => t.Timestamp),
                preview      = g.Where(t => t.Role == "user").OrderBy(t => t.Timestamp).Select(t => t.Content).FirstOrDefault() ?? ""
            })
            .OrderByDescending(s => s.lastSeen)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var total = await query.Select(t => t.SessionId).Distinct().CountAsync(ct);
        return Ok(new { sessions, totalSessions = total, page, pageSize, totalPages = (int)Math.Ceiling((double)total / pageSize) });
    }

    // ── GET /api/admin/sessions/{id} ──────────────────────────────────────────
    [HttpGet("sessions/{sessionId}")]
    public async Task<IActionResult> SessionDetail(string sessionId, CancellationToken ct)
    {
        var s = CurrentSession();
        if (s is null) return Unauthorized();
        await LogAsync(s, "view_session", sessionId, ct);

        var turns = await _db.ConversationTurns
            .Where(t => t.SessionId == sessionId).OrderBy(t => t.Timestamp)
            .Select(t => new { t.Role, t.Content, t.Timestamp }).ToListAsync(ct);

        if (turns.Count == 0) return NotFound();
        return Ok(new { sessionId, turns });
    }

    // ── DELETE /api/admin/sessions/{id} ──────────────────────────────────────
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(string sessionId, CancellationToken ct)
    {
        var s = CurrentSession();
        if (s is null) return Unauthorized();

        var deleted = await _db.ConversationTurns.Where(t => t.SessionId == sessionId).ExecuteDeleteAsync(ct);
        await LogAsync(s, "delete_session", sessionId, ct);
        _logger.LogInformation("Admin [{Name}] deleted session {SessionId}", s.Name, sessionId);
        return Ok(new { deleted });
    }

    // ── DELETE /api/admin/sessions — clear all ────────────────────────────────
    [HttpDelete("sessions")]
    public async Task<IActionResult> DeleteAllSessions(CancellationToken ct)
    {
        var s = CurrentSession();
        if (s is null) return Unauthorized();

        var deleted = await _db.ConversationTurns.ExecuteDeleteAsync(ct);
        await LogAsync(s, "delete_all", $"{deleted} turns removed", ct);
        _logger.LogInformation("Admin [{Name}] cleared ALL sessions ({Count} turns)", s.Name, deleted);
        return Ok(new { deleted });
    }

    // ── GET /api/admin/analytics ──────────────────────────────────────────────
    [HttpGet("analytics")]
    public async Task<IActionResult> Analytics([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var s = CurrentSession();
        if (s is null) return Unauthorized();
        await LogAsync(s, "view_analytics", $"days={days}", ct);

        var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var allTurns = await _db.ConversationTurns
            .Where(t => t.Timestamp >= since)
            .Select(t => new { t.Role, t.Timestamp, t.SessionId })
            .ToListAsync(ct);

        var trends = Enumerable.Range(0, days).Select(i => since.AddDays(i)).Select(date => new
        {
            date     = date.ToString("yyyy-MM-dd"),
            messages = allTurns.Count(t => t.Role == "user" && t.Timestamp.Date == date),
            sessions = allTurns.Where(t => t.Timestamp.Date == date).Select(t => t.SessionId).Distinct().Count()
        }).ToList();

        var allUserTurns = await _db.ConversationTurns
            .Where(t => t.Role == "user")
            .Select(t => new { t.Timestamp, t.Content })
            .ToListAsync(ct);

        var peakHours = Enumerable.Range(0, 24).Select(h => new
        {
            hour  = h,
            label = h == 0 ? "12am" : h < 12 ? $"{h}am" : h == 12 ? "12pm" : $"{h - 12}pm",
            count = allUserTurns.Count(t => t.Timestamp.Hour == h)
        }).ToList();

        var topicRules = new List<(string Topic, string[] Keywords)>
        {
            ("Council Tax",         new[]{"council tax","tax band","band","ctr","council tax reduction","pay my tax","my band"}),
            ("Bins & Recycling",    new[]{"bin","recycling","collection","waste","grey bin","green bin","brown bin","missed bin","rubbish"}),
            ("Housing",             new[]{"housing","homeless","house","rent","landlord","tenant","bradford homes","incommunities","evict","accommodation"}),
            ("Benefits",            new[]{"benefit","housing benefit","universal credit","free school meal","crisis fund","food bank","pip","dwp","welfare"}),
            ("Schools & Education", new[]{"school","primary","secondary","academy","ofsted","admissions","nursery","sixth form","enrol","term date","send","ehcp"}),
            ("Libraries",           new[]{"library","libraries","book","join library","library card","borrowbox"}),
            ("Planning & Building", new[]{"planning","planning permission","extension","building regs","building control","permitted","loft","conservatory","demolish"}),
            ("Transport & Roads",   new[]{"parking","pothole","blue badge","bus pass","road","roadworks","gritting","fly tip","street light","cycling","pcn","taxi"}),
            ("Health & Wellbeing",  new[]{"health","mental health","anxiety","depression","vaccine","alcohol","drugs","sexual health","smoking","obesity","gp"}),
            ("Sports & Leisure",    new[]{"sport","leisure","gym","swimming","pool","fitness","sports centre","leisure card","badminton","football","dance"}),
            ("Adult Social Care",   new[]{"social care","home care","carer","care assessment","occupational therapy","safeguarding","direct payment","care home","dementia"}),
            ("Children & Families", new[]{"child","children","family","fostering","childminder","ehcp","send","family hub","report concern"}),
            ("Business",            new[]{"business","procurement","tender","rates","commercial","entrepreneur","training course","fire safety","health and safety"}),
            ("Environment",         new[]{"dog","footpath","right of way","conservation","listed building","biodiversity","saltaire","climate","flood","countryside"}),
            ("Address & Postcode",  new[]{"postcode","address","live at","bd1","bd2","bd3","bd4","bd5","bd6","bd7","bd8","bd9","bd10"}),
        };

        var topicCounts = topicRules.Select(rule => new
        {
            topic = rule.Topic,
            count = allUserTurns.Count(t => rule.Keywords.Any(k => t.Content.Contains(k, StringComparison.OrdinalIgnoreCase)))
        }).Where(x => x.count > 0).OrderByDescending(x => x.count).ToList();

        var dowLabels = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        var dayOfWeek = Enumerable.Range(0, 7).Select(d => new
        {
            day   = dowLabels[d],
            count = allUserTurns.Count(t => (int)t.Timestamp.DayOfWeek == d)
        }).ToList();

        var sessionCounts = await _db.ConversationTurns
            .GroupBy(t => t.SessionId)
            .Select(g => new { count = g.Count(t => t.Role == "user") })
            .ToListAsync(ct);

        var engaged = sessionCounts.Count(s => s.count >= 3);
        var single  = sessionCounts.Count(s => s.count == 1);
        var total   = sessionCounts.Count;

        return Ok(new
        {
            trends, topicCounts, peakHours, dayOfWeek,
            engagement = new { total, singleQuery = single, engaged, engagedPct = total > 0 ? Math.Round((double)engaged / total * 100, 1) : 0 }
        });
    }

    // ── GET /api/admin/profile ───────────────────────────────────────────────
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var s = CurrentSession();
        if (s is null) return Unauthorized();

        var profile = await _db.AdminProfiles.FirstOrDefaultAsync(p => p.Username == s.Username, ct);
        return Ok(new
        {
            username    = s.Username,
            name        = s.Name,
            role        = s.Role,
            displayName = profile?.DisplayName ?? s.Name,
            bio         = profile?.Bio ?? "",
            hasAvatar   = !string.IsNullOrEmpty(profile?.AvatarBase64),
            avatarDataUrl = string.IsNullOrEmpty(profile?.AvatarBase64)
                ? null
                : $"data:{profile.AvatarMime ?? "image/jpeg"};base64,{profile.AvatarBase64}"
        });
    }

    // ── PUT /api/admin/profile ───────────────────────────────────────────────
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        var s = CurrentSession();
        if (s is null) return Unauthorized();

        var profile = await _db.AdminProfiles.FirstOrDefaultAsync(p => p.Username == s.Username, ct);
        if (profile is null)
        {
            profile = new AdminProfile { Username = s.Username };
            _db.AdminProfiles.Add(profile);
        }

        if (req.DisplayName is not null) profile.DisplayName = req.DisplayName.Trim();
        if (req.Bio         is not null) profile.Bio         = req.Bio.Trim();
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await LogAsync(s, "update_profile", null, ct);
        return Ok(new { success = true, displayName = profile.DisplayName, bio = profile.Bio });
    }

    // ── POST /api/admin/profile/avatar ───────────────────────────────────────
    [HttpPost("profile/avatar")]
    public async Task<IActionResult> UploadAvatar([FromBody] UploadAvatarRequest req, CancellationToken ct)
    {
        var s = CurrentSession();
        if (s is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Base64))
            return BadRequest(new { error = "No image data provided." });

        // Validate mime type
        var mime = req.MimeType?.ToLower() ?? "image/jpeg";
        if (!new[] { "image/jpeg", "image/png", "image/webp", "image/gif" }.Contains(mime))
            return BadRequest(new { error = "Unsupported image type." });

        // Limit size to ~800KB base64
        if (req.Base64.Length > 1_100_000)
            return BadRequest(new { error = "Image too large. Please use an image under 600KB." });

        var profile = await _db.AdminProfiles.FirstOrDefaultAsync(p => p.Username == s.Username, ct);
        if (profile is null)
        {
            profile = new AdminProfile { Username = s.Username, DisplayName = s.Name };
            _db.AdminProfiles.Add(profile);
        }

        profile.AvatarBase64 = req.Base64;
        profile.AvatarMime   = mime;
        profile.UpdatedAt    = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await LogAsync(s, "update_avatar", null, ct);
        return Ok(new { success = true, avatarDataUrl = $"data:{mime};base64,{req.Base64}" });
    }

    // ── DELETE /api/admin/profile/avatar ─────────────────────────────────────
    [HttpDelete("profile/avatar")]
    public async Task<IActionResult> DeleteAvatar(CancellationToken ct)
    {
        var s = CurrentSession();
        if (s is null) return Unauthorized();

        var profile = await _db.AdminProfiles.FirstOrDefaultAsync(p => p.Username == s.Username, ct);
        if (profile is not null)
        {
            profile.AvatarBase64 = null;
            profile.AvatarMime   = null;
            profile.UpdatedAt    = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return Ok(new { success = true });
    }

    // ── GET /api/admin/profiles  [superadmin — all staff profiles] ────────────
    [HttpGet("profiles")]
    public async Task<IActionResult> AllProfiles(CancellationToken ct)
    {
        var s = CurrentSession();
        if (s is null) return Unauthorized();
        if (s.Role != "superadmin") return StatusCode(403, new { error = "Superadmin access required." });

        var profiles = await _db.AdminProfiles.ToListAsync(ct);
        return Ok(profiles.Select(p => new
        {
            p.Username, p.DisplayName, p.Bio, p.UpdatedAt,
            hasAvatar    = !string.IsNullOrEmpty(p.AvatarBase64),
            avatarDataUrl = string.IsNullOrEmpty(p.AvatarBase64)
                ? null
                : $"data:{p.AvatarMime ?? "image/jpeg"};base64,{p.AvatarBase64}"
        }));
    }

    // ── GET /api/admin/staff  [superadmin only] ───────────────────────────────
    [HttpGet("staff")]
    public async Task<IActionResult> Staff(CancellationToken ct)
    {
        var s = CurrentSession();
        if (s is null) return Unauthorized();
        if (s.Role != "superadmin") return StatusCode(403, new { error = "Superadmin access required." });

        var cutoff = DateTime.UtcNow.AddDays(-30);

        var activity = await _db.AdminActivities
            .Where(a => a.Timestamp >= cutoff)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new { a.Username, a.Name, a.Action, a.Detail, a.IpAddress, a.Timestamp })
            .ToListAsync(ct);

        // Per-admin performance summary
        var allActivity = await _db.AdminActivities
            .Select(a => new { a.Username, a.Name, a.Action, a.Timestamp })
            .ToListAsync(ct);

        var staff = allActivity
            .GroupBy(a => a.Username)
            .Select(g => new
            {
                username       = g.Key,
                name           = g.First().Name,
                totalLogins    = g.Count(a => a.Action == "login"),
                failedLogins   = g.Count(a => a.Action == "login_failed"),
                sessionsViewed = g.Count(a => a.Action == "view_session"),
                sessionsDeleted= g.Count(a => a.Action == "delete_session"),
                deleteAlls     = g.Count(a => a.Action == "delete_all"),
                analyticsViews = g.Count(a => a.Action == "view_analytics"),
                lastLogin      = g.Where(a => a.Action == "login").OrderByDescending(a => a.Timestamp).Select(a => a.Timestamp).FirstOrDefault(),
                lastActive     = g.OrderByDescending(a => a.Timestamp).First().Timestamp,
                totalActions   = g.Count()
            })
            .OrderByDescending(a => a.lastActive)
            .ToList();

        return Ok(new { staff, recentActivity = activity.Take(50) });
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────
public sealed class AdminLoginRequest  { public string? Username { get; set; } public string? Password { get; set; } }
public sealed class AdminUserConfig    { public string Username { get; set; } = ""; public string Password { get; set; } = ""; public string Name { get; set; } = ""; public string Role { get; set; } = "admin"; }
public sealed class UpdateProfileRequest { public string? DisplayName { get; set; } public string? Bio { get; set; } }
public sealed class UploadAvatarRequest  { public string? Base64 { get; set; } public string? MimeType { get; set; } }
