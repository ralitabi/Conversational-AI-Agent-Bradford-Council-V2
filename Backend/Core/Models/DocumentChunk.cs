namespace Bradford.Core.Models;

public class DocumentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;  // bins, planning, housing, etc.
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    public float[]? Embedding { get; set; }
}

public class RagContext
{
    public List<RetrievedChunk> Chunks { get; set; } = new();
    public bool HasResults => Chunks.Count > 0;

    public string BuildContextBlock()
    {
        if (!HasResults) return string.Empty;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== RETRIEVED KNOWLEDGE FROM BRADFORD COUNCIL ===");
        foreach (var chunk in Chunks)
        {
            sb.AppendLine($"[Source: {chunk.Title} | {chunk.SourceUrl}]");
            sb.AppendLine(chunk.Content);
            sb.AppendLine("---");
        }
        sb.AppendLine("=== END OF RETRIEVED KNOWLEDGE ===");
        return sb.ToString();
    }
}

public class RetrievedChunk
{
    public string Content { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public float Score { get; set; }
}
