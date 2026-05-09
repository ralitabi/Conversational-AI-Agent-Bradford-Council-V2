using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bradford.Core.Interfaces;
using Bradford.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bradford.Core.Services;

public class LlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly ILogger<LlmService> _logger;
    private readonly string _model;
    private const string ChatUrl = "https://api.openai.com/v1/chat/completions";

    public LlmService(IHttpClientFactory factory, IConfiguration config, ILogger<LlmService> logger)
    {
        _http   = factory.CreateClient("OpenAI");
        _logger = logger;
        _model  = config["OpenAI:ChatModel"] ?? "gpt-4o";
    }

    // ── Plain generation (no tools) ──────────────────────────────────────────
    public async Task<string> GenerateAsync(string systemPrompt, List<LlmMessage> messages, CancellationToken ct = default)
    {
        var payload = BuildMessages(systemPrompt, messages);
        var body    = new { model = _model, messages = payload, max_tokens = 600, temperature = 0.3 };
        var resp    = await _http.PostAsJsonAsync(ChatUrl, body, ct);
        resp.EnsureSuccessStatusCode();
        var result  = await resp.Content.ReadFromJsonAsync<OaiResponse>(cancellationToken: ct);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    // ── Generation with tool/function calling ────────────────────────────────
    public async Task<LlmResponse> GenerateWithToolsAsync(
        string systemPrompt,
        List<LlmMessage> messages,
        List<ToolDefinition> tools,
        CancellationToken ct = default)
    {
        var msgPayload = BuildMessages(systemPrompt, messages);
        var body = new
        {
            model               = _model,
            messages            = msgPayload,
            tools               = tools,
            tool_choice         = "auto",
            parallel_tool_calls = false,
            max_tokens          = 800,
            temperature         = 0.3
        };

        var json = JsonSerializer.Serialize(body);
        using var req = new HttpRequestMessage(HttpMethod.Post, ChatUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<OaiResponse>(cancellationToken: ct);
        var choice = result?.Choices?.FirstOrDefault();

        if (choice == null) return new LlmResponse { Content = string.Empty };

        // Tool call response
        if (choice.FinishReason == "tool_calls" && choice.Message?.ToolCalls != null)
        {
            return new LlmResponse
            {
                Content   = choice.Message.Content,
                ToolCalls = choice.Message.ToolCalls.Select(tc => new ToolCall
                {
                    Id        = tc.Id,
                    Name      = tc.Function?.Name ?? string.Empty,
                    Arguments = tc.Function?.Arguments ?? "{}"
                }).ToList()
            };
        }

        return new LlmResponse { Content = choice.Message?.Content ?? string.Empty };
    }

    // ── Streaming ─────────────────────────────────────────────────────────────
    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string systemPrompt,
        List<LlmMessage> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = BuildMessages(systemPrompt, messages);
        var body    = new { model = _model, messages = payload, stream = true, max_tokens = 800, temperature = 0.3 };
        var json    = JsonSerializer.Serialize(body);

        using var req = new HttpRequestMessage(HttpMethod.Post, ChatUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;
            var data = line["data: ".Length..];
            if (data == "[DONE]") yield break;

            OaiStreamChunk? chunk = null;
            try { chunk = JsonSerializer.Deserialize<OaiStreamChunk>(data); } catch { continue; }

            var token = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(token)) yield return token;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private List<object> BuildMessages(string systemPrompt, List<LlmMessage> messages)
    {
        var list = new List<object> { new { role = "system", content = systemPrompt } };

        foreach (var m in messages)
        {
            if (m is ToolResultMessage tr)
            {
                list.Add(new { role = "tool", tool_call_id = tr.ToolCallId, content = tr.Content });
            }
            else if (m is AssistantToolCallMessage atc)
            {
                list.Add(new
                {
                    role       = "assistant",
                    content    = (string?)null,
                    tool_calls = atc.ToolCalls.Select(tc => new
                    {
                        id       = tc.Id,
                        type     = "function",
                        function = new { name = tc.Name, arguments = tc.Arguments }
                    }).ToList()
                });
            }
            else
            {
                list.Add(new { role = m.Role, content = m.Content });
            }
        }
        return list;
    }

    // ── Private response shapes ───────────────────────────────────────────────
    private sealed class OaiResponse
    {
        [JsonPropertyName("choices")] public List<OaiChoice>? Choices { get; set; }
    }
    private sealed class OaiChoice
    {
        [JsonPropertyName("message")]      public OaiMessage? Message     { get; set; }
        [JsonPropertyName("finish_reason")] public string?    FinishReason { get; set; }
    }
    private sealed class OaiMessage
    {
        [JsonPropertyName("content")]    public string?          Content   { get; set; }
        [JsonPropertyName("tool_calls")] public List<OaiToolCall>? ToolCalls { get; set; }
    }
    private sealed class OaiToolCall
    {
        [JsonPropertyName("id")]       public string?       Id       { get; set; }
        [JsonPropertyName("function")] public OaiFunction?  Function { get; set; }
    }
    private sealed class OaiFunction
    {
        [JsonPropertyName("name")]      public string? Name      { get; set; }
        [JsonPropertyName("arguments")] public string? Arguments { get; set; }
    }
    private sealed class OaiStreamChunk
    {
        [JsonPropertyName("choices")] public List<OaiStreamChoice>? Choices { get; set; }
    }
    private sealed class OaiStreamChoice
    {
        [JsonPropertyName("delta")] public OaiDelta? Delta { get; set; }
    }
    private sealed class OaiDelta
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}
