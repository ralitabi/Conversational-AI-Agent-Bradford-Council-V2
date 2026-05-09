using Bradford.Core.Models;

namespace Bradford.Core.Interfaces;

public interface IIngestionService
{
    Task<IngestionResult> IngestUrlsAsync(IngestionRequest request, CancellationToken ct = default);
    Task<IngestionResult> IngestAllCouncilPagesAsync(CancellationToken ct = default);
}
