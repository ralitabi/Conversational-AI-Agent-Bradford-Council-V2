// Retries OpenAI requests that fail with 429 (rate limit) or 5xx (server error).
// Uses exponential backoff: 1s after attempt 1, 2s after attempt 2.
public sealed class OpenAiRetryHandler : DelegatingHandler
{
    private const int MaxRetries = 2;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage? response = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), ct);

            response = await base.SendAsync(request, ct);

            if (response.IsSuccessStatusCode) return response;

            var status = (int)response.StatusCode;
            if (status != 429 && status < 500) return response;
            if (attempt == MaxRetries)         return response;

            response.Dispose();
        }

        return response!;
    }
}
