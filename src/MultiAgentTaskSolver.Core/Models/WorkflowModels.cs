using System.Text.Json.Serialization;

namespace MultiAgentTaskSolver.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskLifecycleState
{
    [JsonStringEnumMemberName("draft")]
    Draft,

    [JsonStringEnumMemberName("review-ready")]
    ReviewReady,

    [JsonStringEnumMemberName("under-review")]
    UnderReview,

    [JsonStringEnumMemberName("work-approved")]
    WorkApproved,

    [JsonStringEnumMemberName("working")]
    Working,

    [JsonStringEnumMemberName("under-critique")]
    UnderCritique,

    [JsonStringEnumMemberName("needs-rework")]
    NeedsRework,

    [JsonStringEnumMemberName("done")]
    Done,

    [JsonStringEnumMemberName("archived")]
    Archived,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskRunKind
{
    [JsonStringEnumMemberName("task-review")]
    TaskReview,

    [JsonStringEnumMemberName("worker")]
    Worker,

    [JsonStringEnumMemberName("critic")]
    Critic,

    [JsonStringEnumMemberName("user-decision")]
    UserDecision,

    [JsonStringEnumMemberName("evaluation")]
    Evaluation,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskRunStatus
{
    [JsonStringEnumMemberName("planned")]
    Planned,

    [JsonStringEnumMemberName("running")]
    Running,

    [JsonStringEnumMemberName("completed")]
    Completed,

    [JsonStringEnumMemberName("failed")]
    Failed,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled,

    [JsonStringEnumMemberName("superseded")]
    Superseded,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskStepType
{
    [JsonStringEnumMemberName("task-review")]
    TaskReview,

    [JsonStringEnumMemberName("worker")]
    Worker,

    [JsonStringEnumMemberName("critic")]
    Critic,

    [JsonStringEnumMemberName("user-decision")]
    UserDecision,

    [JsonStringEnumMemberName("evaluation")]
    Evaluation,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskStepStatus
{
    [JsonStringEnumMemberName("planned")]
    Planned,

    [JsonStringEnumMemberName("running")]
    Running,

    [JsonStringEnumMemberName("completed")]
    Completed,

    [JsonStringEnumMemberName("failed")]
    Failed,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewDecision
{
    [JsonStringEnumMemberName("approve")]
    Approve,

    [JsonStringEnumMemberName("revise")]
    Revise,
}

public sealed record StepArtifactsPayload
{
    public string PromptMarkdown { get; init; } = string.Empty;

    public string ResponseMarkdown { get; init; } = string.Empty;

    public UsageRecord? Usage { get; init; }
}

public sealed record ResolvedArtifactReference
{
    public string Alias { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string MediaType { get; init; } = "application/octet-stream";

    public bool IsTextual { get; init; }

    public bool WasTruncated { get; init; }

    public int? CharacterCount { get; init; }

    public string ContentExcerpt { get; init; } = string.Empty;
}

public sealed record TaskReferenceResolution
{
    public IReadOnlyList<string> ReferencedAliases { get; init; } = [];

    public IReadOnlyList<ResolvedArtifactReference> ResolvedArtifacts { get; init; } = [];

    public IReadOnlyList<string> MissingAliases { get; init; } = [];
}

public sealed record ReviewPromptPackage
{
    public string PromptVersion { get; init; } = "task-review-v1";

    public string Instructions { get; init; } = string.Empty;

    public string InputText { get; init; } = string.Empty;

    public IReadOnlyList<string> ReferencedAliases { get; init; } = [];
}

public sealed record TaskReviewRequest
{
    public string ProviderId { get; init; } = "openai";

    public string ModelId { get; init; } = string.Empty;
}

public sealed record TaskReviewResult
{
    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string StepId { get; init; } = string.Empty;

    public TaskLifecycleState TaskStatus { get; init; }

    public string OutputText { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string PromptVersion { get; init; } = string.Empty;

    public IReadOnlyList<string> ReferencedAliases { get; init; } = [];

    public IReadOnlyList<string> MissingAliases { get; init; } = [];

    public UsageRecord? Usage { get; init; }
}

public sealed record ReviewDecisionRequest
{
    public ReviewDecision Decision { get; init; } = ReviewDecision.Approve;

    public string Notes { get; init; } = string.Empty;
}

public sealed record ReviewDecisionResult
{
    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string StepId { get; init; } = string.Empty;

    public ReviewDecision Decision { get; init; }

    public TaskLifecycleState TaskStatus { get; init; }

    public string OutputText { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string PromptVersion { get; init; } = string.Empty;
}
