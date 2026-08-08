using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using OrderHub.Core.Domain;
using OrderHub.Infrastructure.Gemini;

namespace OrderHub.Tests;

public class GeminiOrderQueryTranslatorTests
{
    [Fact]
    public async Task Translate_ValidJson_MapsOnlyWhitelistedFields()
    {
        var client = new StubGeminiJsonClient(
            """{"intent":"search","status":"Cancelled","memberTier":"Gold","dateFrom":"2026-07-01","dateTo":"2026-07-31"}""");
        var translator = new GeminiOrderQueryTranslator(
            client,
            NullLogger<GeminiOrderQueryTranslator>.Instance);

        var result = await translator.TranslateAsync("上個月金卡會員取消的訂單");

        Assert.NotNull(result);
        Assert.Equal(OrderStatus.Cancelled, result.Status);
        Assert.Equal(CustomerTier.Gold, result.MemberTier);
        Assert.Equal(new DateTime(2026, 7, 1), result.DateFrom);
        Assert.Equal(new DateTime(2026, 7, 31), result.DateTo);
        Assert.Contains(
            DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            client.LastInput);
        Assert.Contains("上個月金卡會員取消的訂單", client.LastInput);
        Assert.Contains("unsupported", client.LastSchema);
    }

    [Fact]
    public async Task Translate_UnsupportedOrInvalidModelOutput_ReturnsNull()
    {
        var invalidOutputs = new[]
        {
            """{"intent":"unsupported"}""",
            """{"intent":"search","status":"99"}""",
            """{"intent":"search","memberTier":"Platinum"}""",
            """{"intent":"search","dateFrom":"07/01/2026"}""",
            """{"intent":"search","status":"Pending","sql":"DROP TABLE Orders"}""",
            "not-json"
        };

        foreach (var output in invalidOutputs)
        {
            var translator = new GeminiOrderQueryTranslator(
                new StubGeminiJsonClient(output),
                NullLogger<GeminiOrderQueryTranslator>.Instance);

            var result = await translator.TranslateAsync("測試輸入");

            Assert.Null(result);
        }
    }

    [Fact]
    public async Task Translate_SearchWithoutFilters_ReturnsFilterlessQueryForServiceToReject()
    {
        var translator = new GeminiOrderQueryTranslator(
            new StubGeminiJsonClient("""{"intent":"search"}"""),
            NullLogger<GeminiOrderQueryTranslator>.Instance);

        var result = await translator.TranslateAsync("所有訂單");

        Assert.NotNull(result);
        Assert.False(result.HasAnyFilter);
    }

    private sealed class StubGeminiJsonClient(string output) : IGeminiJsonClient
    {
        public string LastInput { get; private set; } = string.Empty;
        public string LastSchema { get; private set; } = string.Empty;

        public Task<string> GenerateJsonAsync(
            string input,
            string responseSchemaJson,
            CancellationToken cancellationToken = default)
        {
            LastInput = input;
            LastSchema = responseSchemaJson;
            return Task.FromResult(output);
        }
    }
}
