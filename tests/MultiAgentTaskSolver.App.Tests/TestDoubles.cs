using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.Core;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.Tests;

internal sealed class FakeTaskWorkspaceCoordinator : ITaskWorkspaceCoordinator
{
    public AppSettings Settings { get; set; } = new()
    {
        WorkspaceRootPath = "C:\\workspace",
        OpenAiGatewayBaseUrl = "https://gateway.example.test",
    };

    public string? OpenAiBearerToken { get; set; } = "token";

    public List<TaskManifest> Tasks { get; } = [];

    public Dictionary<string, TaskWorkspaceSnapshot> Snapshots { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<ModelRef>> ModelsByProvider { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<(TaskManifest Manifest, string Markdown)> SavedTasks { get; } = [];

    public List<(string TaskId, string DestinationRelativeDirectory, IReadOnlyList<string> SourcePaths)> ImportedArtifacts { get; } = [];

    public List<(TaskReviewRequest Request, string TaskId)> ReviewRequests { get; } = [];

    public List<(AppSettings Settings, string? BearerToken)> SavedSettings { get; } = [];

    public Func<CreateTaskRequest, TaskWorkspaceSnapshot>? CreateTaskHandler { get; set; }

    public Func<string, TaskWorkspaceSnapshot?>? LoadTaskHandler { get; set; }

    public Func<TaskReviewRequest, string, TaskReviewResult>? RunTaskReviewHandler { get; set; }

    public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Settings);
    }

    public Task SaveSettingsAsync(AppSettings settings, string? openAiBearerToken, CancellationToken cancellationToken = default)
    {
        Settings = settings;
        OpenAiBearerToken = openAiBearerToken;
        SavedSettings.Add((settings, openAiBearerToken));
        return Task.CompletedTask;
    }

    public Task<string?> GetOpenAiBearerTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OpenAiBearerToken);
    }

    public Task<IReadOnlyList<TaskManifest>> ListTasksAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TaskManifest>>(Tasks.ToArray());
    }

    public Task<TaskWorkspaceSnapshot> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        if (CreateTaskHandler is not null)
        {
            var snapshot = CreateTaskHandler(request);
            Snapshots[snapshot.Manifest.Id] = snapshot;
            return Task.FromResult(snapshot);
        }

        var manifest = new TaskManifest
        {
            Id = "task-created",
            FolderName = "Task-task-created",
            Title = request.Title,
            Summary = request.Summary,
            Slug = "task-created",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            InputCategories = TaskFolderConventions.DefaultInputCategories.ToArray(),
        };

        var snapshotResult = new TaskWorkspaceSnapshot
        {
            TaskRootPath = Path.Combine(Settings.WorkspaceRootPath, manifest.FolderName),
            Manifest = manifest,
            TaskMarkdown = request.TaskMarkdown,
        };

        Snapshots[manifest.Id] = snapshotResult;
        return Task.FromResult(snapshotResult);
    }

    public Task<TaskWorkspaceSnapshot?> LoadTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (LoadTaskHandler is not null)
        {
            return Task.FromResult(LoadTaskHandler(taskId));
        }

        Snapshots.TryGetValue(taskId, out var snapshot);
        return Task.FromResult(snapshot);
    }

    public Task SaveTaskAsync(TaskManifest manifest, string taskMarkdown, CancellationToken cancellationToken = default)
    {
        SavedTasks.Add((manifest, taskMarkdown));

        if (Snapshots.TryGetValue(manifest.Id, out var snapshot))
        {
            Snapshots[manifest.Id] = snapshot with
            {
                Manifest = manifest,
                TaskMarkdown = taskMarkdown,
            };
        }

        return Task.CompletedTask;
    }

    public Task ImportArtifactsAsync(string taskId, string destinationRelativeDirectory, IEnumerable<string> sourcePaths, CancellationToken cancellationToken = default)
    {
        var paths = sourcePaths.ToArray();
        ImportedArtifacts.Add((taskId, destinationRelativeDirectory, paths));
        return Task.CompletedTask;
    }

    public Task SaveRunAsync(string taskId, RunManifest run, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ModelRef>> GetModelsAsync(string providerId, CancellationToken cancellationToken = default)
    {
        ModelsByProvider.TryGetValue(providerId, out var models);
        return Task.FromResult<IReadOnlyList<ModelRef>>(models?.ToArray() ?? []);
    }

    public Task<IReadOnlyList<ModelRef>> GetTextModelsAsync(string providerId, CancellationToken cancellationToken = default)
    {
        ModelsByProvider.TryGetValue(providerId, out var models);
        return Task.FromResult<IReadOnlyList<ModelRef>>(
            models?.Where(static model => model.Capabilities.SupportsTextInput).ToArray() ?? []);
    }

    public Task<ProviderRef> GetProviderAsync(string providerId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderRef
        {
            ProviderId = providerId,
            DisplayName = providerId,
            BaseUrl = Settings.OpenAiGatewayBaseUrl,
        });
    }

    public Task<TaskReviewResult> RunTaskReviewAsync(TaskReviewRequest request, string taskId, CancellationToken cancellationToken = default)
    {
        ReviewRequests.Add((request, taskId));

        if (RunTaskReviewHandler is not null)
        {
            return Task.FromResult(RunTaskReviewHandler(request, taskId));
        }

        return Task.FromResult(new TaskReviewResult
        {
            TaskId = taskId,
            RunId = "run-001",
            StepId = "step-001",
            TaskStatus = TaskLifecycleState.UnderReview,
            Summary = "Review completed.",
            OutputText = "Review completed.",
            PromptVersion = "task-review-v1",
        });
    }
}

