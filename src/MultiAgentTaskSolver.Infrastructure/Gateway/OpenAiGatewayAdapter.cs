using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;
using MultiAgentTaskSolver.Infrastructure.Serialization;

namespace MultiAgentTaskSolver.Infrastructure.Gateway;

public sealed class OpenAiGatewayAdapter : IProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly IUsageNormalizer _usageNormalizer;

    public OpenAiGatewayAdapter(HttpClient httpClient, IUsageNormalizer usageNormalizer)
    {
        _httpClient = httpClient;
        _usageNormalizer = usageNormalizer;
    }

    public string ProviderId => "openai";

    public async Task<ProviderTextResponse> SendTextAsync(
        ProviderRef provider,
        LlmRequest request,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        if (request.Stream)
        {
            throw new NotSupportedException("Streaming text responses are not implemented in this milestone.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.ModelId,
            ["input"] = request.InputText,
            ["stream"] = request.Stream,
            ["include"] = request.Include,
        };

        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            payload["instructions"] = request.Instructions;
        }

        if (request.Metadata is { Count: > 0 })
        {
            payload["metadata"] = request.Metadata;
        }

        using var message = CreateAuthorizedRequest(HttpMethod.Post, provider, "/v1/llm", bearerToken);
        message.Content = JsonContent.Create(payload, options: JsonDefaults.SerializerOptions);

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        stopwatch.Stop();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GatewayApiException(response.StatusCode, $"Gateway request failed with status {(int)response.StatusCode}.", body);
        }

        var gatewayRequestId = response.Headers.TryGetValues("x-request-id", out var values)
            ? values.FirstOrDefault()
            : null;

        return new ProviderTextResponse
        {
            ProviderId = provider.ProviderId,
            ModelId = request.ModelId,
            OutputText = ExtractOutputText(body),
            RawResponseBody = body,
            Usage = _usageNormalizer.TryNormalizeTextResponse(provider.ProviderId, request.ModelId, body, stopwatch.Elapsed, gatewayRequestId),
            GatewayRequestId = gatewayRequestId,
            HttpStatusCode = (int)response.StatusCode,
            IsStreamingResponse = request.Stream,
        };
    }

    public async Task<IReadOnlyList<UsageRecord>> GetUsageAsync(
        ProviderRef provider,
        string bearerToken,
        UsageQuery query,
        CancellationToken cancellationToken = default)
    {
        var path = $"/v1/usage?limit={query.Limit}&offset={query.Offset}";

        using var message = CreateAuthorizedRequest(HttpMethod.Get, provider, path, bearerToken);
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new GatewayApiException(response.StatusCode, $"Gateway request failed with status {(int)response.StatusCode}.", body);
        }

        return _usageNormalizer.NormalizeUsageHistory(provider.ProviderId, body);
    }

    public async Task<TranscriptionResponse> TranscribeAsync(
        ProviderRef provider,
        TranscriptionRequest request,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModelId);

        using var message = CreateAuthorizedRequest(HttpMethod.Post, provider, "/v1/whisper", bearerToken);
        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(request.ModelId, Encoding.UTF8), "model");

        Stream? stream = null;
        if (!string.IsNullOrWhiteSpace(request.AudioUrl))
        {
            formData.Add(new StringContent(request.AudioUrl, Encoding.UTF8), "audio_url");
        }
        else if (!string.IsNullOrWhiteSpace(request.FilePath))
        {
            if (!File.Exists(request.FilePath))
            {
                throw new FileNotFoundException("Transcription source file was not found.", request.FilePath);
            }

            stream = File.OpenRead(request.FilePath);
            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(GuessMediaType(request.FilePath));
            formData.Add(streamContent, "file", Path.GetFileName(request.FilePath));
        }
        else
        {
            throw new InvalidOperationException("Provide either a file path or an audio URL.");
        }

        message.Content = formData;

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        stopwatch.Stop();

        if (stream is not null)
        {
            await stream.DisposeAsync();
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GatewayApiException(response.StatusCode, $"Gateway request failed with status {(int)response.StatusCode}.", body);
        }

        var gatewayRequestId = response.Headers.TryGetValues("x-request-id", out var values)
            ? values.FirstOrDefault()
            : null;

        return new TranscriptionResponse
        {
            ProviderId = provider.ProviderId,
            ModelId = request.ModelId,
            TranscriptText = ExtractTranscriptText(body),
            RawResponseBody = body,
            Usage = _usageNormalizer.TryNormalizeTextResponse(provider.ProviderId, request.ModelId, body, stopwatch.Elapsed, gatewayRequestId),
            GatewayRequestId = gatewayRequestId,
        };
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, ProviderRef provider, string relativePath, string bearerToken)
    {
        var baseUri = new Uri(EnsureTrailingSlash(provider.BaseUrl), UriKind.Absolute);
        var requestUri = new Uri(baseUri, relativePath.TrimStart('/'));
        var message = new HttpRequestMessage(method, requestUri);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return message;
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return baseUrl[^1] == '/' ? baseUrl : $"{baseUrl}/";
    }

    private static string? ExtractOutputText(string rawPayload)
    {
        try
        {
            using var json = JsonDocument.Parse(rawPayload);
            var root = json.RootElement;

            if (root.TryGetProperty("output_text", out var outputTextElement)
                && outputTextElement.ValueKind == JsonValueKind.String)
            {
                return outputTextElement.GetString();
            }

            if (root.TryGetProperty("output", out var outputElement) && outputElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var outputItem in outputElement.EnumerateArray())
                {
                    if (!outputItem.TryGetProperty("content", out var contentElement)
                        || contentElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var contentItem in contentElement.EnumerateArray())
                    {
                        if (contentItem.TryGetProperty("text", out var textElement)
                            && textElement.ValueKind == JsonValueKind.String)
                        {
                            return textElement.GetString();
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static string ExtractTranscriptText(string rawPayload)
    {
        try
        {
            using var json = JsonDocument.Parse(rawPayload);
            return json.RootElement.TryGetProperty("text", out var textElement)
                ? textElement.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string GuessMediaType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            _ => "application/octet-stream",
        };
    }
}
