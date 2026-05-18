using Bradford.Core.Models;
using Bradford.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Bradford.Infrastructure.Data;

public class AgentDbContext : DbContext
{
    public AgentDbContext(DbContextOptions<AgentDbContext> options) : base(options) { }

    public DbSet<ConversationTurn> ConversationTurns => Set<ConversationTurn>();
    public DbSet<AdminActivity>    AdminActivities   => Set<AdminActivity>();
    public DbSet<AdminProfile>     AdminProfiles     => Set<AdminProfile>();
    public DbSet<ContactSession>   ContactSessions   => Set<ContactSession>();
    public DbSet<ContactMessage>   ContactMessages   => Set<ContactMessage>();
    public DbSet<AdminUser>        AdminUsers        => Set<AdminUser>();
    public DbSet<ContactFeedback>  ContactFeedback   => Set<ContactFeedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationTurn>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.SessionId);
            e.HasIndex(t => t.Timestamp);
            e.Property(t => t.Role).HasMaxLength(20);
        });
        modelBuilder.Entity<AdminActivity>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.Username);
            e.HasIndex(a => a.Timestamp);
            e.Property(a => a.Action).HasMaxLength(30);
        });
        modelBuilder.Entity<AdminProfile>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.Username).IsUnique();
        });
        modelBuilder.Entity<ContactSession>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.Status);
            e.HasIndex(s => s.CreatedAt);
        });
        modelBuilder.Entity<ContactMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.SessionId);
            e.HasIndex(m => m.Timestamp);
        });
        modelBuilder.Entity<AdminUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Username).IsUnique();
        });
        modelBuilder.Entity<ContactFeedback>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => f.SessionId);
            e.HasIndex(f => f.AdminUsername);
        });
    }
}

public class ConversationRepository : IConversationRepository
{
    private readonly AgentDbContext _db;

    public ConversationRepository(AgentDbContext db)
    {
        _db = db;
    }

    public async Task<List<ConversationTurn>> GetRecentTurnsAsync(string sessionId, int limit, CancellationToken ct)
    {
        return await _db.ConversationTurns
            .Where(t => t.SessionId == sessionId)
            .OrderByDescending(t => t.Timestamp)
            .Take(limit)
            .OrderBy(t => t.Timestamp)
            .ToListAsync(ct);
    }

    public async Task AddTurnAsync(ConversationTurn turn, CancellationToken ct)
    {
        _db.ConversationTurns.Add(turn);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClearSessionAsync(string sessionId, CancellationToken ct)
    {
        var turns = _db.ConversationTurns.Where(t => t.SessionId == sessionId);
        _db.ConversationTurns.RemoveRange(turns);
        await _db.SaveChangesAsync(ct);
    }
}
