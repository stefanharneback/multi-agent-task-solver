namespace MultiAgentTaskSolver.Core.Models;

public sealed record TaskManifest
{
    public string SchemaVersion { get; init; } = TaskFolderConventions.SchemaVersion;

    public string Id { get; init; } = string.Empty;

    public string FolderName { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public TaskLifecycleState Status { get; init; } = TaskLifecycleState.Draft;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public string TaskMarkdownPath { get; init; } = TaskFolderConventions.TaskMarkdownFileName;

    public IReadOnlyList<string> InputPaths { get; init; } = TaskFolderConventions.DefaultInputPaths.ToArray();

    public IReadOnlyList<string> OutputPaths { get; init; } = TaskFolderConventions.DefaultOutputPaths.ToArray();

    public IReadOnlyDictionary<string, string> OutputPathDescriptions { get; init; } = new Dictionary<string, string>();

    // Legacy compatibility for previously saved task manifests.
    public IReadOnlyList<string> InputCategories { get; init; } = TaskFolderConventions.DefaultInputCategories.ToArray();

    public IReadOnlyList<ArtifactManifest> Artifacts { get; init; } = [];

    public IReadOnlyList<RunManifest> Runs { get; init; } = [];
}

public sealed record ArtifactManifest
{
    public string Id { get; init; } = string.Empty;

    public string Alias { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string MediaType { get; init; } = "application/octet-stream";

    public string Sha256 { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public DateTimeOffset ImportedAtUtc { get; init; }
}

public sealed record RunManifest
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public TaskRunKind Kind { get; init; } = TaskRunKind.TaskReview;

    public TaskRunStatus Status { get; init; } = TaskRunStatus.Planned;

    public int Sequence { get; init; }

    public string Summary { get; init; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public IReadOnlyList<StepManifest> Steps { get; init; } = [];
}

public sealed record StepManifest
{
    public string Id { get; init; } = string.Empty;

    public TaskStepType StepType { get; init; } = TaskStepType.TaskReview;

    public TaskStepStatus Status { get; init; } = TaskStepStatus.Planned;

    public int Attempt { get; init; } = 1;

    public ProviderRef Provider { get; init; } = new();

    public ModelRef Model { get; init; } = new();

    public string RelativeDirectory { get; init; } = string.Empty;

    public string StepFilePath { get; init; } = "step.json";

    public string PromptPath { get; init; } = "prompt.md";

    public string ResponsePath { get; init; } = "response.md";

    public string UsagePath { get; init; } = "usage.json";

    public string PromptVersion { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> ReferencedArtifactAliases { get; init; } = [];

    public IReadOnlyList<string> OutputArtifactPaths { get; init; } = [];

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }
}

public sealed record TaskWorkspaceSnapshot
{
    public string TaskRootPath { get; init; } = string.Empty;

    public TaskManifest Manifest { get; init; } = new();

    public string TaskMarkdown { get; init; } = string.Empty;

    public IReadOnlyList<TaskTreeNode> Tree { get; init; } = [];
}

public sealed record TaskTreeNode
{
    public string Name { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    public IReadOnlyList<TaskTreeNode> Children { get; init; } = [];
}

public sealed record CreateTaskRequest
{
    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string TaskMarkdown { get; init; } = string.Empty;

    public IReadOnlyList<string> InputPaths { get; init; } = [];

    public IReadOnlyList<string> OutputPaths { get; init; } = [];

    public IReadOnlyDictionary<string, string> OutputPathDescriptions { get; init; } = new Dictionary<string, string>();
}

public sealed record ArtifactImportRequest
{
    public string SourcePath { get; init; } = string.Empty;

    public string DestinationRelativeDirectory { get; init; } = string.Empty;

    public string? Alias { get; init; }
}
