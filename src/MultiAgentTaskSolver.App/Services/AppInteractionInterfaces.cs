using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.Services;

public interface ITaskWorkspaceCoordinator
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(AppSettings settings, string? openAiBearerToken, CancellationToken cancellationToken = default);

    Task<string?> GetOpenAiBearerTokenAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskManifest>> ListTasksAsync(CancellationToken cancellationToken = default);

    Task<TaskWorkspaceSnapshot> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);

    Task<TaskWorkspaceSnapshot?> LoadTaskAsync(string taskId, CancellationToken cancellationToken = default);

    Task SaveTaskAsync(TaskManifest manifest, string taskMarkdown, CancellationToken cancellationToken = default);

    Task ImportArtifactsAsync(
        string taskId,
        string destinationRelativeDirectory,
        IEnumerable<string> sourcePaths,
        CancellationToken cancellationToken = default);

    Task SaveRunAsync(string taskId, RunManifest run, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelRef>> GetModelsAsync(string providerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelRef>> GetTextModelsAsync(string providerId, CancellationToken cancellationToken = default);

    Task<ProviderRef> GetProviderAsync(string providerId, CancellationToken cancellationToken = default);

    Task<TaskReviewResult> RunTaskReviewAsync(TaskReviewRequest request, string taskId, CancellationToken cancellationToken = default);

    Task<ReviewDecisionResult> ApplyReviewDecisionAsync(ReviewDecisionRequest request, string taskId, CancellationToken cancellationToken = default);
}

public interface IAppNavigationService
{
    Task GoToCreateTaskAsync();

    Task GoToTaskDetailsAsync(string taskId, bool replaceCurrentPage = false);

    Task GoToRunHistoryAsync(string taskId);

    Task GoBackAsync();
}

public interface IFilePickerService
{
    Task<IReadOnlyList<string>> PickFilesAsync(CancellationToken cancellationToken = default);
}

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}
