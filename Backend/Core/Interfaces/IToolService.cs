using Bradford.Core.Models;

namespace Bradford.Core.Interfaces;

public interface IToolService
{
    List<ToolDefinition> GetToolDefinitions();
    Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken ct = default);
}
