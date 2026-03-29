using System.Collections.ObjectModel;
using System.Globalization;
using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.Core;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.ViewModels;

public sealed partial class RunHistoryViewModel : ViewModelBase
{
    private readonly ITaskWorkspaceCoordinator _coordinator;

    public RunHistoryViewModel(ITaskWorkspaceCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public ObservableCollection<FlowSummaryMetricViewModel> SummaryMetrics { get; } = [];

    public ObservableCollection<TaskFlowStageViewModel> FlowStages { get; } = [];

    public ObservableCollection<RunHistoryEntryViewModel> Runs { get; } = [];

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string TaskId { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string TaskTitle { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string TaskStatusText { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string ProgressNarrative { get; set; } = string.Empty;

    public Task LoadAsync(string taskId)
    {
        return RunBusyAsync(() => LoadCoreAsync(taskId));
    }

    private async Task LoadCoreAsync(string taskId)
    {
        var snapshot = await _coordinator.LoadTaskAsync(taskId);
        if (snapshot is null)
        {
            throw new InvalidOperationException($"Task '{taskId}' was not found.");
        }

        TaskId = snapshot.Manifest.Id;
        TaskTitle = snapshot.Manifest.Title;
        TaskStatusText = snapshot.Manifest.Status.GetDisplayName();
        ProgressNarrative = BuildProgressNarrative(snapshot.Manifest);

        SummaryMetrics.Clear();
        foreach (var metric in BuildSummaryMetrics(snapshot.Manifest))
        {
            SummaryMetrics.Add(metric);
        }

        FlowStages.Clear();
        foreach (var stage in BuildFlowStages(snapshot.Manifest))
        {
            FlowStages.Add(stage);
        }

        Runs.Clear();
        foreach (var run in snapshot.Manifest.Runs.OrderByDescending(static run => run.Sequence))
        {
            Runs.Add(await BuildRunEntryAsync(snapshot, run));
        }
    }

    private static IReadOnlyList<FlowSummaryMetricViewModel> BuildSummaryMetrics(TaskManifest manifest)
    {
        var totalSteps = manifest.Runs.Sum(static run => run.Steps.Count);
        var latestRun = manifest.Runs.OrderByDescending(static run => run.Sequence).FirstOrDefault();
        var latestStep = latestRun is not null && latestRun.Steps.Count > 0
            ? latestRun.Steps[^1]
            : null;
        var latestActivity = latestRun?.CompletedAtUtc ?? latestRun?.StartedAtUtc ?? manifest.UpdatedAtUtc;
        var latestModel = latestStep is null
            ? "Not run yet"
            : string.IsNullOrWhiteSpace(latestStep.Model.DisplayName) ? latestStep.Model.ModelId : latestStep.Model.DisplayName;

        return
        [
            new FlowSummaryMetricViewModel("Status", manifest.Status.GetDisplayName(), "Current task lifecycle state"),
            new FlowSummaryMetricViewModel("Runs", manifest.Runs.Count.ToString(CultureInfo.InvariantCulture), "Immutable execution attempts recorded"),
            new FlowSummaryMetricViewModel("Steps", totalSteps.ToString(CultureInfo.InvariantCulture), "Total persisted model steps"),
            new FlowSummaryMetricViewModel("Latest model", latestModel, $"Last activity {latestActivity.LocalDateTime:yyyy-MM-dd HH:mm}"),
        ];
    }

    private static IReadOnlyList<TaskFlowStageViewModel> BuildFlowStages(TaskManifest manifest)
    {
        var reviewCount = manifest.Runs.Count(static run => run.Kind == TaskRunKind.TaskReview);
        var workerCount = manifest.Runs.Count(static run => run.Kind == TaskRunKind.Worker);
        var criticCount = manifest.Runs.Count(static run => run.Kind == TaskRunKind.Critic);
        var currentStageIndex = GetCurrentStageIndex(manifest.Status);

        return
        [
            CreateStage("Draft", "Task text and inputs are being prepared", 0, currentStageIndex, manifest.Artifacts.Count == 0 ? "No imported artifacts yet" : $"{manifest.Artifacts.Count} imported artifact(s)"),
            CreateStage("Review", "Review agent checks clarity and readiness", 1, currentStageIndex, reviewCount == 0 ? "No review run yet" : $"{reviewCount} review run(s) recorded"),
            CreateStage("Work", "Worker agent executes approved task", 2, currentStageIndex, workerCount == 0 ? "Worker loop not started" : $"{workerCount} worker run(s) recorded"),
            CreateStage("Critique", "Critic evaluates work against the task", 3, currentStageIndex, criticCount == 0 ? "Critic loop not started" : $"{criticCount} critic run(s) recorded"),
            CreateStage("Done", "Task is accepted or archived", 4, currentStageIndex, manifest.Status is TaskLifecycleState.Done or TaskLifecycleState.Archived ? "Task has been closed" : "Completion not reached yet"),
        ];
    }

    private static TaskFlowStageViewModel CreateStage(string title, string description, int stageIndex, int currentStageIndex, string detail)
    {
        var isCompleted = stageIndex < currentStageIndex;
        var isCurrent = stageIndex == currentStageIndex;
        var stateText = isCompleted ? "Completed" : isCurrent ? "Current" : "Planned";

        return new TaskFlowStageViewModel(
            title,
            stateText,
            $"{description}. {detail}",
            isCompleted,
            isCurrent);
    }

    private static int GetCurrentStageIndex(TaskLifecycleState status)
    {
        return status switch
        {
            TaskLifecycleState.Draft => 0,
            TaskLifecycleState.ReviewReady or TaskLifecycleState.UnderReview => 1,
            TaskLifecycleState.WorkApproved or TaskLifecycleState.Working or TaskLifecycleState.NeedsRework => 2,
            TaskLifecycleState.UnderCritique => 3,
            TaskLifecycleState.Done or TaskLifecycleState.Archived => 4,
            _ => 0,
        };
    }

    private static string BuildProgressNarrative(TaskManifest manifest)
    {
        if (manifest.Runs.Count == 0)
        {
            return "The task has been created, but no agent run has started yet.";
        }

        var latestRun = manifest.Runs.OrderByDescending(static run => run.Sequence).First();
        var completedAt = latestRun.CompletedAtUtc?.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "still running";
        return $"Latest progress: {latestRun.Kind.GetDisplayName()} is {latestRun.Status.GetDisplayName().ToLowerInvariant()} and last changed at {completedAt}.";
    }

    private static async Task<RunHistoryEntryViewModel> BuildRunEntryAsync(TaskWorkspaceSnapshot snapshot, RunManifest run)
    {
        var title = string.IsNullOrWhiteSpace(run.Title) ? run.Kind.GetDisplayName() : run.Title;
        var durationText = run.CompletedAtUtc is null
            ? "Duration unavailable"
            : $"{Math.Max(1, (int)Math.Round((run.CompletedAtUtc.Value - run.StartedAtUtc).TotalMinutes, MidpointRounding.AwayFromZero))} min";
        var modelText = BuildModelText(run);
        var referencedAliasesText = BuildReferencedAliasesText(run);
        var steps = new List<StepHistoryEntryViewModel>(run.Steps.Count);

        foreach (var step in run.Steps)
        {
            steps.Add(await BuildStepEntryAsync(snapshot, step));
        }

        return new RunHistoryEntryViewModel(
            run.Id,
            $"Run {run.Sequence}",
            title,
            run.Kind.GetDisplayName(),
            run.Status.GetDisplayName(),
            BuildTimingText(run.StartedAtUtc, run.CompletedAtUtc),
            durationText,
            modelText,
            referencedAliasesText,
            $"{run.Steps.Count} step(s)",
            string.IsNullOrWhiteSpace(run.Summary) ? "No run summary yet." : run.Summary,
            steps);
    }

    private static async Task<StepHistoryEntryViewModel> BuildStepEntryAsync(TaskWorkspaceSnapshot snapshot, StepManifest step)
    {
        var modelLabel = string.IsNullOrWhiteSpace(step.Model.DisplayName) ? step.Model.ModelId : step.Model.DisplayName;
        var providerLabel = string.IsNullOrWhiteSpace(step.Provider.DisplayName) ? step.Provider.ProviderId : step.Provider.DisplayName;
        var promptVersionText = string.IsNullOrWhiteSpace(step.PromptVersion) ? "Prompt version not recorded" : $"Prompt {step.PromptVersion}";
        var referencedAliasesText = step.ReferencedArtifactAliases.Count == 0
            ? "No referenced artifacts"
            : string.Join(", ", step.ReferencedArtifactAliases.Select(static alias => $"@{alias.TrimStart('@')}"));
        var usage = await WorkflowArtifactReader.LoadUsageRecordAsync(snapshot.TaskRootPath, step);
        var usageText = WorkflowArtifactReader.BuildUsageText(usage);

        return new StepHistoryEntryViewModel(
            $"Attempt {step.Attempt}",
            GetStepTitle(step.StepType),
            step.Status.GetDisplayName(),
            modelLabel,
            providerLabel,
            BuildTimingText(step.StartedAtUtc, step.CompletedAtUtc),
            promptVersionText,
            referencedAliasesText,
            usageText,
            string.IsNullOrWhiteSpace(step.Summary) ? "No step summary yet." : step.Summary);
    }

    private static string GetStepTitle(TaskStepType stepType)
    {
        return stepType switch
        {
            TaskStepType.TaskReview => "Task review step",
            TaskStepType.Worker => "Worker step",
            TaskStepType.Critic => "Critic step",
            TaskStepType.UserDecision => "User decision step",
            TaskStepType.Evaluation => "Evaluation step",
            _ => "Step",
        };
    }

    private static string BuildModelText(RunManifest run)
    {
        var models = run.Steps
            .Select(static step => string.IsNullOrWhiteSpace(step.Model.DisplayName) ? step.Model.ModelId : step.Model.DisplayName)
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return models.Length == 0 ? "Model not recorded" : string.Join(" | ", models);
    }

    private static string BuildReferencedAliasesText(RunManifest run)
    {
        var aliases = run.Steps
            .SelectMany(static step => step.ReferencedArtifactAliases)
            .Select(static alias => $"@{alias.TrimStart('@')}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return aliases.Length == 0 ? "No referenced artifacts" : string.Join(", ", aliases);
    }

    private static string BuildTimingText(DateTimeOffset startedAtUtc, DateTimeOffset? completedAtUtc)
    {
        var startedText = startedAtUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (completedAtUtc is null)
        {
            return $"Started {startedText}";
        }

        return $"Started {startedText} | Completed {completedAtUtc.Value.LocalDateTime:yyyy-MM-dd HH:mm}";
    }
}
