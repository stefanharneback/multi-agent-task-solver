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

    public static string CreateTaskId(DateTimeOffset utcNow)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{utcNow:yyyyMMdd-HHmmss}-{suffix}";
    }

    public static string CreateTaskFolderName(string taskId) => $"Task-{taskId}";

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
}
