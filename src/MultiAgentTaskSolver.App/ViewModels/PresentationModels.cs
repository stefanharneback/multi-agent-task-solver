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

public sealed record FlowSummaryMetricViewModel(
    string Label,
    string Value,
    string Detail);

public sealed record TaskFlowStageViewModel(
    string Title,
    string StateText,
    string Detail,
    bool IsCompleted,
    bool IsCurrent);

public sealed record StepHistoryEntryViewModel(
    string SequenceText,
    string Title,
    string Status,
    string ModelText,
    string ProviderText,
    string TimingText,
    string PromptVersionText,
    string ReferencedAliasesText,
    string Summary);

public sealed record RunHistoryEntryViewModel(
    string RunId,
    string SequenceText,
    string Title,
    string KindText,
    string Status,
    string TimingText,
    string DurationText,
    string ModelText,
    string ReferencedAliasesText,
    string StepCountText,
    string Summary,
    IReadOnlyList<StepHistoryEntryViewModel> Steps);

public sealed record ModelEntryViewModel(
    string ModelId,
    string DisplayName,
    string Description);
