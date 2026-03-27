using System.Net;
using System.Text;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;
using MultiAgentTaskSolver.Infrastructure.Configuration;
using MultiAgentTaskSolver.Infrastructure.Gateway;

namespace MultiAgentTaskSolver.Infrastructure.Tests;

public sealed class GatewayBackedModelCatalogTests : IDisposable
{
    private readonly string _tempRootPath = Path.Combine(Path.GetTempPath(), "mats-gateway-model-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetModelsAsyncUsesLiveGatewayCatalogForOpenAiWhenAvailable()
    {
        Directory.CreateDirectory(_tempRootPath);
        await File.WriteAllTextAsync(
            Path.Combine(_tempRootPath, "openai.models.json"),
            """
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
                  "description": "Mini model",
                  "capabilities": {
                    "supportsTextInput": true
                  }
                },
                {
                  "providerId": "openai",
                  "modelId": "gpt-5.4",
                  "displayName": "GPT-5.4",
                  "description": "Should be excluded when not live.",
                  "capabilities": {
                    "supportsTextInput": true
                  }
                }
              ]
            }
            """);

        var adapter = CreateAdapter(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "models": ["gpt-5.4-mini", "whisper-1"],
                  "unrestricted": false
                }
                """,
                Encoding.UTF8,
                "application/json"),
        });

        var catalog = new GatewayBackedModelCatalog(
            _tempRootPath,
            new StubAppSettingsStore(),
            new StubSecretStore("client-secret"),
            adapter);

        var models = await catalog.GetModelsAsync("openai");

        Assert.Equal(2, models.Count);
        Assert.Equal(["gpt-5.4-mini", "whisper-1"], models.Select(static model => model.ModelId).ToArray());

        var gptModel = Assert.Single(models, static model => model.ModelId == "gpt-5.4-mini");
        Assert.Equal("GPT-5.4 Mini", gptModel.DisplayName);

        var whisperModel = Assert.Single(models, static model => model.ModelId == "whisper-1");
        Assert.Equal("Whisper 1", whisperModel.DisplayName);
        Assert.False(whisperModel.Capabilities.SupportsTextInput);
        Assert.True(whisperModel.Capabilities.SupportsAudioInput);
    }

    [Fact]
    public async Task GetModelsAsyncFallsBackToLocalCatalogWhenGatewayLookupFails()
    {
        Directory.CreateDirectory(_tempRootPath);
        await File.WriteAllTextAsync(
            Path.Combine(_tempRootPath, "openai.models.json"),
            """
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
                  "description": "Mini model",
                  "capabilities": {
                    "supportsTextInput": true
                  }
                }
              ]
            }
            """);

        var adapter = CreateAdapter(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                """
                {
                  "error": {
                    "code": "invalid_auth"
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"),
        });

        var catalog = new GatewayBackedModelCatalog(
            _tempRootPath,
            new StubAppSettingsStore(),
            new StubSecretStore("bad-token"),
            adapter);

        var models = await catalog.GetModelsAsync("openai");

        var model = Assert.Single(models);
        Assert.Equal("gpt-5.4-mini", model.ModelId);
        Assert.Equal("GPT-5.4 Mini", model.DisplayName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootPath))
        {
            Directory.Delete(_tempRootPath, recursive: true);
        }
    }

    private static OpenAiGatewayAdapter CreateAdapter(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        var httpClient = new HttpClient(handler);
        return new OpenAiGatewayAdapter(httpClient, new OpenAiUsageNormalizer());
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class StubAppSettingsStore : IAppSettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AppSettings
            {
                WorkspaceRootPath = "C:\\workspace",
                OpenAiGatewayBaseUrl = "http://localhost:3000",
            });
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubSecretStore : ISecretStore
    {
        private readonly string? _secret;

        public StubSecretStore(string? secret)
        {
            _secret = secret;
        }

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_secret);
        }

        public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
