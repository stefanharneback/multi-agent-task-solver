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

    public async Task<IReadOnlyList<string>> GetModelsAsync(
        ProviderRef provider,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        using var message = CreateAuthorizedRequest(HttpMethod.Get, provider, "/v1/models", bearerToken);
        using var response = await SendAsync(message, "loading available models", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new GatewayApiException(response.StatusCode, $"Gateway request failed with status {(int)response.StatusCode}.", body);
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            if (!json.RootElement.TryGetProperty("models", out var modelsElement)
                || modelsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Gateway models payload did not contain a models array.");
            }

            return modelsElement
                .EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.String)
                .Select(static item => item.GetString())
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()!;
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException("Gateway models payload could not be parsed.", error);
        }
    }

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
        };

        if (request.Include is { Count: > 0 })
        {
            payload["include"] = request.Include;
        }

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
        using var response = await SendAsync(message, $"sending a text request to model '{request.ModelId}'", cancellationToken);
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
        using var response = await SendAsync(message, "loading usage history", cancellationToken);
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

            var stream = File.OpenRead(request.FilePath);
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
        using var response = await SendAsync(message, $"transcribing audio with model '{request.ModelId}'", cancellationToken);
        stopwatch.Stop();

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

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage message,
        string operationDescription,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(message, cancellationToken);
        }
        catch (Exception ex) when (IsGatewayTimeout(ex, cancellationToken))
        {
            throw CreateTimeoutException(operationDescription, ex);
        }
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return baseUrl[^1] == '/' ? baseUrl : $"{baseUrl}/";
    }

    private static TimeoutException CreateTimeoutException(string operationDescription, Exception innerException)
    {
        return new TimeoutException(
            $"Gateway request timed out while {operationDescription}. The configured timeout may be too short for this model.",
            innerException);
    }

    private static bool IsGatewayTimeout(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is TaskCanceledException
            || string.Equals(exception.GetType().FullName, "Polly.Timeout.TimeoutRejectedException", StringComparison.Ordinal);
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
                var chunks = new List<string>();
                foreach (var outputItem in outputElement.EnumerateArray())
                {
                    if (!outputItem.TryGetProperty("content", out var contentElement)
                        || contentElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var contentItem in contentElement.EnumerateArray())
                    {
                        if (contentItem.TryGetProperty("type", out var typeElement)
                            && typeElement.ValueKind == JsonValueKind.String
                            && typeElement.GetString() == "output_text"
                            && contentItem.TryGetProperty("text", out var textElement)
                            && textElement.ValueKind == JsonValueKind.String)
                        {
                            var text = textElement.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                chunks.Add(text.Trim());
                            }
                        }
                    }
                }

                if (chunks.Count > 0)
                {
                    return string.Join(Environment.NewLine, chunks);
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
