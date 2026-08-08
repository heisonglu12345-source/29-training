using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderHub.Core.Ai;

namespace OrderHub.Infrastructure.Gemini;

/// <summary>
/// 使用 HttpClient 呼叫 Gemini Interactions API。只重試網路錯誤、429 與 5xx；
/// 重試耗盡後轉成 AiServiceUnavailableException，讓 Web 層回 503。
/// </summary>
public class GeminiInteractionsClient(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    ILogger<GeminiInteractionsClient> logger) : IGeminiJsonClient
{
    private readonly GeminiOptions _options = options.Value;

    public async Task<string> GenerateJsonAsync(
        string input,
        string responseSchemaJson,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _options.ApiKey ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiServiceUnavailableException(
                "Gemini API key 未設定：請設定 user-secrets 的 Gemini:ApiKey 或環境變數 GEMINI_API_KEY");
        }

        using var schema = JsonDocument.Parse(responseSchemaJson);
        var body = JsonSerializer.Serialize(new
        {
            model = _options.Model,
            input,
            response_format = new
            {
                type = "text",
                mime_type = "application/json",
                schema = schema.RootElement
            }
        });

        Exception? lastTransportException = null;
        TimeSpan? delay = null;
        var maxRetries = Math.Max(0, _options.MaxRetries);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (delay is { } wait && wait > TimeSpan.Zero)
            {
                logger.LogWarning(
                    "Gemini 暫時失敗，{Seconds:0.#} 秒後重試（第 {Attempt}/{Max} 次）",
                    wait.TotalSeconds,
                    attempt,
                    maxRetries);
                await Task.Delay(wait, cancellationToken);
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-goog-api-key", apiKey);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                lastTransportException = ex;
                if (attempt == maxRetries)
                    break;

                delay = ExponentialBackoff(attempt);
                continue;
            }

            using (response)
            {
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                    return ExtractModelOutput(payload);

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new AiServiceUnavailableException(
                        "Gemini 拒絕存取：API key 無效或專案權限不足");
                }

                var isTransient = response.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500;

                if (!isTransient)
                {
                    throw new AiServiceUnavailableException(
                        $"Gemini 請求失敗（HTTP {(int)response.StatusCode}）");
                }

                if (attempt == maxRetries)
                    break;

                delay = response.StatusCode == HttpStatusCode.TooManyRequests
                    ? SuggestedRetryDelay(response, payload) ?? ExponentialBackoff(attempt)
                    : ExponentialBackoff(attempt);
            }
        }

        throw new AiServiceUnavailableException(
            $"Gemini 重試 {maxRetries} 次後仍失敗，請稍後再試",
            lastTransportException);
    }

    private static TimeSpan ExponentialBackoff(int attempt) =>
        TimeSpan.FromSeconds(Math.Pow(2, attempt));

    private static TimeSpan? SuggestedRetryDelay(HttpResponseMessage response, string payload)
    {
        if (response.Headers.RetryAfter?.Delta is { } retryAfter)
            return retryAfter;

        if (response.Headers.RetryAfter?.Date is { } retryDate)
        {
            var delay = retryDate - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("error", out var error) ||
                !error.TryGetProperty("details", out var details) ||
                details.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var detail in details.EnumerateArray())
            {
                if (detail.TryGetProperty("retryDelay", out var retryDelay) &&
                    retryDelay.GetString() is { } text &&
                    text.EndsWith('s') &&
                    double.TryParse(
                        text[..^1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }
        }
        catch (JsonException)
        {
            // 無法解析建議等待時間時，呼叫端會改用指數退避。
        }

        return null;
    }

    private static string ExtractModelOutput(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("steps", out var steps) &&
                steps.ValueKind == JsonValueKind.Array)
            {
                foreach (var step in steps.EnumerateArray())
                {
                    if (!step.TryGetProperty("type", out var type) ||
                        type.GetString() != "model_output" ||
                        !step.TryGetProperty("content", out var content) ||
                        content.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var part in content.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var text) &&
                            text.GetString() is { Length: > 0 } json)
                        {
                            return json;
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            throw new AiServiceUnavailableException("Gemini 回應不是合法 JSON", ex);
        }

        throw new AiServiceUnavailableException(
            "Gemini 回應中沒有 model_output，無法取得結果");
    }
}
