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
        Assert.Single(viewModel.WorkerModels);
        Assert.Equal("gpt-5", viewModel.SelectedReviewModel?.ModelId);
        Assert.Equal("gpt-5", viewModel.SelectedWorkerModel?.ModelId);
        Assert.True(viewModel.CanRunTaskReview);
        Assert.False(viewModel.CanApplyReviewDecision);
        Assert.False(viewModel.CanRunWorker);
        Assert.Equal("Usage not recorded.", viewModel.TaskUsageSummaryText);
        Assert.Equal("Usage not recorded.", viewModel.LatestReviewUsageText);
        Assert.Equal("Usage not recorded.", viewModel.LatestWorkerUsageText);
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
    public async Task SaveAsyncPersistsConfiguredInputAndOutputPaths()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot("task-001", "Task", "Summary", TaskLifecycleState.Draft);

        var viewModel = new TaskDetailsViewModel(coordinator, new FakeNavigationService(), new FakeFilePickerService());
        await viewModel.LoadAsync("task-001");
        viewModel.InputPathsText = "research/articles\ninputs/contracts";
        viewModel.OutputPathsText = "deliverables/final-report.md";

        await viewModel.SaveAsync();

        var savedTask = Assert.Single(coordinator.SavedTasks);
        Assert.Equal(["inputs/research/articles", "inputs/contracts"], savedTask.Manifest.InputPaths);
        Assert.Equal(["outputs/deliverables/final-report.md"], savedTask.Manifest.OutputPaths);
    }

    [Fact]
    public async Task SaveAsyncAllowsClearingDeclaredOutputPaths()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot("task-001", "Task", "Summary", TaskLifecycleState.Draft);

        var viewModel = new TaskDetailsViewModel(coordinator, new FakeNavigationService(), new FakeFilePickerService());
        await viewModel.LoadAsync("task-001");
        viewModel.OutputPathsText = string.Empty;

        await viewModel.SaveAsync();

        var savedTask = Assert.Single(coordinator.SavedTasks);
        Assert.Empty(savedTask.Manifest.OutputPaths);
    }

    [Fact]
    public async Task ImportPickedFolderAsyncUsesSelectedFolderPath()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot("task-001", "Task", "Summary", TaskLifecycleState.Draft);
        var folderPicker = new FakeFolderPickerService
        {
            SelectedPath = "C:\\temp\\source-folder",
        };

        var viewModel = new TaskDetailsViewModel(
            coordinator,
            new FakeNavigationService(),
            new FakeFilePickerService(),
            folderPicker);
        await viewModel.LoadAsync("task-001");

        await viewModel.ImportPickedFolderAsync();

        var importCall = Assert.Single(coordinator.ImportedArtifacts);
        Assert.Equal(["C:\\temp\\source-folder"], importCall.SourcePaths);
        Assert.Equal("inputs/articles", importCall.DestinationRelativeDirectory);
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
            Usage = new UsageRecord
            {
                ProviderId = "openai",
                ModelId = request.ModelId,
                InputTokens = 12,
                OutputTokens = 6,
                TotalTokens = 18,
                DurationMs = 140,
            },
        };

        var viewModel = new TaskDetailsViewModel(coordinator, new FakeNavigationService(), new FakeFilePickerService());
        await viewModel.LoadAsync("task-001");

        await viewModel.RunTaskReviewAsync();

        Assert.Equal("Under Review", viewModel.TaskStatusText);
        Assert.Equal("Needs clearer acceptance criteria.", viewModel.LatestReviewSummary);
        Assert.Equal("Detailed review output.", viewModel.LatestReviewOutput);
        Assert.Equal("18 total tokens | 12 input | 6 output | 140 ms", viewModel.LatestReviewUsageText);
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
        Assert.False(viewModel.CanRunTaskReview);
        Assert.False(viewModel.CanApplyReviewDecision);
        Assert.True(viewModel.CanRunWorker);
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
        Assert.True(viewModel.CanRunTaskReview);
        Assert.False(viewModel.CanApplyReviewDecision);
        Assert.False(viewModel.CanRunWorker);
    }

    [Fact]
    public async Task RunWorkerAsyncReloadsLatestWorkerState()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot(
            "task-001",
            "Task",
            "Summary",
            TaskLifecycleState.WorkApproved,
            runs:
            [
                new RunManifest
                {
                    Id = "run-review-001",
                    Kind = TaskRunKind.TaskReview,
                    Status = TaskRunStatus.Completed,
                    Sequence = 1,
                    Summary = "Looks ready.",
                },
                new RunManifest
                {
                    Id = "run-decision-001",
                    Kind = TaskRunKind.UserDecision,
                    Status = TaskRunStatus.Completed,
                    Sequence = 2,
                    Summary = "Task approved for worker execution.",
                },
            ]);
        coordinator.LoadTaskHandler = taskId =>
        {
            if (coordinator.WorkerRequests.Count == 0)
            {
                return coordinator.Snapshots[taskId];
            }

            return TestData.CreateSnapshot(
                taskId,
                "Task",
                "Summary",
                TaskLifecycleState.Working,
                runs:
                [
                    new RunManifest
                    {
                        Id = "run-review-001",
                        Kind = TaskRunKind.TaskReview,
                        Status = TaskRunStatus.Completed,
                        Sequence = 1,
                        Summary = "Looks ready.",
                    },
                    new RunManifest
                    {
                        Id = "run-decision-001",
                        Kind = TaskRunKind.UserDecision,
                        Status = TaskRunStatus.Completed,
                        Sequence = 2,
                        Summary = "Task approved for worker execution.",
                    },
                    new RunManifest
                    {
                        Id = "run-worker-001",
                        Kind = TaskRunKind.Worker,
                        Status = TaskRunStatus.Completed,
                        Sequence = 3,
                        Summary = "Worker produced a first draft.",
                    },
                ]);
        };
        coordinator.RunWorkerHandler = (request, taskId) => new TaskWorkerResult
        {
            TaskId = taskId,
            RunId = "run-worker-001",
            StepId = "step-worker-001",
            TaskStatus = TaskLifecycleState.Working,
            Summary = "Worker produced a first draft.",
            OutputText = "## Outcome Summary\nDraft complete.",
            PromptVersion = "worker-v1",
            OutputArtifactPaths = ["outputs/0003-worker/worker-output.md"],
            Usage = new UsageRecord
            {
                ProviderId = "openai",
                ModelId = request.ModelId,
                InputTokens = 20,
                OutputTokens = 10,
                TotalTokens = 30,
                DurationMs = 320,
                TotalCostUsd = 0.0042m,
            },
        };

        var viewModel = new TaskDetailsViewModel(coordinator, new FakeNavigationService(), new FakeFilePickerService());
        await viewModel.LoadAsync("task-001");

        Assert.False(viewModel.CanRunTaskReview);
        Assert.True(viewModel.CanRunWorker);

        await viewModel.RunWorkerAsync();

        Assert.Equal("Working", viewModel.TaskStatusText);
        Assert.Single(coordinator.WorkerRequests);
        Assert.Equal("Worker produced a first draft.", viewModel.LatestWorkerSummary);
        Assert.Equal("## Outcome Summary\nDraft complete.", viewModel.LatestWorkerOutput);
        Assert.Equal("30 total tokens | 20 input | 10 output | 320 ms | $0.0042", viewModel.LatestWorkerUsageText);
        Assert.False(viewModel.CanRunTaskReview);
    }

    [Fact]
    public async Task LoadAsyncReadsLatestWorkerOutputFromOutputArtifact()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        var taskRootPath = Path.Combine(Path.GetTempPath(), "mats-task-details-worker-tests", Guid.NewGuid().ToString("N"));
        var outputDirectoryPath = Path.Combine(taskRootPath, "outputs", "0003-worker");
        var runDirectoryPath = Path.Combine(taskRootPath, "runs", "0003-worker", "01-worker");
        Directory.CreateDirectory(outputDirectoryPath);
        Directory.CreateDirectory(runDirectoryPath);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectoryPath, "worker-output.md"),
            """
            ## Outcome Summary
            Draft complete.

            ## Deliverable
            First worker output.
            """);
        await File.WriteAllTextAsync(
            Path.Combine(runDirectoryPath, "usage.json"),
            """
            {
              "providerId": "openai",
              "modelId": "gpt-5",
              "inputTokens": 20,
              "outputTokens": 10,
              "totalTokens": 30,
              "durationMs": 750,
              "totalCostUsd": 0.0042
            }
            """);

        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot(
            "task-001",
            "Task",
            "Summary",
            TaskLifecycleState.Working,
            runs:
            [
                new RunManifest
                {
                    Id = "run-003",
                    Kind = TaskRunKind.Worker,
                    Status = TaskRunStatus.Completed,
                    Sequence = 3,
                    Summary = "Worker produced a first draft.",
                    Steps =
                    [
                        new StepManifest
                        {
                            Id = "step-003",
                            RelativeDirectory = "runs/0003-worker/01-worker",
                            OutputArtifactPaths = ["outputs/0003-worker/worker-output.md"],
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
            Assert.Equal("Worker produced a first draft.", viewModel.LatestWorkerSummary);
            Assert.Equal("## Outcome Summary\nDraft complete.\n\n## Deliverable\nFirst worker output.", viewModel.LatestWorkerOutput);
            Assert.Equal("30 total tokens | 20 input | 10 output | 750 ms | $0.0042", viewModel.LatestWorkerUsageText);
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
    public async Task LoadAsyncBuildsTaskUsageSummaryAcrossSavedSteps()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.ModelsByProvider["openai"] = [TestData.CreateTextModel("gpt-5", "GPT-5")];
        var taskRootPath = Path.Combine(Path.GetTempPath(), "mats-task-usage-tests", Guid.NewGuid().ToString("N"));
        var reviewDirectoryPath = Path.Combine(taskRootPath, "runs", "0001-task-review", "01-task-review");
        var workerDirectoryPath = Path.Combine(taskRootPath, "runs", "0003-worker", "01-worker");
        Directory.CreateDirectory(reviewDirectoryPath);
        Directory.CreateDirectory(workerDirectoryPath);
        await File.WriteAllTextAsync(
            Path.Combine(reviewDirectoryPath, "usage.json"),
            """
            {
              "providerId": "openai",
              "modelId": "gpt-5",
              "inputTokens": 12,
              "outputTokens": 6,
              "totalTokens": 18,
              "durationMs": 140
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(workerDirectoryPath, "usage.json"),
            """
            {
              "providerId": "openai",
              "modelId": "gpt-5",
              "inputTokens": 20,
              "outputTokens": 10,
              "cachedInputTokens": 4,
              "reasoningTokens": 2,
              "totalTokens": 30,
              "durationMs": 320,
              "totalCostUsd": 0.0042
            }
            """);

        coordinator.Snapshots["task-001"] = TestData.CreateSnapshot(
            "task-001",
            "Task",
            "Summary",
            TaskLifecycleState.Working,
            runs:
            [
                new RunManifest
                {
                    Id = "run-001",
                    Kind = TaskRunKind.TaskReview,
                    Status = TaskRunStatus.Completed,
                    Sequence = 1,
                    Steps =
                    [
                        new StepManifest
                        {
                            Id = "step-review-001",
                            RelativeDirectory = "runs/0001-task-review/01-task-review",
                        },
                    ],
                },
                new RunManifest
                {
                    Id = "run-003",
                    Kind = TaskRunKind.Worker,
                    Status = TaskRunStatus.Completed,
                    Sequence = 3,
                    Steps =
                    [
                        new StepManifest
                        {
                            Id = "step-worker-001",
                            RelativeDirectory = "runs/0003-worker/01-worker",
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
            Assert.Equal("48 total tokens | 32 input | 16 output | 4 cached | 2 reasoning | 460 ms | $0.0042", viewModel.TaskUsageSummaryText);
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
        await File.WriteAllTextAsync(
            Path.Combine(responseDirectoryPath, "usage.json"),
            """
            {
              "providerId": "openai",
              "modelId": "gpt-5",
              "inputTokens": 12,
              "outputTokens": 6,
              "totalTokens": 18,
              "durationMs": 140
            }
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
            Assert.Equal("18 total tokens | 12 input | 6 output | 140 ms", viewModel.LatestReviewUsageText);
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
