using Bradford.Core.Interfaces;
using Bradford.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using QVal = Qdrant.Client.Grpc.Value;

namespace Bradford.Infrastructure.VectorStore;

public class QdrantVectorStore : IVectorStoreService
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantVectorStore> _logger;
    private readonly string _collectionName;
    private readonly uint _vectorSize;

    public QdrantVectorStore(IConfiguration config, ILogger<QdrantVectorStore> logger)
    {
        var host     = config["Qdrant:Host"]       ?? "localhost";
        var port     = int.Parse(config["Qdrant:GrpcPort"] ?? "6334");
        var apiKey   = config["Qdrant:ApiKey"];
        var useHttps = bool.Parse(config["Qdrant:UseHttps"] ?? "false");

        _client = string.IsNullOrEmpty(apiKey)
            ? new QdrantClient(host, port)
            : new QdrantClient(host, port, https: useHttps, apiKey: apiKey);

        _logger         = logger;
        _collectionName = config["Qdrant:CollectionName"] ?? "bradford_council";
        _vectorSize     = uint.Parse(config["Qdrant:VectorSize"] ?? "1536");
    }

    public async Task EnsureCollectionAsync(CancellationToken ct = default)
    {
        var collections = await _client.ListCollectionsAsync(ct);
        if (collections.Any(c => c == _collectionName))
        {
            _logger.LogInformation("Collection '{Name}' already exists.", _collectionName);
            return;
        }

        // Pass VectorParams directly — the single-vector overload of CreateCollectionAsync
        await _client.CreateCollectionAsync(
            _collectionName,
            new VectorParams { Size = _vectorSize, Distance = Distance.Cosine },
            cancellationToken: ct);

        _logger.LogInformation("Created Qdrant collection '{Name}' (dims={Dims}, cosine).", _collectionName, _vectorSize);
    }

    public async Task UpsertChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken ct = default)
    {
        var points = new List<PointStruct>();

        foreach (var c in chunks.Where(c => c.Embedding != null))
        {
            var vector = new Vector();
            vector.Data.AddRange(c.Embedding!);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = c.Id },
                Vectors = new Vectors { Vector = vector }
            };

            // Qdrant.Client.Grpc.Value stores strings via the StringValue property
            point.Payload["content"]    = new QVal { StringValue = c.Content };
            point.Payload["source_url"] = new QVal { StringValue = c.SourceUrl };
            point.Payload["title"]      = new QVal { StringValue = c.Title };
            point.Payload["category"]   = new QVal { StringValue = c.Category };
            point.Payload["indexed_at"] = new QVal { StringValue = c.IndexedAt.ToString("O") };

            points.Add(point);
        }

        if (points.Count == 0) return;

        await _client.UpsertAsync(_collectionName, points, cancellationToken: ct);
        _logger.LogInformation("Upserted {Count} points to Qdrant.", points.Count);
    }

    public async Task<List<RetrievedChunk>> SearchAsync(float[] queryVector, int topK = 5, CancellationToken ct = default)
    {
        var results = await _client.SearchAsync(
            collectionName: _collectionName,
            vector: queryVector,
            limit: (ulong)topK,
            payloadSelector: new WithPayloadSelector { Enable = true },
            cancellationToken: ct);

        return results.Select(r => new RetrievedChunk
        {
            Content   = r.Payload.TryGetValue("content",    out var c) ? c.StringValue : string.Empty,
            SourceUrl = r.Payload.TryGetValue("source_url", out var u) ? u.StringValue : string.Empty,
            Title     = r.Payload.TryGetValue("title",      out var t) ? t.StringValue : string.Empty,
            Score     = r.Score
        }).ToList();
    }

    public async Task DeleteByUrlAsync(string sourceUrl, CancellationToken ct = default)
    {
        var condition = new Condition
        {
            Field = new FieldCondition
            {
                Key   = "source_url",
                Match = new Match { Keyword = sourceUrl }
            }
        };

        var filter = new Filter();
        filter.Must.Add(condition);

        await _client.DeleteAsync(
            collectionName: _collectionName,
            filter: filter,
            cancellationToken: ct);

        _logger.LogInformation("Deleted vectors for URL: {Url}", sourceUrl);
    }
}
