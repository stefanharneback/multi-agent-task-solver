using System.Globalization;
using System.Text;
using MultiAgentTaskSolver.Core;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.Infrastructure.Execution;

public sealed class UserDecisionWorkflow : IUserDecisionWorkflow
{
    private const string PromptVersion = "user-decision-v1";

    private static readonly ProviderRef UserProvider = new()
    {
        ProviderId = "user",
        DisplayName = "User approval",
    };

    private static readonly ModelRef UserDecisionModel = new()
    {
        ProviderId = "user",
        ModelId = "manual-decision",
        DisplayName = "Manual decision",
        Description = "Explicit user approval or revision decision.",
    };

    private readonly ITaskWorkspaceStore _taskWorkspaceStore;

    public UserDecisionWorkflow(ITaskWorkspaceStore taskWorkspaceStore)
    {
        _taskWorkspaceStore = taskWorkspaceStore;
    }

    public async Task<ReviewDecisionResult> RunAsync(
        string workspaceRootPath,
        TaskWorkspaceSnapshot snapshot,
        ReviewDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var latestReviewRun = snapshot.Manifest.Runs
            .Where(static run => run.Kind == TaskRunKind.TaskReview)
            .OrderByDescending(static run => run.Sequence)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Run a task review before recording a review decision.");

        var completedAtUtc = DateTimeOffset.UtcNow;
        var targetStatus = request.Decision switch
        {
            ReviewDecision.Approve => TaskWorkflowStateMachine.ApproveReviewedTask(snapshot.Manifest.Status),
            ReviewDecision.Revise => TaskWorkflowStateMachine.ReviseReviewedTask(snapshot.Manifest.Status),
            _ => throw new InvalidOperationException($"Review decision '{request.Decision}' is not supported."),
        };

        var summary = request.Decision switch
        {
            ReviewDecision.Approve => "Task approved for worker execution.",
            ReviewDecision.Revise => "Task returned to draft for revision.",
            _ => "Review decision recorded.",
        };

        var notes = request.Notes.Trim();
        var outputText = string.IsNullOrWhiteSpace(notes)
            ? summary
            : $"{summary}\n\nNotes: {notes}";

        var runId = Guid.NewGuid().ToString("N");
        var stepId = Guid.NewGuid().ToString("N");
        var referencedAliases = latestReviewRun.Steps
            .SelectMany(static step => step.ReferencedArtifactAliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var step = new StepManifest
        {
            Id = stepId,
            StepType = TaskStepType.UserDecision,
            Status = TaskStepStatus.Completed,
            Attempt = 1,
            Provider = UserProvider,
            Model = UserDecisionModel,
            PromptVersion = PromptVersion,
            Summary = summary,
            ReferencedArtifactAliases = referencedAliases,
            StartedAtUtc = completedAtUtc,
            CompletedAtUtc = completedAtUtc,
        };

        var run = new RunManifest
        {
            Id = runId,
            Title = TaskRunKind.UserDecision.GetDisplayName(),
            Kind = TaskRunKind.UserDecision,
            Status = TaskRunStatus.Completed,
            Sequence = NextSequence(snapshot.Manifest),
            Summary = summary,
            StartedAtUtc = completedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Steps = [step],
        };

        await _taskWorkspaceStore.SaveRunAsync(workspaceRootPath, snapshot.Manifest.Id, run, cancellationToken);
        await _taskWorkspaceStore.SaveStepArtifactsAsync(
            workspaceRootPath,
            snapshot.Manifest.Id,
            runId,
            stepId,
            new StepArtifactsPayload
            {
                PromptMarkdown = BuildPromptMarkdown(snapshot, latestReviewRun),
                ResponseMarkdown = BuildResponseMarkdown(request, snapshot.Manifest.Status, targetStatus, latestReviewRun),
            },
            cancellationToken);

        var latestSnapshot = await _taskWorkspaceStore.LoadTaskAsync(workspaceRootPath, snapshot.Manifest.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Task '{snapshot.Manifest.Id}' was not found after saving the user decision run.");

        var updatedManifest = latestSnapshot.Manifest with
        {
            Status = targetStatus,
            UpdatedAtUtc = completedAtUtc,
        };

        await _taskWorkspaceStore.SaveTaskAsync(workspaceRootPath, updatedManifest, latestSnapshot.TaskMarkdown, cancellationToken);

        return new ReviewDecisionResult
        {
            TaskId = snapshot.Manifest.Id,
            RunId = runId,
            StepId = stepId,
            Decision = request.Decision,
            TaskStatus = targetStatus,
            OutputText = outputText,
            Summary = summary,
            PromptVersion = PromptVersion,
        };
    }

    private static int NextSequence(TaskManifest manifest)
    {
        return manifest.Runs.Count == 0 ? 1 : manifest.Runs.Max(static run => run.Sequence) + 1;
    }

    private static string BuildPromptMarkdown(TaskWorkspaceSnapshot snapshot, RunManifest latestReviewRun)
    {
        var builder = new StringBuilder();
        builder.Append("# User Decision Context (").Append(PromptVersion).AppendLine(")");
        builder.AppendLine();
        builder.Append("- Task: ").AppendLine(snapshot.Manifest.Title);
        builder.Append("- Current status: ").AppendLine(snapshot.Manifest.Status.GetDisplayName());
        builder.Append("- Latest review run: ")
            .Append(latestReviewRun.Sequence.ToString("0000", CultureInfo.InvariantCulture))
            .Append(' ')
            .AppendLine(latestReviewRun.Status.GetDisplayName());

        if (!string.IsNullOrWhiteSpace(latestReviewRun.Summary))
        {
            builder.Append("- Latest review summary: ").AppendLine(latestReviewRun.Summary);
        }

        builder.AppendLine();
        builder.AppendLine("Record the explicit user decision for the review gate without overwriting prior review artifacts.");
        return builder.ToString().TrimEnd();
    }

    private static string BuildResponseMarkdown(
        ReviewDecisionRequest request,
        TaskLifecycleState previousStatus,
        TaskLifecycleState targetStatus,
        RunManifest latestReviewRun)
    {
        var notes = request.Notes.Trim();
        var builder = new StringBuilder();
        builder.AppendLine("# User Decision");
        builder.AppendLine();
        builder.Append("- Decision: ").AppendLine(request.Decision == ReviewDecision.Approve ? "Approve" : "Revise");
        builder.Append("- Previous status: ").AppendLine(previousStatus.GetDisplayName());
        builder.Append("- New status: ").AppendLine(targetStatus.GetDisplayName());
        builder.Append("- Review run: ").AppendLine(latestReviewRun.Id);

        if (!string.IsNullOrWhiteSpace(notes))
        {
            builder.AppendLine();
            builder.AppendLine("## Notes");
            builder.AppendLine(notes);
        }

        return builder.ToString().TrimEnd();
    }
}