internal sealed class FakeNavigationService : IAppNavigationService
{
    public int CreateTaskNavigationCount { get; private set; }

    public List<(string TaskId, bool ReplaceCurrentPage)> TaskDetailsNavigations { get; } = [];

    public List<string> RunHistoryNavigations { get; } = [];

    public int GoBackCount { get; private set; }

    public Task GoToCreateTaskAsync()
    {
        CreateTaskNavigationCount++;
        return Task.CompletedTask;
    }

    public Task GoToTaskDetailsAsync(string taskId, bool replaceCurrentPage = false)
    {
        TaskDetailsNavigations.Add((taskId, replaceCurrentPage));
        return Task.CompletedTask;
    }

    public Task GoToRunHistoryAsync(string taskId)
    {
        RunHistoryNavigations.Add(taskId);
        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        GoBackCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeFilePickerService : IFilePickerService
{
    public IReadOnlyList<string> SelectedPaths { get; set; } = [];

    public Task<IReadOnlyList<string>> PickFilesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SelectedPaths);
    }
}

internal sealed class FakeFolderPickerService : IFolderPickerService
{
    public string? SelectedPath { get; set; }

    public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SelectedPath);
    }
}

internal static class TestData
{
    public static ModelRef CreateTextModel(string modelId, string displayName)
    {
        return new ModelRef
        {
            ProviderId = "openai",
            ModelId = modelId,
            DisplayName = displayName,
            Description = $"{displayName} description",
            Capabilities = new ModelCapabilities
            {
                SupportsTextInput = true,
            },
        };
    }

    public static TaskWorkspaceSnapshot CreateSnapshot(
        string taskId,
        string title,
        string summary,
        TaskLifecycleState status,
        IReadOnlyList<string>? inputCategories = null,
        IReadOnlyList<ArtifactManifest>? artifacts = null,
        IReadOnlyList<RunManifest>? runs = null)
    {
        return new TaskWorkspaceSnapshot
        {
            TaskRootPath = Path.Combine("C:\\workspace", $"Task-{taskId}"),
            TaskMarkdown = "# Task",
            Manifest = new TaskManifest
            {
                Id = taskId,
                FolderName = $"Task-{taskId}",
                Title = title,
                Summary = summary,
                Slug = title.ToLowerInvariant().Replace(' ', '-'),
                Status = status,
                CreatedAtUtc = new DateTimeOffset(2026, 3, 26, 8, 0, 0, TimeSpan.Zero),
                UpdatedAtUtc = new DateTimeOffset(2026, 3, 26, 9, 0, 0, TimeSpan.Zero),
                InputCategories = inputCategories ?? TaskFolderConventions.DefaultInputCategories.ToArray(),
                Artifacts = artifacts ?? [],
                Runs = runs ?? [],
            },
            Tree =
            [
                new TaskTreeNode
                {
                    Name = "inputs",
                    RelativePath = "inputs",
                    IsDirectory = true,
                },
            ],
        };
    }
}
