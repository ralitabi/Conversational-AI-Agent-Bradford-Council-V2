namespace Bradford.Core.Models;

public class IngestionRequest
{
    public List<string> Urls { get; set; } = new();
    public bool ForceReindex { get; set; } = false;
}

public class IngestionResult
{
    public int ChunksIndexed { get; set; }
    public int PagesProcessed { get; set; }
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class CrawledPage
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime CrawledAt { get; set; } = DateTime.UtcNow;
}
