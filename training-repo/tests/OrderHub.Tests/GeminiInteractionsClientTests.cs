using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderHub.Core.Ai;
using OrderHub.Infrastructure.Gemini;

namespace OrderHub.Tests;

public class GeminiInteractionsClientTests
{
    private const string Schema = """{"type":"object","properties":{"intent":{"type":"string"}},"required":["intent"]}""";

    [Fact]
    public async Task GenerateJson_Success_SendsStructuredOutputRequestAndExtractsText()
    {
        var handler = new QueueHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(
                """{"steps":[{"type":"model_output","content":[{"type":"text","text":"{\"intent\":\"search\",\"status\":\"Pending\"}"}]}]}""")
        });
        var client = CreateClient(handler);

        var result = await client.GenerateJsonAsync("查待處理訂單", Schema);

        Assert.Equal("""{"intent":"search","status":"Pending"}""", result);
        Assert.Equal("test-key", handler.ApiKeys.Single());

        using var request = JsonDocument.Parse(handler.Bodies.Single());
        Assert.Equal("gemini-3.5-flash", request.RootElement.GetProperty("model").GetString());
        var responseFormat = request.RootElement.GetProperty("response_format");
        Assert.Equal("application/json", responseFormat.GetProperty("mime_type").GetString());
        Assert.Equal("object", responseFormat.GetProperty("schema").GetProperty("type").GetString());
    }

    [Fact]
    public async Task GenerateJson_MissingKey_ThrowsClearUnavailableErrorWithoutSendingRequest()
    {
        var handler = new QueueHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler, new GeminiOptions { ApiKey = " " });

        var exception = await Assert.ThrowsAsync<AiServiceUnavailableException>(() =>
            client.GenerateJsonAsync("test", Schema));

        Assert.Contains("API key 未設定", exception.Message);
        Assert.Equal(0, handler.SendCount);
    }

    [Fact]
    public async Task GenerateJson_TooManyRequests_RetriesThenSucceeds()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = JsonContent("""{"error":{"details":[{"retryDelay":"0s"}]}}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """{"steps":[{"type":"model_output","content":[{"type":"text","text":"{\"intent\":\"search\"}"}]}]}""")
            }
        });
        var handler = new QueueHttpMessageHandler(_ => responses.Dequeue());
        var client = CreateClient(handler, new GeminiOptions
        {
            ApiKey = "test-key",
            MaxRetries = 1
        });

        var result = await client.GenerateJsonAsync("test", Schema);

        Assert.Equal("""{"intent":"search"}""", result);
        Assert.Equal(2, handler.SendCount);
    }

    [Fact]
    public async Task GenerateJson_NonTransientClientError_DoesNotRetry()
    {
        var handler = new QueueHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest));
        var client = CreateClient(handler, new GeminiOptions
        {
            ApiKey = "test-key",
            MaxRetries = 4
        });

        var exception = await Assert.ThrowsAsync<AiServiceUnavailableException>(() =>
            client.GenerateJsonAsync("test", Schema));

        Assert.Contains("HTTP 400", exception.Message);
        Assert.Equal(1, handler.SendCount);
    }

    [Fact]
    public async Task GenerateJson_UnauthorizedOrMissingOutput_ThrowsClearUnavailableError()
    {
        var unauthorized = CreateClient(new QueueHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var missingOutput = CreateClient(new QueueHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{"status":"completed","steps":[]}""")
            }));

        var unauthorizedError = await Assert.ThrowsAsync<AiServiceUnavailableException>(() =>
            unauthorized.GenerateJsonAsync("test", Schema));
        var outputError = await Assert.ThrowsAsync<AiServiceUnavailableException>(() =>
            missingOutput.GenerateJsonAsync("test", Schema));

        Assert.Contains("拒絕存取", unauthorizedError.Message);
        Assert.Contains("沒有 model_output", outputError.Message);
    }

    private static GeminiInteractionsClient CreateClient(
        HttpMessageHandler handler,
        GeminiOptions? options = null) =>
        new(
            new HttpClient(handler),
            Options.Create(options ?? new GeminiOptions
            {
                ApiKey = "test-key",
                MaxRetries = 0
            }),
            NullLogger<GeminiInteractionsClient>.Instance);

    private static StringContent JsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private sealed class QueueHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int SendCount { get; private set; }
        public List<string> Bodies { get; } = new();
        public List<string?> ApiKeys { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            ApiKeys.Add(request.Headers.TryGetValues("x-goog-api-key", out var values)
                ? values.SingleOrDefault()
                : null);
            return responseFactory(request);
        }
    }
}
