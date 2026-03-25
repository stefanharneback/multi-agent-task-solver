using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.Services;

public sealed class TaskWorkspaceCoordinator
{
    private const string OpenAiSecretKey = "providers:openai:bearer";

    private readonly ITaskWorkspaceStore _taskWorkspaceStore;
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly ISecretStore _secretStore;
    private readonly IModelCatalog _modelCatalog;
    private readonly ITaskReviewWorkflow _taskReviewWorkflow;
    private readonly Dictionary<string, IProviderAdapter> _providerAdapters;

    public TaskWorkspaceCoordinator(
        ITaskWorkspaceStore taskWorkspaceStore,
        IAppSettingsStore appSettingsStore,
        ISecretStore secretStore,
        IModelCatalog modelCatalog,
        ITaskReviewWorkflow taskReviewWorkflow,
        IEnumerable<IProviderAdapter> providerAdapters)
    {
        _taskWorkspaceStore = taskWorkspaceStore;
        _appSettingsStore = appSettingsStore;
        _secretStore = secretStore;
        _modelCatalog = modelCatalog;
        _taskReviewWorkflow = taskReviewWorkflow;
        _providerAdapters = providerAdapters.ToDictionary(adapter => adapter.ProviderId, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsStore.LoadAsync(cancellationToken);
        Directory.CreateDirectory(settings.WorkspaceRootPath);
        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings, string? openAiBearerToken, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(settings.WorkspaceRootPath);
        await _appSettingsStore.SaveAsync(settings, cancellationToken);
        await _secretStore.SetAsync(OpenAiSecretKey, openAiBearerToken, cancellationToken);
    }

    public Task<string?> GetOpenAiBearerTokenAsync(CancellationToken cancellationToken = default)
    {
        return _secretStore.GetAsync(OpenAiSecretKey, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskManifest>> ListTasksAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return await _taskWorkspaceStore.ListTasksAsync(settings.WorkspaceRootPath, cancellationToken);
    }

    public async Task<TaskWorkspaceSnapshot> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return await _taskWorkspaceStore.CreateTaskAsync(settings.WorkspaceRootPath, request, cancellationToken);
    }

    public async Task<TaskWorkspaceSnapshot?> LoadTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return await _taskWorkspaceStore.LoadTaskAsync(settings.WorkspaceRootPath, taskId, cancellationToken);
    }

    public async Task SaveTaskAsync(TaskManifest manifest, string taskMarkdown, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        await _taskWorkspaceStore.SaveTaskAsync(settings.WorkspaceRootPath, manifest, taskMarkdown, cancellationToken);
    }

    public async Task ImportArtifactsAsync(
        string taskId,
        string destinationRelativeDirectory,
        IEnumerable<string> sourcePaths,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);

        foreach (var sourcePath in sourcePaths)
        {
            await _taskWorkspaceStore.ImportArtifactAsync(
                settings.WorkspaceRootPath,
                taskId,
                new ArtifactImportRequest
                {
                    SourceFilePath = sourcePath,
                    DestinationRelativeDirectory = destinationRelativeDirectory,
                },
                cancellationToken);
        }
    }

    public async Task SaveRunAsync(string taskId, RunManifest run, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        await _taskWorkspaceStore.SaveRunAsync(settings.WorkspaceRootPath, taskId, run, cancellationToken);
    }

    public Task<IReadOnlyList<ModelRef>> GetModelsAsync(string providerId, CancellationToken cancellationToken = default)
    {
        return _modelCatalog.GetModelsAsync(providerId, cancellationToken);
    }

    public async Task<IReadOnlyList<ModelRef>> GetTextModelsAsync(string providerId, CancellationToken cancellationToken = default)
    {
        return (await _modelCatalog.GetModelsAsync(providerId, cancellationToken))
            .Where(static model => model.Capabilities.SupportsTextInput)
            .OrderBy(model => model.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<ProviderRef> GetProviderAsync(string providerId, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);

        return providerId.ToLowerInvariant() switch
        {
            "openai" => new ProviderRef
            {
                ProviderId = "openai",
                DisplayName = "OpenAI via Gateway",
                BaseUrl = settings.OpenAiGatewayBaseUrl,
            },
            _ => throw new InvalidOperationException($"Unknown provider '{providerId}'."),
        };
    }

    public IProviderAdapter GetProviderAdapter(string providerId)
    {
        return _providerAdapters.TryGetValue(providerId, out var adapter)
            ? adapter
            : throw new InvalidOperationException($"No provider adapter is registered for '{providerId}'.");
    }

    public async Task<TaskReviewResult> RunTaskReviewAsync(TaskReviewRequest request, string taskId, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var snapshot = await _taskWorkspaceStore.LoadTaskAsync(settings.WorkspaceRootPath, taskId, cancellationToken)
            ?? throw new InvalidOperationException($"Task '{taskId}' was not found.");

        var provider = await GetProviderAsync(request.ProviderId, cancellationToken);
        var model = await _modelCatalog.GetModelAsync(request.ProviderId, request.ModelId, cancellationToken)
            ?? throw new InvalidOperationException($"Model '{request.ModelId}' is not known for provider '{request.ProviderId}'.");

        var bearerToken = request.ProviderId.ToLowerInvariant() switch
        {
            "openai" => await GetOpenAiBearerTokenAsync(cancellationToken),
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            throw new InvalidOperationException($"No client bearer token is configured for provider '{request.ProviderId}'.");
        }

        return await _taskReviewWorkflow.RunAsync(
            settings.WorkspaceRootPath,
            snapshot,
            provider,
            model,
            bearerToken,
            cancellationToken);
    }
}
