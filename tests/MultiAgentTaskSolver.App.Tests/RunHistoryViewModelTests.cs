using MultiAgentTaskSolver.App.ViewModels;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.Tests;

public sealed class RunHistoryViewModelTests
{
    [Fact]
    public async Task LoadAsyncPopulatesRunEntries()
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
                            StartedAtUtc = new DateTimeOffset(2026, 3, 26, 11, 0, 0, TimeSpan.Zero),
                        },
                    ],
                },
            ]);

        var viewModel = new RunHistoryViewModel(coordinator);

        await viewModel.LoadAsync("task-001");

        Assert.Equal("task-001", viewModel.TaskId);
        Assert.Equal("Task", viewModel.TaskTitle);
        Assert.Single(viewModel.Runs);
        Assert.Equal("Review summary", viewModel.Runs[0].Summary);
    }
}
