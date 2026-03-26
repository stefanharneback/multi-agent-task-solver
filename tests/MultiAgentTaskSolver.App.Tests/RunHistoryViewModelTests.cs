using MultiAgentTaskSolver.App.ViewModels;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.Tests;

public sealed class RunHistoryViewModelTests
{
    [Fact]
    public async Task LoadAsyncBuildsFlowStagesAndDetailedRunHistory()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot(
            "task-001",
            "Task",
            "Summary",
            TaskLifecycleState.UnderReview,
            runs:
            [
                new RunManifest
                {
                    Id = "run-001",
                    Kind = TaskRunKind.TaskReview,
                    Status = TaskRunStatus.Completed,
                    Sequence = 1,
                    Summary = "Review summary",
                    StartedAtUtc = new DateTimeOffset(2026, 3, 26, 11, 0, 0, TimeSpan.Zero),
                    Steps =
                    [
                        new StepManifest
                        {
                            Id = "step-001",
                            StepType = TaskStepType.TaskReview,
                            Status = TaskStepStatus.Completed,
                            Provider = new ProviderRef
                            {
                                ProviderId = "openai",
                                DisplayName = "OpenAI via Gateway",
                            },
                            Model = TestData.CreateTextModel("gpt-5", "GPT-5"),
                            PromptVersion = "task-review-v1",
                            ReferencedArtifactAliases = ["spec", "@notes"],
                            Summary = "Step summary",
                            StartedAtUtc = new DateTimeOffset(2026, 3, 26, 11, 0, 0, TimeSpan.Zero),
                            CompletedAtUtc = new DateTimeOffset(2026, 3, 26, 11, 3, 0, TimeSpan.Zero),
                        },
                    ],
                },
            ]);

        var viewModel = new RunHistoryViewModel(coordinator);

        await viewModel.LoadAsync("task-001");

        Assert.Equal("task-001", viewModel.TaskId);
        Assert.Equal("Task", viewModel.TaskTitle);
        Assert.Equal("Under Review", viewModel.TaskStatusText);
        Assert.NotEmpty(viewModel.ProgressNarrative);
        Assert.Equal(4, viewModel.SummaryMetrics.Count);
        Assert.Equal(5, viewModel.FlowStages.Count);
        Assert.Equal("Current", viewModel.FlowStages[1].StateText);
        Assert.Single(viewModel.Runs);
        Assert.Equal("Review summary", viewModel.Runs[0].Summary);
        Assert.Equal("Run 1", viewModel.Runs[0].SequenceText);
        Assert.Equal("GPT-5", viewModel.Runs[0].ModelText);
        Assert.Equal("@spec, @notes", viewModel.Runs[0].ReferencedAliasesText);
        Assert.Single(viewModel.Runs[0].Steps);
        Assert.Equal("Task review step", viewModel.Runs[0].Steps[0].Title);
        Assert.Equal("Completed", viewModel.Runs[0].Steps[0].Status);
        Assert.Equal("Prompt task-review-v1", viewModel.Runs[0].Steps[0].PromptVersionText);
        Assert.Equal("@spec, @notes", viewModel.Runs[0].Steps[0].ReferencedAliasesText);
    }

    [Fact]
    public async Task LoadAsyncWithoutRunsShowsDraftProgressNarrative()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot(
            "task-001",
            "Task",
            "Summary",
            TaskLifecycleState.Draft);

        var viewModel = new RunHistoryViewModel(coordinator);

        await viewModel.LoadAsync("task-001");

        Assert.Equal("Draft", viewModel.TaskStatusText);
        Assert.Equal("Current", viewModel.FlowStages[0].StateText);
        Assert.Empty(viewModel.Runs);
        Assert.Equal("The task has been created, but no agent run has started yet.", viewModel.ProgressNarrative);
    }
}
