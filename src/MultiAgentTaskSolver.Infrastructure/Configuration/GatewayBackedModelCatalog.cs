using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;
using MultiAgentTaskSolver.Infrastructure.Gateway;

namespace MultiAgentTaskSolver.Infrastructure.Configuration;

public sealed class GatewayBackedModelCatalog : IModelCatalog
{
    private const string OpenAiProviderId = "openai";
    private const string OpenAiSecretKey = "providers:openai:bearer";

    private readonly JsonModelCatalog _fallbackCatalog;
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly ISecretStore _secretStore;
    private readonly OpenAiGatewayAdapter _openAiGatewayAdapter;

    public GatewayBackedModelCatalog(
        string configDirectoryPath,
        IAppSettingsStore appSettingsStore,
        ISecretStore secretStore,
        OpenAiGatewayAdapter openAiGatewayAdapter)
    {
        _fallbackCatalog = new JsonModelCatalog(configDirectoryPath);
        _appSettingsStore = appSettingsStore;
        _secretStore = secretStore;
        _openAiGatewayAdapter = openAiGatewayAdapter;
    }

    public async Task<IReadOnlyList<ModelRef>> GetModelsAsync(string providerId, CancellationToken cancellationToken = default)
    {
        var fallbackModels = await _fallbackCatalog.GetModelsAsync(providerId, cancellationToken);
        if (!string.Equals(providerId, OpenAiProviderId, StringComparison.OrdinalIgnoreCase))
        {
            return fallbackModels;
        }

        var liveModels = await TryGetLiveOpenAiModelsAsync(fallbackModels, cancellationToken);
        return liveModels ?? fallbackModels;
    }

    public async Task<ModelRef?> GetModelAsync(string providerId, string modelId, CancellationToken cancellationToken = default)
    {
        var models = await GetModelsAsync(providerId, cancellationToken);
        return models.FirstOrDefault(model => string.Equals(model.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<ModelRef>?> TryGetLiveOpenAiModelsAsync(
        IReadOnlyList<ModelRef> fallbackModels,
        CancellationToken cancellationToken)
    {
        var settings = await _appSettingsStore.LoadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.OpenAiGatewayBaseUrl))
        {
            return null;
        }

        var bearerToken = await _secretStore.GetAsync(OpenAiSecretKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return null;
        }

        try
        {
            var provider = new ProviderRef
            {
                ProviderId = OpenAiProviderId,
                DisplayName = "OpenAI via Gateway",
                BaseUrl = settings.OpenAiGatewayBaseUrl,
            };

            var liveModelIds = await _openAiGatewayAdapter.GetModelsAsync(provider, bearerToken, cancellationToken);
            return MapLiveOpenAiModels(liveModelIds, fallbackModels);
        }
        catch (GatewayApiException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static ModelRef[] MapLiveOpenAiModels(
        IReadOnlyList<string> liveModelIds,
        IReadOnlyList<ModelRef> fallbackModels)
    {
        var fallbackLookup = fallbackModels.ToDictionary(
            model => model.ModelId,
            StringComparer.OrdinalIgnoreCase);

        return liveModelIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(modelId =>
                fallbackLookup.TryGetValue(modelId, out var fallbackModel)
                    ? fallbackModel
                    : CreateInferredOpenAiModel(modelId))
            .OrderBy(model => model.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ModelRef CreateInferredOpenAiModel(string modelId)
    {
        var supportsAudioInput = IsAudioModel(modelId);
        var supportsTextInput = !supportsAudioInput;

        return new ModelRef
        {
            ProviderId = OpenAiProviderId,
            ModelId = modelId,
            DisplayName = BuildDisplayName(modelId),
            Description = supportsAudioInput
                ? $"Audio transcription model discovered live from the OpenAI gateway ({modelId})."
                : $"Text model discovered live from the OpenAI gateway ({modelId}).",
            Capabilities = new ModelCapabilities
            {
                SupportsTextInput = supportsTextInput,
                SupportsAudioInput = supportsAudioInput,
            },
        };
    }

    private static bool IsAudioModel(string modelId)
    {
        return string.Equals(modelId, "whisper-1", StringComparison.OrdinalIgnoreCase)
            || modelId.Contains("transcribe", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDisplayName(string modelId)
    {
        var tokens = modelId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return modelId;
        }

        return string.Join(
            " ",
            tokens.Select((token, index) =>
            {
                if (index == 0 && string.Equals(token, "gpt", StringComparison.OrdinalIgnoreCase))
                {
                    return "GPT";
                }

                if (string.Equals(token, "whisper", StringComparison.OrdinalIgnoreCase))
                {
                    return "Whisper";
                }

                return char.ToUpperInvariant(token[0]) + token[1..];
            }));
    }
}
