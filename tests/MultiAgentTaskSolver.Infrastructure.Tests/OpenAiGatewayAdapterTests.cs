using System.Net;
using System.Text;
using MultiAgentTaskSolver.Core.Models;
using MultiAgentTaskSolver.Infrastructure.Gateway;

namespace MultiAgentTaskSolver.Infrastructure.Tests;

public sealed class OpenAiGatewayAdapterTests
{
    [Fact]
    public async Task SendTextAsyncSendsAuthorizationHeaderAndParsesResponse()
    {
        string? capturedAuthorizationScheme = null;
        string? capturedAuthorizationParameter = null;
        string? capturedPath = null;
        var responseJson = """
        {
          "id": "resp_1",
          "output_text": "hello",
          "usage": {
            "input_tokens": 10,
            "output_tokens": 5,
            "total_tokens": 15
          }
        }
        """;

        var adapter = CreateAdapter(request =>
        {
            capturedAuthorizationScheme ??= request.Headers.Authorization?.Scheme;
            capturedAuthorizationParameter ??= request.Headers.Authorization?.Parameter;
            capturedPath ??= request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
                Headers =
                {
                    { "x-request-id", "req-1" },
                },
            };
        });

        var response = await adapter.SendTextAsync(
            CreateProvider(),
            new LlmRequest
            {
                ModelId = "gpt-5.4-mini",
                InputText = "hello",
            },
            "client-secret");

