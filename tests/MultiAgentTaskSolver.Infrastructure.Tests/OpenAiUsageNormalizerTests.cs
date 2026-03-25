using MultiAgentTaskSolver.Infrastructure.Gateway;

namespace MultiAgentTaskSolver.Infrastructure.Tests;

public sealed class OpenAiUsageNormalizerTests
{
    [Fact]
    public void TryNormalizeTextResponseParsesUsagePayload()
    {
        var normalizer = new OpenAiUsageNormalizer();
        var payload = """
        {
          "id": "resp_1",
          "usage": {
            "input_tokens": 12,
            "output_tokens": 4,
            "total_tokens": 16,
            "input_tokens_details": {
              "cached_tokens": 2
            },
            "output_tokens_details": {
              "reasoning_tokens": 1
            }
          }
        }
        """;

        var usage = normalizer.TryNormalizeTextResponse("openai", "gpt-5.4-mini", payload, TimeSpan.FromMilliseconds(123), "req-1");

        Assert.NotNull(usage);
        Assert.Equal(12, usage!.InputTokens);
        Assert.Equal(4, usage.OutputTokens);
        Assert.Equal(16, usage.TotalTokens);
        Assert.Equal(2, usage.CachedInputTokens);
        Assert.Equal(1, usage.ReasoningTokens);
        Assert.Equal("req-1", usage.GatewayRequestId);
    }

    [Fact]
    public void NormalizeUsageHistoryParsesUsageItems()
    {
        var normalizer = new OpenAiUsageNormalizer();
        var payload = """
        {
          "clientId": "client-1",
          "items": [
            {
              "id": "request-1",
              "created_at": "2026-03-23T20:00:00Z",
              "model": "gpt-5.4-mini",
              "http_status": 200,
              "duration_ms": 99,
              "input_tokens": 10,
              "output_tokens": 5,
              "total_tokens": 15,
              "total_cost_usd": 0.0012
            }
          ]
        }
        """;

        var items = normalizer.NormalizeUsageHistory("openai", payload);

        Assert.Single(items);
        Assert.Equal("request-1", items[0].SourceRequestId);
        Assert.Equal(15, items[0].TotalTokens);
        Assert.Equal(0.0012m, items[0].TotalCostUsd);
    }
}
