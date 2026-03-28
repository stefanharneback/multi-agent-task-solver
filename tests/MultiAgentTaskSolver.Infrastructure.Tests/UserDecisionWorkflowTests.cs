using MultiAgentTaskSolver.Core;
using MultiAgentTaskSolver.Core.Models;
using MultiAgentTaskSolver.Infrastructure.Execution;
using MultiAgentTaskSolver.Infrastructure.FileSystem;

namespace MultiAgentTaskSolver.Infrastructure.Tests;

public sealed class UserDecisionWorkflowTests : IDisposable
{
    private readonly string _tempRootPath = Path.Combine(Path.GetTempPath(), "mats-user-decision-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RunAsyncPersistsApprovedDecisionAndUpdatesTaskStatus()
    {
        var store = new FileSystemTaskWorkspaceStore();
        var created = await store.CreateTaskAsync(_tempRootPath, new CreateTaskRequest
        {
            Title = "Approve ready task",
            Summary = "Approve the reviewed task.",
            TaskMarkdown = "# Task\nReady for worker approval.",
        });

        await SeedCompletedReviewAsync(store, _tempRootPath, created.Manifest.Id, "Task is review-ready.");

        var snapshot = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(snapshot);

        var workflow = new UserDecisionWorkflow(store);
        var result = await workflow.RunAsync(
            _tempRootPath,
            snapshot!,
            new ReviewDecisionRequest
            {
                Decision = ReviewDecision.Approve,
            });

        var reloaded = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(TaskLifecycleState.WorkApproved, reloaded!.Manifest.Status);
        Assert.Equal(2, reloaded.Manifest.Runs.Count);
        Assert.Equal(TaskRunKind.UserDecision, reloaded.Manifest.Runs[1].Kind);
        Assert.Equal(TaskRunStatus.Completed, reloaded.Manifest.Runs[1].Status);
        Assert.Equal(TaskStepType.UserDecision, reloaded.Manifest.Runs[1].Steps[0].StepType);
        Assert.Equal("Task approved for worker execution.", result.Summary);

        var stepDirectoryPath = Path.Combine(reloaded.TaskRootPath, "runs", "0002-user-decision", "01-user-decision");
        Assert.Contains("user-decision-v1", await File.ReadAllTextAsync(Path.Combine(stepDirectoryPath, "prompt.md")), StringComparison.Ordinal);
        Assert.Contains("Approve", await File.ReadAllTextAsync(Path.Combine(stepDirectoryPath, "response.md")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncPersistsReviseDecisionAndReturnsTaskToDraft()
    {
        var store = new FileSystemTaskWorkspaceStore();
        var created = await store.CreateTaskAsync(_tempRootPath, new CreateTaskRequest
        {
            Title = "Revise task",
            Summary = "Send the reviewed task back to draft.",
            TaskMarkdown = "# Task\nNeeds another pass.",
        });

        await SeedCompletedReviewAsync(store, _tempRootPath, created.Manifest.Id, "Task needs a tighter summary.");

        var snapshot = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(snapshot);

        var workflow = new UserDecisionWorkflow(store);
        var result = await workflow.RunAsync(
            _tempRootPath,
            snapshot!,
            new ReviewDecisionRequest
            {
                Decision = ReviewDecision.Revise,
                Notes = "Clarify the acceptance criteria before approving.",
            });

        var reloaded = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(TaskLifecycleState.Draft, reloaded!.Manifest.Status);
        Assert.Equal(TaskRunKind.UserDecision, reloaded.Manifest.Runs[1].Kind);
        Assert.Equal("Task returned to draft for revision.", result.Summary);

        var stepDirectoryPath = Path.Combine(reloaded.TaskRootPath, "runs", "0002-user-decision", "01-user-decision");
        Assert.Contains("Clarify the acceptance criteria", await File.ReadAllTextAsync(Path.Combine(stepDirectoryPath, "response.md")), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootPath))
        {
            Directory.Delete(_tempRootPath, recursive: true);
        }
    }

    private static async Task SeedCompletedReviewAsync(
        FileSystemTaskWorkspaceStore store,
        string workspaceRootPath,
        string taskId,
        string reviewSummary)
    {
        var snapshot = await store.LoadTaskAsync(workspaceRootPath, taskId)
            ?? throw new InvalidOperationException($"Task '{taskId}' was not found.");

        var updatedManifest = snapshot.Manifest with
        {
            Status = TaskLifecycleState.ReviewReady,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        await store.SaveTaskAsync(workspaceRootPath, updatedManifest, snapshot.TaskMarkdown);

        var runId = Guid.NewGuid().ToString("N");
        var stepId = Guid.NewGuid().ToString("N");
        var completedAtUtc = DateTimeOffset.UtcNow;
        var run = new RunManifest
        {
            Id = runId,
            Title = TaskRunKind.TaskReview.GetDisplayName(),
            Kind = TaskRunKind.TaskReview,
            Status = TaskRunStatus.Completed,
            Sequence = 1,
            Summary = reviewSummary,
            StartedAtUtc = completedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Steps =
            [
                new StepManifest
                {
                    Id = stepId,
                    StepType = TaskStepType.TaskReview,
                    Status = TaskStepStatus.Completed,
                    Provider = new ProviderRef
                    {
                        ProviderId = "openai",
                        DisplayName = "OpenAI via Gateway",
                        BaseUrl = "http://localhost:3000",
                    },
                    Model = new ModelRef
                    {
                        ProviderId = "openai",
                        ModelId = "gpt-5.4-mini",
                        DisplayName = "GPT-5.4 Mini",
                    },
                    PromptVersion = "task-review-v1",
                    Summary = reviewSummary,
                    StartedAtUtc = completedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                },
            ],
        };

        await store.SaveRunAsync(workspaceRootPath, taskId, run);
        await store.SaveStepArtifactsAsync(
            workspaceRootPath,
            taskId,
            runId,
            stepId,
            new StepArtifactsPayload
            {
                PromptMarkdown = "# Prompt\nReview prompt.",
                ResponseMarkdown = $"# Output\n{reviewSummary}",
            });
    }
}
