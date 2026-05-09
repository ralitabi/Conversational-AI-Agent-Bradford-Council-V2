using Bradford.Core.Interfaces;
using Bradford.Core.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Bradford.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IAgentService _agent;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IAgentService agent, ILogger<ChatController> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    /// <summary>Send a message and receive a full response.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), 200)]
    public async Task<ActionResult<ChatResponse>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message cannot be empty.");

        _logger.LogInformation("Chat [{Session}]: {Message}", request.SessionId, RedactPii(request.Message));
        var response = await _agent.ChatAsync(request, ct);
        return Ok(response);
    }

    /// <summary>Stream a response token-by-token via Server-Sent Events.</summary>
    [HttpPost("stream")]
    [Produces("text/event-stream")]
    public async Task ChatStream([FromBody] ChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.ContentType = "text/event-stream";

        await foreach (var token in _agent.ChatStreamAsync(request, ct))
        {
            // Escape newlines so SSE line format is never broken by token content.
            // Frontend unescapes \\n → \n before rendering markdown.
            var escaped = token.Replace("\n", "\\n");
            await Response.WriteAsync($"data: {escaped}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        await Response.WriteAsync("data: [DONE]\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    // Strips profile context prefix and masks postcodes before writing to Railway logs.
    private static string RedactPii(string text)
    {
        // Remove [User: name=Sarah, postcode=BD5 8LT] context prefix
        text = Regex.Replace(text, @"^\s*\[User:[^\]]*\]\s*", "", RegexOptions.IgnoreCase);
        // Remove [brief reply] / [detailed reply ...] style hints
        text = Regex.Replace(text, @"\[(brief|detailed)[^\]]*\]\s*", "", RegexOptions.IgnoreCase);
        // Mask remaining UK postcodes
        text = Regex.Replace(text, @"\b[A-Z]{1,2}\d{1,2}[A-Z]?\s?\d[A-Z]{2}\b", "[postcode]", RegexOptions.IgnoreCase);
        return text.Trim();
    }

    /// <summary>Clear conversation history for a session.</summary>
    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> ClearSession(string sessionId, CancellationToken ct)
    {
        var conversation = HttpContext.RequestServices.GetRequiredService<IConversationService>();
        await conversation.ClearSessionAsync(sessionId, ct);
        return NoContent();
    }
}
