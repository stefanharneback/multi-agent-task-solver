using System.Text.Json;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;
using MultiAgentTaskSolver.Infrastructure.Serialization;

namespace MultiAgentTaskSolver.Infrastructure.Configuration;

public sealed class JsonModelCatalog : IModelCatalog
{
    private readonly string _configDirectoryPath;

    public JsonModelCatalog(string configDirectoryPath)
    {
        _configDirectoryPath = configDirectoryPath;
    }

    public async Task<IReadOnlyList<ModelRef>> GetModelsAsync(string providerId, CancellationToken cancellationToken = default)
    {
        var documents = await LoadDocumentsAsync(cancellationToken);
        return documents
            .Where(document => string.Equals(document.Provider.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(document => document.Models)
            .ToArray();
    }

    public async Task<ModelRef?> GetModelAsync(string providerId, string modelId, CancellationToken cancellationToken = default)
    {
        var models = await GetModelsAsync(providerId, cancellationToken);
        return models.FirstOrDefault(model => string.Equals(model.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<ProviderCatalogDocument>> LoadDocumentsAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_configDirectoryPath))
        {
            return [];
        }

        var documents = new List<ProviderCatalogDocument>();
        foreach (var filePath in Directory.EnumerateFiles(_configDirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            await using var stream = File.OpenRead(filePath);
            var document = await JsonSerializer.DeserializeAsync<ProviderCatalogDocument>(
                stream,
                JsonDefaults.SerializerOptions,
                cancellationToken);

            if (document is not null)
            {
                documents.Add(document);
            }
        }

        return documents;
    }

    private sealed record ProviderCatalogDocument
    {
        public ProviderRef Provider { get; init; } = new();

        public IReadOnlyList<ModelRef> Models { get; init; } = [];
    }
}
