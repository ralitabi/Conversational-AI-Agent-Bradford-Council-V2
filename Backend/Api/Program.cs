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

// CORS — allow Vercel (prod), Netlify (legacy), localhost, and null origin (file:// local frontend)
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                "https://bradford-council-ai.vercel.app",
                "https://golden-pika-2ca976.netlify.app",
                "http://localhost:5000",
                "http://127.0.0.1:5000",
                "null"   // file:// pages send Origin: null
              )
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
