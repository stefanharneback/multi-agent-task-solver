using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.Core.Abstractions;

public interface IProviderAdapter
{
    string ProviderId { get; }

    Task<IReadOnlyList<string>> GetModelsAsync(
        ProviderRef provider,
        string bearerToken,
        CancellationToken cancellationToken = default);

    Task<ProviderTextResponse> SendTextAsync(
        ProviderRef provider,
        LlmRequest request,
        string bearerToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsageRecord>> GetUsageAsync(
        ProviderRef provider,
        string bearerToken,
        UsageQuery query,
        CancellationToken cancellationToken = default);

    Task<TranscriptionResponse> TranscribeAsync(
        ProviderRef provider,
        TranscriptionRequest request,
        string bearerToken,
        CancellationToken cancellationToken = default);
}

public interface IModelCatalog
{
    Task<IReadOnlyList<ModelRef>> GetModelsAsync(string providerId, CancellationToken cancellationToken = default);

    Task<ModelRef?> GetModelAsync(string providerId, string modelId, CancellationToken cancellationToken = default);
}

public interface IUsageNormalizer
{
    UsageRecord? TryNormalizeTextResponse(
        string providerId,
        string modelId,
        string rawPayload,
        TimeSpan duration,
        string? gatewayRequestId);

    IReadOnlyList<UsageRecord> NormalizeUsageHistory(string providerId, string rawPayload);
}

public interface ITaskWorkspaceStore
{
    Task<TaskWorkspaceSnapshot> CreateTaskAsync(
        string workspaceRootPath,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskManifest>> ListTasksAsync(
        string workspaceRootPath,
        CancellationToken cancellationToken = default);

    Task<TaskWorkspaceSnapshot?> LoadTaskAsync(
        string workspaceRootPath,
        string taskId,
        CancellationToken cancellationToken = default);

    Task SaveTaskAsync(
        string workspaceRootPath,
        TaskManifest manifest,
        string taskMarkdown,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArtifactManifest>> ImportArtifactAsync(
        string workspaceRootPath,
        string taskId,
        ArtifactImportRequest request,
        CancellationToken cancellationToken = default);

    Task SaveRunAsync(
        string workspaceRootPath,
        string taskId,
        RunManifest run,
        CancellationToken cancellationToken = default);

    Task SaveStepArtifactsAsync(
        string workspaceRootPath,
        string taskId,
        string runId,
        string stepId,
        StepArtifactsPayload payload,
        CancellationToken cancellationToken = default);

    Task SaveOutputArtifactAsync(
        string workspaceRootPath,
        string taskId,
        OutputArtifactPayload payload,
        CancellationToken cancellationToken = default);
}

public interface IAppSettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface ISecretStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string? value, CancellationToken cancellationToken = default);
}

public interface IArtifactReferenceResolver
{
    Task<TaskReferenceResolution> ResolveAsync(TaskWorkspaceSnapshot snapshot, CancellationToken cancellationToken = default);
}

public interface IReviewPromptFactory
{
    ReviewPromptPackage Create(TaskWorkspaceSnapshot snapshot, TaskReferenceResolution resolution);
}

public interface IWorkerPromptFactory
{
    WorkerPromptPackage Create(TaskWorkspaceSnapshot snapshot, TaskReferenceResolution resolution);
}

public interface ITaskReviewWorkflow
{
    Task<TaskReviewResult> RunAsync(
        string workspaceRootPath,
        TaskWorkspaceSnapshot snapshot,
        ProviderRef provider,
        ModelRef model,
        string bearerToken,
        CancellationToken cancellationToken = default);
}

public interface ITaskWorkerWorkflow
{
    Task<TaskWorkerResult> RunAsync(
        string workspaceRootPath,
        TaskWorkspaceSnapshot snapshot,
        ProviderRef provider,
        ModelRef model,
        string bearerToken,
        CancellationToken cancellationToken = default);
}

public interface IUserDecisionWorkflow
{
    Task<ReviewDecisionResult> RunAsync(
        string workspaceRootPath,
        TaskWorkspaceSnapshot snapshot,
        ReviewDecisionRequest request,
        CancellationToken cancellationToken = default);
}
