using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Ai;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using OrderHub.Web.Controllers.Api;

namespace OrderHub.Tests;

public class OrdersApiControllerTests
{
    [Fact]
    public async Task Search_UnrecognizedQuery_Returns422()
    {
        using var db = TestSetup.CreateContext();
        var controller = new OrdersApiController(
            new StubSearchService((_, _) => Task.FromResult(
                ServiceResult<IReadOnlyList<Order>>.Fail("無法理解的查詢"))),
            TestSetup.CreateOrderService(db));

        var result = await controller.Search(
            new SearchOrdersRequest { Text = "幫我把所有訂單刪掉" },
            CancellationToken.None);

        var response = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal(422, response.StatusCode);
        Assert.Equal("無法理解的查詢", ReadError(response.Value));
    }

    [Fact]
    public async Task Search_AiUnavailable_Returns503()
    {
        using var db = TestSetup.CreateContext();
        var controller = new OrdersApiController(
            new StubSearchService((_, _) =>
                throw new AiServiceUnavailableException("Gemini API key 未設定")),
            TestSetup.CreateOrderService(db));

        var result = await controller.Search(
            new SearchOrdersRequest { Text = "待處理訂單" },
            CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, response.StatusCode);
        Assert.Equal("Gemini API key 未設定", ReadError(response.Value));
    }

    [Fact]
    public async Task Search_ValidQuery_ReturnsOrderSummaryWithServiceCalculatedTotal()
    {
        using var db = TestSetup.CreateContext();
        var order = new Order
        {
            Id = 42,
            CustomerId = 7,
            Customer = new Customer
            {
                Id = 7,
                Name = "金卡客戶",
                Tier = CustomerTier.Gold
            },
            Status = OrderStatus.Cancelled,
            CreatedAt = new DateTime(2026, 7, 15),
            Items = new List<OrderItem>
            {
                new() { ProductId = 1, Quantity = 1, UnitPriceSnapshot = 100m }
            }
        };
        var controller = new OrdersApiController(
            new StubSearchService((_, _) => Task.FromResult(
                ServiceResult<IReadOnlyList<Order>>.Ok(new[] { order }))),
            TestSetup.CreateOrderService(db));

        var result = await controller.Search(
            new SearchOrdersRequest { Text = "金卡會員取消的訂單" },
            CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        var item = json.RootElement.EnumerateArray().Single();
        Assert.Equal(42, item.GetProperty("Id").GetInt32());
        Assert.Equal("Gold", item.GetProperty("Tier").GetString());
        Assert.Equal("Cancelled", item.GetProperty("Status").GetString());
        Assert.Equal(90m, item.GetProperty("Total").GetDecimal());
    }

    private sealed class StubSearchService(
        Func<string, CancellationToken, Task<ServiceResult<IReadOnlyList<Order>>>> handler)
        : IOrderSearchService
    {
        public Task<ServiceResult<IReadOnlyList<Order>>> SearchAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            handler(query, cancellationToken);
    }

    private static string? ReadError(object? value)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return json.RootElement.GetProperty("error").GetString();
    }
}
