using Bradford.Core.Models;

namespace Bradford.Core.Interfaces;

public interface ILlmService
{
    Task<string> GenerateAsync(string systemPrompt, List<LlmMessage> messages, CancellationToken ct = default);
    Task<LlmResponse> GenerateWithToolsAsync(string systemPrompt, List<LlmMessage> messages, List<ToolDefinition> tools, CancellationToken ct = default);
    IAsyncEnumerable<string> GenerateStreamAsync(string systemPrompt, List<LlmMessage> messages, CancellationToken ct = default);
}
