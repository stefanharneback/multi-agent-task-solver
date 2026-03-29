using System.Globalization;
using System.Text.Json;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.ViewModels;

internal static class WorkflowArtifactReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task<UsageRecord?> LoadUsageRecordAsync(
        string taskRootPath,
        StepManifest? step,
        CancellationToken cancellationToken = default)
    {
        if (step is null
            || string.IsNullOrWhiteSpace(taskRootPath)
            || string.IsNullOrWhiteSpace(step.RelativeDirectory)
            || string.IsNullOrWhiteSpace(step.UsagePath))
        {
            return null;
        }

        var usagePath = Path.Combine(
            taskRootPath,
            step.RelativeDirectory.Replace('/', Path.DirectorySeparatorChar),
            step.UsagePath);

        if (!File.Exists(usagePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(usagePath);

        try
        {
            return await JsonSerializer.DeserializeAsync<UsageRecord>(stream, SerializerOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static async Task<IReadOnlyList<UsageRecord>> LoadUsageRecordsAsync(
        string taskRootPath,
        IEnumerable<StepManifest> steps,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var results = await Task.WhenAll(steps.Select(step => LoadUsageRecordAsync(taskRootPath, step, cancellationToken)));
        return results
            .Where(static usage => usage is not null)
            .Cast<UsageRecord>()
            .ToArray();
    }

    public static UsageRecord? AggregateUsage(IEnumerable<UsageRecord?> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var usages = records
            .Where(static record => record is not null)
            .Cast<UsageRecord>()
            .ToArray();

        if (usages.Length == 0)
        {
            return null;
        }

        return new UsageRecord
        {
            ProviderId = usages[0].ProviderId,
            InputTokens = SumInt32(usages, static usage => usage.InputTokens),
            OutputTokens = SumInt32(usages, static usage => usage.OutputTokens),
            CachedInputTokens = SumInt32(usages, static usage => usage.CachedInputTokens),
            ReasoningTokens = SumInt32(usages, static usage => usage.ReasoningTokens),
            TotalTokens = SumInt32(usages, static usage => usage.TotalTokens),
            TotalCostUsd = SumDecimal(usages, static usage => usage.TotalCostUsd),
            DurationMs = SumInt32(usages, static usage => usage.DurationMs),
        };
    }

    public static string BuildUsageText(UsageRecord? usage)
    {
        if (usage is null)
        {
            return "Usage not recorded.";
        }

        var parts = new List<string>();

        if (usage.TotalTokens is int totalTokens)
        {
            parts.Add($"{totalTokens:n0} total tokens");
        }

        if (usage.InputTokens is int inputTokens)
        {
            parts.Add($"{inputTokens:n0} input");
        }

        if (usage.OutputTokens is int outputTokens)
        {
            parts.Add($"{outputTokens:n0} output");
        }

        if (usage.CachedInputTokens is int cachedTokens)
        {
            parts.Add($"{cachedTokens:n0} cached");
        }

        if (usage.ReasoningTokens is int reasoningTokens)
        {
            parts.Add($"{reasoningTokens:n0} reasoning");
        }

        if (usage.DurationMs is int durationMs)
        {
            parts.Add($"{durationMs:n0} ms");
        }

        if (usage.TotalCostUsd is decimal totalCostUsd)
        {
            parts.Add($"${totalCostUsd.ToString("0.####", CultureInfo.InvariantCulture)}");
        }

        return parts.Count == 0
            ? "Usage recorded without token counts."
            : string.Join(" | ", parts);
    }

    private static int? SumInt32(IEnumerable<UsageRecord> records, Func<UsageRecord, int?> selector)
    {
        var hasValue = false;
        var sum = 0;

        foreach (var record in records)
        {
            if (selector(record) is not int value)
            {
                continue;
            }

            hasValue = true;
            sum += value;
        }

        return hasValue ? sum : null;
    }

    private static decimal? SumDecimal(IEnumerable<UsageRecord> records, Func<UsageRecord, decimal?> selector)
    {
        var hasValue = false;
        decimal sum = 0;

        foreach (var record in records)
        {
            if (selector(record) is not decimal value)
            {
                continue;
            }

            hasValue = true;
            sum += value;
        }

        return hasValue ? sum : null;
    }
}
