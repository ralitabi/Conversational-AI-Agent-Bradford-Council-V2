using Bradford.Core.Interfaces;
using Bradford.Core.Services;
using Bradford.Infrastructure.Crawlers;
using Bradford.Infrastructure.Data;
using Bradford.Infrastructure.Tools;
using Bradford.Infrastructure.VectorStore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Load appsettings.Local.json for local development (gitignored — safe for real keys)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

var config = builder.Configuration;

// ── HTTP Clients ───────────────────────────────────────────────────────────
builder.Services.AddTransient<OpenAiRetryHandler>();
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config["OpenAI:ApiKey"]}");
    client.Timeout = TimeSpan.FromMinutes(3);
}).AddHttpMessageHandler<OpenAiRetryHandler>();

builder.Services.AddHttpClient("Crawler", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent",
        "BradfordCouncilAI/1.0 (council service assistant)");
    client.Timeout = TimeSpan.FromSeconds(45);
});

// ── Database ───────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AgentDbContext>(opt =>
    opt.UseSqlite(config.GetConnectionString("Default")));

// ── Core Services ──────────────────────────────────────────────────────────
builder.Services.AddScoped<ILlmService, LlmService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<IConversationService, ConversationService>();

// ── Infrastructure ─────────────────────────────────────────────────────────
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IVectorStoreService, QdrantVectorStore>();
builder.Services.AddScoped<IWebCrawlerService, CouncilWebCrawler>();
builder.Services.AddScoped<IToolService, CouncilToolService>();
builder.Services.AddMemoryCache(opt => opt.SizeLimit = 500); // max 500 cached lookups
builder.Services.AddSingleton<AdminAuthService>();
builder.Services.AddHostedService<SessionCleanupService>();

// ── API & Swagger ──────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Bradford Council AI Agent",
        Version = "v1",
        Description = "Conversational AI with RAG over Bradford Council services"
    });
});

// CORS — allow any origin (API is secured by bearer tokens)
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
    ));

// Rate limiting — 30 requests per minute per IP (prevents runaway API cost)
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        // X-Forwarded-For must be checked first — on Railway, RemoteIpAddress
        // is always the proxy's internal IP, not the real client IP.
        var ip = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                 ?? ctx.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit       = 30,
            Window            = TimeSpan.FromMinutes(1)
        });
    });
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsync("Rate limit exceeded — please wait a moment before trying again.", ct);
    };
});

var app = builder.Build();

// ── DB Auto-Migrate ────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
    await db.Database.EnsureCreatedAsync();
    // Create AdminActivity table if it doesn't exist yet (added after initial schema)
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "AdminActivities" (
            "Id"        INTEGER NOT NULL CONSTRAINT "PK_AdminActivities" PRIMARY KEY AUTOINCREMENT,
            "Username"  TEXT    NOT NULL,
            "Name"      TEXT    NOT NULL,
            "Action"    TEXT    NOT NULL,
            "Detail"    TEXT,
            "IpAddress" TEXT,
            "Timestamp" TEXT    NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_AdminActivities_Username"  ON "AdminActivities" ("Username");
        CREATE INDEX IF NOT EXISTS "IX_AdminActivities_Timestamp" ON "AdminActivities" ("Timestamp");
        CREATE TABLE IF NOT EXISTS "AdminProfiles" (
            "Id"           INTEGER NOT NULL CONSTRAINT "PK_AdminProfiles" PRIMARY KEY AUTOINCREMENT,
            "Username"     TEXT    NOT NULL,
            "DisplayName"  TEXT    NOT NULL DEFAULT '',
            "Bio"          TEXT    NOT NULL DEFAULT '',
            "AvatarBase64" TEXT,
            "AvatarMime"   TEXT,
            "UpdatedAt"    TEXT    NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminProfiles_Username" ON "AdminProfiles" ("Username");
        CREATE TABLE IF NOT EXISTS "ContactSessions" (
            "Id"        TEXT NOT NULL CONSTRAINT "PK_ContactSessions" PRIMARY KEY,
            "Name"      TEXT NOT NULL,
            "Email"     TEXT,
            "Phone"     TEXT,
            "Subject"   TEXT NOT NULL,
            "Status"    TEXT NOT NULL DEFAULT 'waiting',
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_ContactSessions_Status"    ON "ContactSessions" ("Status");
        CREATE INDEX IF NOT EXISTS "IX_ContactSessions_CreatedAt" ON "ContactSessions" ("CreatedAt");
        CREATE TABLE IF NOT EXISTS "ContactMessages" (
            "Id"         INTEGER NOT NULL CONSTRAINT "PK_ContactMessages" PRIMARY KEY AUTOINCREMENT,
            "SessionId"  TEXT    NOT NULL,
            "Sender"     TEXT    NOT NULL,
            "SenderName" TEXT    NOT NULL,
            "Content"    TEXT    NOT NULL,
            "IsRead"     INTEGER NOT NULL DEFAULT 0,
            "Timestamp"  TEXT    NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_ContactMessages_SessionId" ON "ContactMessages" ("SessionId");
        CREATE INDEX IF NOT EXISTS "IX_ContactMessages_Timestamp" ON "ContactMessages" ("Timestamp");
        """);
}

// ── Middleware ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bradford Council AI v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseRateLimiter();
app.UseCors();
app.UseRouting();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
