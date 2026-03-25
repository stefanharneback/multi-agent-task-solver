using System.Text.Json;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.Infrastructure.Gateway;

public sealed class OpenAiUsageNormalizer : IUsageNormalizer
{
    public UsageRecord? TryNormalizeTextResponse(
        string providerId,
        string modelId,
        string rawPayload,
        TimeSpan duration,
        string? gatewayRequestId)
    {
        try
        {
            using var json = JsonDocument.Parse(rawPayload);
            var root = json.RootElement;
            if (!root.TryGetProperty("usage", out var usage))
            {
                return null;
            }

            return new UsageRecord
            {
                ProviderId = providerId,
                ModelId = modelId,
                GatewayRequestId = gatewayRequestId,
                RecordedAtUtc = DateTimeOffset.UtcNow,
                InputTokens = TryGetInt32(usage, "input_tokens"),
                OutputTokens = TryGetInt32(usage, "output_tokens"),
                CachedInputTokens = TryGetNestedInt32(usage, "input_tokens_details", "cached_tokens"),
                ReasoningTokens = TryGetNestedInt32(usage, "output_tokens_details", "reasoning_tokens"),
                TotalTokens = TryGetInt32(usage, "total_tokens"),
                DurationMs = (int)Math.Round(duration.TotalMilliseconds),
                HttpStatusCode = 200,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<UsageRecord> NormalizeUsageHistory(string providerId, string rawPayload)
    {
        using var json = JsonDocument.Parse(rawPayload);
        if (!json.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var records = new List<UsageRecord>();
        foreach (var item in items.EnumerateArray())
        {
            records.Add(new UsageRecord
            {
                ProviderId = providerId,
                ModelId = item.TryGetProperty("model", out var modelElement) ? modelElement.GetString() ?? string.Empty : string.Empty,
                SourceRequestId = item.TryGetProperty("id", out var idElement) ? idElement.GetString() : null,
                RecordedAtUtc = item.TryGetProperty("created_at", out var createdAtElement)
                    && createdAtElement.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(createdAtElement.GetString(), out var createdAtUtc)
                        ? createdAtUtc
                        : DateTimeOffset.UtcNow,
                InputTokens = TryGetInt32(item, "input_tokens"),
                OutputTokens = TryGetInt32(item, "output_tokens"),
                TotalTokens = TryGetInt32(item, "total_tokens"),
                TotalCostUsd = TryGetDecimal(item, "total_cost_usd"),
                DurationMs = TryGetInt32(item, "duration_ms"),
                HttpStatusCode = TryGetInt32(item, "http_status"),
            });
        }

        return records;
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.Number
            && property.TryGetInt32(out var value)
                ? value
                : null;
    }

    private static int? TryGetNestedInt32(JsonElement element, string parentPropertyName, string propertyName)
    {
        return element.TryGetProperty(parentPropertyName, out var parent)
            ? TryGetInt32(parent, propertyName)
            : null;
    }

    private static decimal? TryGetDecimal(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.Number
            && property.TryGetDecimal(out var value)
                ? value
                : null;
    }
}
