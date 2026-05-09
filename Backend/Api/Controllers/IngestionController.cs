using Bradford.Core.Interfaces;
using Bradford.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bradford.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class IngestionController : ControllerBase
{
    private readonly IIngestionService _ingestion;
    private readonly ILogger<IngestionController> _logger;
    private readonly IConfiguration _config;

    public IngestionController(IIngestionService ingestion, ILogger<IngestionController> logger, IConfiguration config)
    {
        _ingestion = ingestion;
        _logger    = logger;
        _config    = config;
    }

    /// <summary>Ingest all default Bradford Council pages into the vector store.</summary>
    [HttpPost("full")]
    [ProducesResponseType(typeof(IngestionResult), 200)]
    public async Task<ActionResult<IngestionResult>> IngestAll(CancellationToken ct)
    {
        if (!IsAdminAuthorized())
            return Unauthorized("Admin key required. Pass X-Admin-Key header.");

        _logger.LogInformation("Full council ingestion started.");
        var result = await _ingestion.IngestAllCouncilPagesAsync(ct);
        return Ok(result);
    }

    /// <summary>Ingest specific URLs into the vector store.</summary>
    [HttpPost("urls")]
    [ProducesResponseType(typeof(IngestionResult), 200)]
    public async Task<ActionResult<IngestionResult>> IngestUrls(
        [FromBody] IngestionRequest request,
        CancellationToken ct)
    {
        if (!IsAdminAuthorized())
            return Unauthorized("Admin key required. Pass X-Admin-Key header.");

        if (request.Urls == null || request.Urls.Count == 0)
            return BadRequest("Provide at least one URL.");

        var result = await _ingestion.IngestUrlsAsync(request, ct);
        return Ok(result);
    }

    // Returns true only when the X-Admin-Key header matches the configured AdminKey.
    // If AdminKey is empty/missing in config, all requests are denied.
    private bool IsAdminAuthorized()
    {
        var configuredKey = _config["AdminKey"];
        if (string.IsNullOrEmpty(configuredKey)) return false;
        return Request.Headers.TryGetValue("X-Admin-Key", out var provided)
               && provided == configuredKey;
    }
}
