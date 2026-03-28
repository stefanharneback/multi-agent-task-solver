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
        Assert.False(viewModel.CanApplyReviewDecision);
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
        Assert.False(viewModel.CanApplyReviewDecision);
    }

    [Fact]
    public async Task ApproveReviewAsyncReloadsLatestTaskState()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot(
            "task-001",
            "Task",
            "Summary",
            TaskLifecycleState.ReviewReady,
            runs:
            [
                new RunManifest
                {
                    Id = "run-001",
                    Kind = TaskRunKind.TaskReview,
                    Status = TaskRunStatus.Completed,
                    Sequence = 1,
                    Summary = "Looks ready.",
                    StartedAtUtc = new DateTimeOffset(2026, 3, 26, 10, 0, 0, TimeSpan.Zero),
                    CompletedAtUtc = new DateTimeOffset(2026, 3, 26, 10, 1, 0, TimeSpan.Zero),
                },
            ]);
        coordinator.LoadTaskHandler = taskId =>
        {
            if (coordinator.ReviewDecisionRequests.Count == 0)
            {
                return coordinator.Snapshots[taskId];
            }

            return TestData.CreateSnapshot(
                taskId,
                "Task",
                "Summary",
                TaskLifecycleState.WorkApproved,
                runs:
                [
                    new RunManifest
                    {
                        Id = "run-001",
                        Kind = TaskRunKind.TaskReview,
                        Status = TaskRunStatus.Completed,
                        Sequence = 1,
                        Summary = "Looks ready.",
                    },
                    new RunManifest
                    {
                        Id = "run-002",
                        Kind = TaskRunKind.UserDecision,
                        Status = TaskRunStatus.Completed,
                        Sequence = 2,
                        Summary = "Task approved for worker execution.",
                    },
                ]);
        };
        coordinator.ApplyReviewDecisionHandler = (request, taskId) => new ReviewDecisionResult
        {
            TaskId = taskId,
            RunId = "run-002",
            StepId = "step-002",
            Decision = request.Decision,
            TaskStatus = TaskLifecycleState.WorkApproved,
            Summary = "Task approved for worker execution.",
            OutputText = "Task approved for worker execution.",
            PromptVersion = "user-decision-v1",
        };

        var viewModel = new TaskDetailsViewModel(coordinator, new FakeNavigationService(), new FakeFilePickerService());
        await viewModel.LoadAsync("task-001");

        Assert.True(viewModel.CanApplyReviewDecision);

        await viewModel.ApproveReviewAsync();

        Assert.Equal("Work Approved", viewModel.TaskStatusText);
        Assert.Single(coordinator.ReviewDecisionRequests);
        Assert.Equal(ReviewDecision.Approve, coordinator.ReviewDecisionRequests[0].Request.Decision);
        Assert.False(viewModel.CanApplyReviewDecision);
    }

    [Fact]
    public async Task ReviseReviewAsyncReloadsTaskAsDraft()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot(
            "task-001",
            "Task",
            "Summary",
            TaskLifecycleState.ReviewReady,
            runs:
            [
                new RunManifest
                {
                    Id = "run-001",
                    Kind = TaskRunKind.TaskReview,
                    Status = TaskRunStatus.Completed,
                    Sequence = 1,
                    Summary = "Needs sharper acceptance criteria.",
                },
            ]);
        coordinator.LoadTaskHandler = taskId =>
        {
            if (coordinator.ReviewDecisionRequests.Count == 0)
            {
                return coordinator.Snapshots[taskId];
            }

            return TestData.CreateSnapshot(
                taskId,
                "Task",
                "Summary",
                TaskLifecycleState.Draft,
                runs:
                [
                    new RunManifest
                    {
                        Id = "run-001",
                        Kind = TaskRunKind.TaskReview,
                        Status = TaskRunStatus.Completed,
                        Sequence = 1,
                        Summary = "Needs sharper acceptance criteria.",
                    },
                    new RunManifest
                    {
                        Id = "run-002",
                        Kind = TaskRunKind.UserDecision,
                        Status = TaskRunStatus.Completed,
                        Sequence = 2,
                        Summary = "Task returned to draft for revision.",
                    },
                ]);
        };
        coordinator.ApplyReviewDecisionHandler = (request, taskId) => new ReviewDecisionResult
        {
            TaskId = taskId,
            RunId = "run-002",
            StepId = "step-002",
            Decision = request.Decision,
            TaskStatus = TaskLifecycleState.Draft,
            Summary = "Task returned to draft for revision.",
            OutputText = "Task returned to draft for revision.",
            PromptVersion = "user-decision-v1",
        };

        var viewModel = new TaskDetailsViewModel(coordinator, new FakeNavigationService(), new FakeFilePickerService());
        await viewModel.LoadAsync("task-001");

        await viewModel.ReviseReviewAsync();

        Assert.Equal("Draft", viewModel.TaskStatusText);
        Assert.Single(coordinator.ReviewDecisionRequests);
        Assert.Equal(ReviewDecision.Revise, coordinator.ReviewDecisionRequests[0].Request.Decision);
        Assert.False(viewModel.CanApplyReviewDecision);
    }

    [Fact]
    public async Task LoadAsyncReadsLatestReviewOutputFromResponseMarkdown()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        var taskRootPath = Path.Combine(Path.GetTempPath(), "mats-task-details-tests", Guid.NewGuid().ToString("N"));
        var responseDirectoryPath = Path.Combine(taskRootPath, "runs", "0001-task-review", "01-task-review");
        Directory.CreateDirectory(responseDirectoryPath);
        await File.WriteAllTextAsync(
            Path.Combine(responseDirectoryPath, "response.md"),
            """
            # Output
            Detailed review output from file.

            ## Raw Response
            ```json
            {"output_text":"Detailed review output from file."}
            ```
            """);

        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot(
            "task-001",
            "Task",
            "Summary",
            TaskLifecycleState.ReviewReady,
            runs:
            [
                new RunManifest
                {
                    Id = "run-001",
                    Kind = TaskRunKind.TaskReview,
                    Status = TaskRunStatus.Completed,
                    Sequence = 1,
                    Summary = "Short summary.",
                    Steps =
                    [
                        new StepManifest
                        {
                            Id = "step-001",
                            RelativeDirectory = "runs/0001-task-review/01-task-review",
                            ResponsePath = "response.md",
                        },
                    ],
                },
            ]) with
        {
            TaskRootPath = taskRootPath,
        };

        var viewModel = new TaskDetailsViewModel(coordinator, new FakeNavigationService(), new FakeFilePickerService());

        try
        {
            await viewModel.LoadAsync("task-001");
            Assert.Equal("Detailed review output from file.", viewModel.LatestReviewOutput);
            Assert.Equal("Short summary.", viewModel.LatestReviewSummary);
        }
        finally
        {
            if (Directory.Exists(taskRootPath))
            {
                Directory.Delete(taskRootPath, recursive: true);
            }
        }
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
