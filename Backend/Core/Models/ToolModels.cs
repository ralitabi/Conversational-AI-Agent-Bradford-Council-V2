using System.Text.Json.Serialization;

namespace Bradford.Core.Models;

// ── Tool definition sent to LLM ──────────────────────────────────────────────
public class ToolDefinition
{
    [JsonPropertyName("type")]     public string Type     { get; set; } = "function";
    [JsonPropertyName("function")] public FunctionDef Function { get; set; } = new();
}

public class FunctionDef
{
    [JsonPropertyName("name")]        public string Name        { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("parameters")]  public object Parameters  { get; set; } = new();
}

// ── LLM response that may contain tool calls ─────────────────────────────────
public class LlmResponse
{
    public string?         Content   { get; set; }
    public List<ToolCall>? ToolCalls { get; set; }
    public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;
}

public class ToolCall
{
    public string Id       { get; set; } = string.Empty;
    public string Name     { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}

// ── Messages ──────────────────────────────────────────────────────────────────
public class ToolResultMessage : LlmMessage
{
    public string ToolCallId { get; set; } = string.Empty;
    public ToolResultMessage() { Role = "tool"; }
}

public class AssistantToolCallMessage : LlmMessage
{
    public List<ToolCall> ToolCalls { get; set; } = new();
    public AssistantToolCallMessage() { Role = "assistant"; Content = string.Empty; }
}
