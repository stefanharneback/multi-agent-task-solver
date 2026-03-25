using MultiAgentTaskSolver.Infrastructure.Configuration;

namespace MultiAgentTaskSolver.Infrastructure.Tests;

public sealed class JsonModelCatalogTests : IDisposable
{
    private readonly string _tempRootPath = Path.Combine(Path.GetTempPath(), "mats-model-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetModelsAsyncLoadsModelsFromProviderConfig()
    {
        Directory.CreateDirectory(_tempRootPath);

        var payload = """
        {
          "provider": {
            "providerId": "openai",
            "displayName": "OpenAI via Gateway",
            "baseUrl": "http://localhost:3000"
          },
          "models": [
            {
              "providerId": "openai",
              "modelId": "gpt-5.4-mini",
              "displayName": "GPT-5.4 Mini",
              "description": "Mini model"
            }
          ]
        }
        """;

        await File.WriteAllTextAsync(Path.Combine(_tempRootPath, "openai.models.json"), payload);
        var catalog = new JsonModelCatalog(_tempRootPath);

        var models = await catalog.GetModelsAsync("openai");
        var model = await catalog.GetModelAsync("openai", "gpt-5.4-mini");

        Assert.Single(models);
        Assert.NotNull(model);
        Assert.Equal("GPT-5.4 Mini", model!.DisplayName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootPath))
        {
            Directory.Delete(_tempRootPath, recursive: true);
        }
    }
}
