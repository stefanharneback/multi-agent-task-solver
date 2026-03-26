using MultiAgentTaskSolver.App.ViewModels;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.Tests;

public sealed class TaskDetailsViewModelTests
{
    [Fact]
    public async Task LoadAsyncPopulatesArtifactsAndModels()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot(
            "task-001",
            "Task",
            "Summary",
            TaskLifecycleState.Draft,
            artifacts:
            [
                new ArtifactManifest
                {
                    Id = "artifact-001",
                    Alias = "@spec",
                    RelativePath = "inputs/documents/spec.md",
                    MediaType = "text/markdown",
                    SizeBytes = 42,
                },
            ]);

        var viewModel = new TaskDetailsViewModel(coordinator, new FakeNavigationService(), new FakeFilePickerService());

        await viewModel.LoadAsync("task-001");

        Assert.Equal("Task", viewModel.Title);
        Assert.Single(viewModel.Artifacts);
        Assert.Single(viewModel.ReviewModels);
        Assert.Equal("gpt-5", viewModel.SelectedReviewModel?.ModelId);
    }

    [Fact]
    public async Task ImportFilesAsyncReloadsTaskStateAfterImport()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot("task-001", "Task", "Summary", TaskLifecycleState.Draft);

        coordinator.LoadTaskHandler = taskId =>
        {
            if (coordinator.ImportedArtifacts.Count == 0)
            {
                return coordinator.Snapshots[taskId];
            }

            return TestData.CreateSnapshot(
                taskId,
                "Task",
                "Summary",
                TaskLifecycleState.Draft,
                artifacts:
                [
                    new ArtifactManifest
                    {
                        Id = "artifact-001",
                        Alias = "@spec",
                        RelativePath = "inputs/documents/spec.md",
                        MediaType = "text/markdown",
                        SizeBytes = 64,
                    },
                ]);
        };

        var viewModel = new TaskDetailsViewModel(coordinator, new FakeNavigationService(), new FakeFilePickerService());
        await viewModel.LoadAsync("task-001");

        await viewModel.ImportFilesAsync(["C:\\temp\\spec.md"]);

        Assert.Single(coordinator.ImportedArtifacts);
        Assert.Single(viewModel.Artifacts);
        Assert.Equal("@spec", viewModel.Artifacts[0].Alias);
    }

    [Fact]
    public async Task RunTaskReviewAsyncReloadsLatestTaskState()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot("task-001", "Task", "Summary", TaskLifecycleState.ReviewReady);
        coordinator.LoadTaskHandler = taskId =>
        {
            if (coordinator.ReviewRequests.Count == 0)
            {
                return coordinator.Snapshots[taskId];
            }

            return TestData.CreateSnapshot(
                taskId,
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
                        Summary = "Needs clearer acceptance criteria.",
                        StartedAtUtc = new DateTimeOffset(2026, 3, 26, 10, 0, 0, TimeSpan.Zero),
                        CompletedAtUtc = new DateTimeOffset(2026, 3, 26, 10, 1, 0, TimeSpan.Zero),
                    },
                ]);
        };
        coordinator.RunTaskReviewHandler = (request, taskId) => new TaskReviewResult
        {
            TaskId = taskId,
            RunId = "run-001",
            StepId = "step-001",
            TaskStatus = TaskLifecycleState.UnderReview,
            Summary = "Needs clearer acceptance criteria.",
            OutputText = "Detailed review output.",
            PromptVersion = "task-review-v1",
        };

        var viewModel = new TaskDetailsViewModel(coordinator, new FakeNavigationService(), new FakeFilePickerService());
        await viewModel.LoadAsync("task-001");

        await viewModel.RunTaskReviewAsync();

        Assert.Equal("Under Review", viewModel.TaskStatusText);
        Assert.Equal("Needs clearer acceptance criteria.", viewModel.LatestReviewSummary);
        Assert.Equal("Detailed review output.", viewModel.LatestReviewOutput);
        Assert.Single(coordinator.ReviewRequests);
    }

    [Fact]
    public async Task OpenRunHistoryAsyncNavigatesForLoadedTask()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot("task-001", "Task", "Summary", TaskLifecycleState.Draft);
        var navigation = new FakeNavigationService();
        var viewModel = new TaskDetailsViewModel(coordinator, navigation, new FakeFilePickerService());
        await viewModel.LoadAsync("task-001");

        await viewModel.OpenRunHistoryAsync();

        Assert.Equal(["task-001"], navigation.RunHistoryNavigations);
    }
}