        Assert.Equal("Bearer", capturedAuthorizationScheme);
        Assert.Equal("client-secret", capturedAuthorizationParameter);
        Assert.Equal("/v1/llm", capturedPath);
        Assert.Equal("hello", response.OutputText);
        Assert.Equal(15, response.Usage?.TotalTokens);
    }

    [Fact]
    public async Task SendTextAsyncEnrichesUsageWithCostFromGatewayHistory()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": "resp_1",
                      "output_text": "hello",
                      "usage": {
                        "input_tokens": 10,
                        "output_tokens": 5,
                        "total_tokens": 15
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
                Headers =
                {
                    { "x-request-id", "req-1" },
                },
            },
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "clientId": "client-1",
                      "items": [
                        {
                          "id": "req-1",
                          "created_at": "2026-03-23T20:00:00Z",
                          "model": "gpt-5.4-mini",
                          "http_status": 200,
                          "duration_ms": 125,
                          "input_tokens": 10,
                          "output_tokens": 5,
                          "total_tokens": 15,
                          "total_cost_usd": 0.0012
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            },
        ]);

        var adapter = CreateAdapter(_ => responses.Dequeue());

        var response = await adapter.SendTextAsync(
            CreateProvider(),
            new LlmRequest
            {
                ModelId = "gpt-5.4-mini",
                InputText = "hello",
            },
            "client-secret");

        Assert.Equal(15, response.Usage?.TotalTokens);
        Assert.Equal(0.0012m, response.Usage?.TotalCostUsd);
        Assert.Equal("req-1", response.Usage?.GatewayRequestId);
        Assert.Equal("req-1", response.Usage?.SourceRequestId);
    }

    [Fact]
    public async Task SendTextAsyncThrowsGatewayApiExceptionOnValidationFailure()
    {
        var adapter = CreateAdapter(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":{\"code\":\"invalid_request\"}}", Encoding.UTF8, "application/json"),
        });

        var exception = await Assert.ThrowsAsync<GatewayApiException>(async () =>
        {
            await adapter.SendTextAsync(
                CreateProvider(),
                new LlmRequest
                {
                    ModelId = "gpt-5.4-mini",
                    InputText = "hello",
                },
                "client-secret");
        });

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("invalid_request", exception.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendTextAsyncRejectsStreamingRequests()
    {
        var requestWasSent = false;
        var adapter = CreateAdapter(_ =>
        {
            requestWasSent = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await adapter.SendTextAsync(
                CreateProvider(),
                new LlmRequest
                {
                    ModelId = "gpt-5.4-mini",
                    InputText = "hello",
                    Stream = true,
                },
                "client-secret");
        });

        Assert.False(requestWasSent);
        Assert.Contains("Streaming text responses", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetUsageAsyncThrowsGatewayApiExceptionOnAuthFailure()
    {
        var adapter = CreateAdapter(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":{\"code\":\"invalid_auth\"}}", Encoding.UTF8, "application/json"),
        });

        var exception = await Assert.ThrowsAsync<GatewayApiException>(async () =>
        {
            await adapter.GetUsageAsync(CreateProvider(), "bad-secret", new UsageQuery());
        });

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Contains("invalid_auth", exception.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetUsageAsyncParsesUsageHistory()
    {
        HttpRequestMessage? capturedRequest = null;
        var payload = """
        {
          "clientId": "client-1",
          "items": [
            {
              "id": "request-1",
              "created_at": "2026-03-23T20:00:00Z",
              "model": "gpt-5.4-mini",
              "http_status": 200,
              "duration_ms": 125,
              "input_tokens": 20,
              "output_tokens": 8,
              "total_tokens": 28,
              "total_cost_usd": 0.0021
            }
          ]
        }
        """;

        var adapter = CreateAdapter(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        });

        var items = await adapter.GetUsageAsync(CreateProvider(), "client-secret", new UsageQuery { Limit = 10, Offset = 5 });

        Assert.NotNull(capturedRequest);
        Assert.Equal("/v1/usage", capturedRequest!.RequestUri?.AbsolutePath);
        Assert.Equal("?limit=10&offset=5", capturedRequest.RequestUri?.Query);
        Assert.Single(items);
        Assert.Equal(28, items[0].TotalTokens);
        Assert.Equal(0.0021m, items[0].TotalCostUsd);
    }

    [Fact]
    public async Task GetModelsAsyncParsesGatewayModelIds()
    {
        HttpRequestMessage? capturedRequest = null;
        var payload = """
        {
          "models": ["gpt-5.4-mini", "whisper-1"],
          "unrestricted": false
        }
        """;

        var adapter = CreateAdapter(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        });

        var models = await adapter.GetModelsAsync(CreateProvider(), "client-secret");

        Assert.NotNull(capturedRequest);
        Assert.Equal("/v1/models", capturedRequest!.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("client-secret", capturedRequest.Headers.Authorization?.Parameter);
        Assert.Equal(["gpt-5.4-mini", "whisper-1"], models);
    }

    [Fact]
    public async Task SendTextAsyncJoinsAllNestedOutputTextSegments()
    {
        var responseJson = """
        {
          "output": [
            {
              "content": [
                { "type": "output_text", "text": "Line 1" },
                { "type": "output_text", "text": "Line 2" }
              ]
            },
            {
              "content": [
                { "type": "output_text", "text": "Line 3" }
              ]
            }
          ]
        }
        """;

        var adapter = CreateAdapter(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });

        var response = await adapter.SendTextAsync(
            CreateProvider(),
            new LlmRequest
            {
                ModelId = "gpt-5.4-mini",
                InputText = "hello",
            },
            "client-secret");

        Assert.Equal("Line 1" + Environment.NewLine + "Line 2" + Environment.NewLine + "Line 3", response.OutputText);
    }

    [Fact]
    public async Task SendTextAsyncThrowsTimeoutExceptionWhenGatewayTimesOut()
    {
        var adapter = CreateAdapter(_ => throw new TaskCanceledException("request timed out"));

        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await adapter.SendTextAsync(
                CreateProvider(),
                new LlmRequest
                {
                    ModelId = "gpt-5.4-pro",
                    InputText = "hello",
                },
                "client-secret");
        });

        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gpt-5.4-pro", exception.Message, StringComparison.Ordinal);
    }

    private static OpenAiGatewayAdapter CreateAdapter(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        var httpClient = new HttpClient(handler);
        return new OpenAiGatewayAdapter(httpClient, new OpenAiUsageNormalizer());
    }

    private static ProviderRef CreateProvider()
    {
        return new ProviderRef
        {
            ProviderId = "openai",
            DisplayName = "OpenAI via Gateway",
            BaseUrl = "http://localhost:3000",
        };
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
}
