namespace MultiAgentTaskSolver.Core.Models;

public sealed record ProviderRef
{
    public string ProviderId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;
}

public sealed record ModelCapabilities
{
    public bool SupportsTextInput { get; init; } = true;

    public bool SupportsImageInput { get; init; }

    public bool SupportsAudioInput { get; init; }

    public bool SupportsStreaming { get; init; }

    public bool SupportsToolCalls { get; init; }

    public bool SupportsStructuredOutputs { get; init; }
}

public sealed record ModelRef
{
    public string ProviderId { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public ModelCapabilities Capabilities { get; init; } = new();

    public int? ContextWindowTokens { get; init; }

    public decimal? InputCostPerMillionUsd { get; init; }

    public decimal? OutputCostPerMillionUsd { get; init; }
}

public sealed record UsageRecord
{
    public string ProviderId { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public string? GatewayRequestId { get; init; }

    public string? SourceRequestId { get; init; }

    public DateTimeOffset RecordedAtUtc { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public int? CachedInputTokens { get; init; }

    public int? ReasoningTokens { get; init; }

    public int? TotalTokens { get; init; }

    public decimal? TotalCostUsd { get; init; }

    public int? DurationMs { get; init; }

    public int? HttpStatusCode { get; init; }
}

public sealed record UsageQuery
{
    public int Limit { get; init; } = 20;

    public int Offset { get; init; }
}

public sealed record LlmRequest
{
    public string ModelId { get; init; } = string.Empty;

    public string InputText { get; init; } = string.Empty;

    public string? Instructions { get; init; }

    public bool Stream { get; init; }

    public IReadOnlyList<string> Include { get; init; } = ["output_text"];

    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public sealed record ProviderTextResponse
{
    public string ProviderId { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public string? OutputText { get; init; }

    public string RawResponseBody { get; init; } = string.Empty;

    public UsageRecord? Usage { get; init; }

    public string? GatewayRequestId { get; init; }

    public int HttpStatusCode { get; init; }

    public bool IsStreamingResponse { get; init; }
}

public sealed record TranscriptionRequest
{
    public string ModelId { get; init; } = string.Empty;

    public string? FilePath { get; init; }

    public string? AudioUrl { get; init; }
}

public sealed record TranscriptionResponse
{
    public string ProviderId { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public string TranscriptText { get; init; } = string.Empty;

    public string RawResponseBody { get; init; } = string.Empty;

    public UsageRecord? Usage { get; init; }

    public string? GatewayRequestId { get; init; }
}

public sealed record AppSettings
{
    public string WorkspaceRootPath { get; init; } = string.Empty;

    public string OpenAiGatewayBaseUrl { get; init; } = "http://localhost:3000";

    public string DefaultProviderId { get; init; } = "openai";
}
