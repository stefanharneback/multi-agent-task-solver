namespace MultiAgentTaskSolver.App.ViewModels;

public sealed record TaskListItemViewModel(
    string TaskId,
    string Title,
    string Summary,
    string UpdatedAtText);

public sealed record TaskTreeEntryViewModel(
    string RelativePath,
    string DisplayName,
    bool IsDirectory);

public sealed record ArtifactEntryViewModel(
    string Alias,
    string RelativePath,
    string MediaType,
    string SizeText);

public sealed record RunEntryViewModel(
    string RunId,
    string Title,
    string Status,
    string StartedAtText,
    string StepCountText);

public sealed record ModelEntryViewModel(
    string ModelId,
    string DisplayName,
    string Description);
