using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bradford.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bradford.Core.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string _model;
    private const string OpenAiEmbeddingUrl = "https://api.openai.com/v1/embeddings";

    public int Dimensions => 1536; // text-embedding-3-small

    public EmbeddingService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<EmbeddingService> logger)
    {
        _http = httpClientFactory.CreateClient("OpenAI");
        _logger = logger;
        _model = config["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var results = await EmbedBatchAsync(new[] { text }, ct);
        return results[0];
    }

    public async Task<List<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var inputList = texts.Select(t => t.Length > 8000 ? t[..8000] : t).ToList();

        var payload = new { input = inputList, model = _model };
        var response = await _http.PostAsJsonAsync(OpenAiEmbeddingUrl, payload, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct);
        if (result?.Data == null) throw new InvalidOperationException("Empty embedding response.");

        return result.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToList();
    }

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")] public List<EmbeddingData>? Data { get; set; }
    }
    private sealed class EmbeddingData
    {
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("embedding")] public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
