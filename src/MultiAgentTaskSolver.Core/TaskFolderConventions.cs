using System.Text;

namespace MultiAgentTaskSolver.Core;

public static class TaskFolderConventions
{
    public const string TaskManifestFileName = "task.json";
    public const string TaskMarkdownFileName = "task.md";
    public const string InputsFolderName = "inputs";
    public const string RunsFolderName = "runs";
    public const string OutputsFolderName = "outputs";
    public const string CacheFolderName = "cache";
    public const string DefaultWorkerOutputFileName = "worker-output.md";
    public const string SchemaVersion = "1.0";

    public static IReadOnlyList<string> DefaultInputCategories { get; } =
    [
        "documents",
        "transcripts",
        "interviews",
        "articles",
        "rules",
        "notes",
        "standards",
    ];

    public static IReadOnlyList<string> DefaultInputPaths { get; } = DefaultInputCategories
        .Select(category => $"{InputsFolderName}/{category}")
        .ToArray();

    public static IReadOnlyList<string> DefaultOutputPaths { get; } =
    [
        $"{OutputsFolderName}/{DefaultWorkerOutputFileName}",
    ];

    public static string CreateTaskId(DateTimeOffset utcNow)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{utcNow:yyyyMMdd-HHmmss}-{suffix}";
    }

    public static string CreateTaskFolderName(string taskId) => $"Task-{taskId}";

    public static string[] ParseInputPaths(string rawValue)
    {
        return ParsePathList(rawValue, NormalizeInputPath);
    }

    public static string[] ParseOutputPaths(string rawValue)
    {
        return ParsePathList(rawValue, NormalizeOutputPath);
    }

    public static string NormalizeInputPath(string value)
    {
        return NormalizeDeclaredPath(value, InputsFolderName);
    }

    public static string NormalizeOutputPath(string value)
    {
        return NormalizeDeclaredPath(value, OutputsFolderName);
    }

    public static string CreateRunScopedWorkerOutputPath(int sequence)
    {
        return $"{OutputsFolderName}/{sequence:0000}-worker/{DefaultWorkerOutputFileName}";
    }

    public static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "task";
        }

        var builder = new StringBuilder(value.Length);
        var previousWasDash = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasDash = false;
                continue;
            }

            if (previousWasDash)
            {
                continue;
            }

            builder.Append('-');
            previousWasDash = true;
        }

        return builder.ToString().Trim('-') is { Length: > 0 } slug ? slug : "task";
    }

    private static string[] ParsePathList(
        string rawValue,
        Func<string, string> normalizer,
        IReadOnlyList<string>? fallback = null)
    {
        var values = string.IsNullOrWhiteSpace(rawValue)
            ? fallback ?? []
            : rawValue
                .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(normalizer)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return values.Count > 0 ? values.ToArray() : [];
    }

    private static string NormalizeDeclaredPath(string value, string rootFolderName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Path must stay under '{rootFolderName}/'.");
        }

        var normalized = value.Trim().Replace('\\', '/').Trim('/');
        if (!normalized.StartsWith(rootFolderName, StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"{rootFolderName}/{normalized}";
        }

        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0 || !string.Equals(segments[0], rootFolderName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path '{value}' must stay under '{rootFolderName}/'.");
        }

        var safeSegments = new List<string> { rootFolderName };
        foreach (var segment in segments.Skip(1))
        {
            if (segment is ".")
            {
                continue;
            }

            if (segment is "..")
            {
                throw new InvalidOperationException($"Path '{value}' must stay under '{rootFolderName}/'.");
            }

            safeSegments.Add(segment);
        }

        if (safeSegments.Count == 1)
        {
            throw new InvalidOperationException($"Path '{value}' must stay under '{rootFolderName}/'.");
        }

        return string.Join('/', safeSegments);
    }
}
