using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Ai;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using OrderHub.Web.Controllers;
using OrderHub.Web.ViewModels;

namespace OrderHub.Tests;

public class OrdersControllerSearchTests
{
    [Fact]
    public async Task Search_WithoutQuery_ShowsEmptySearchPage()
    {
        using var db = TestSetup.CreateContext();
        var controller = CreateController(db, (_, _) =>
            throw new InvalidOperationException("空查詢不應呼叫 service"));

        var result = await controller.Search(null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrderSearchViewModel>(view.Model);
        Assert.False(model.HasSearched);
        Assert.Empty(model.Orders);
        Assert.Null(model.ErrorMessage);
    }

    [Fact]
    public async Task Search_ValidQuery_MapsOrdersAndCalculatesTotal()
    {
        using var db = TestSetup.CreateContext();
        var order = new Order
        {
            Id = 42,
            Customer = new Customer { Name = "金卡客戶", Tier = CustomerTier.Gold },
            Status = OrderStatus.Cancelled,
            CreatedAt = new DateTime(2026, 7, 15),
            Items = new List<OrderItem>
            {
                new() { Quantity = 1, UnitPriceSnapshot = 100m }
            }
        };
        var controller = CreateController(db, (_, _) => Task.FromResult(
            ServiceResult<IReadOnlyList<Order>>.Ok(new[] { order })));

        var result = await controller.Search("上個月金卡會員取消的訂單", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrderSearchViewModel>(view.Model);
        var row = Assert.Single(model.Orders);
        Assert.Equal("上個月金卡會員取消的訂單", model.Query);
        Assert.Equal(42, row.Id);
        Assert.Equal("金卡客戶", row.CustomerName);
        Assert.Equal(OrderStatus.Cancelled, row.Status);
        Assert.Equal(90m, row.Total);
        Assert.Equal(1, row.ItemCount);
        Assert.Null(model.ErrorMessage);
    }

    [Fact]
    public async Task Search_UnsupportedQuery_ShowsWarning()
    {
        using var db = TestSetup.CreateContext();
        var controller = CreateController(db, (_, _) => Task.FromResult(
            ServiceResult<IReadOnlyList<Order>>.Fail("無法理解的查詢")));

        var result = await controller.Search("幫我把所有訂單刪掉", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrderSearchViewModel>(view.Model);
        Assert.Equal("無法理解的查詢", model.ErrorMessage);
        Assert.Empty(model.Orders);
    }

    [Fact]
    public async Task Search_AiUnavailable_ShowsWarningInsteadOfThrowing()
    {
        using var db = TestSetup.CreateContext();
        var controller = CreateController(db, (_, _) =>
            throw new AiServiceUnavailableException("Gemini API key 未設定"));

        var result = await controller.Search("待處理訂單", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrderSearchViewModel>(view.Model);
        Assert.Equal("Gemini API key 未設定", model.ErrorMessage);
    }

    private static OrdersController CreateController(
        Infrastructure.Data.OrderHubDbContext db,
        Func<string, CancellationToken, Task<ServiceResult<IReadOnlyList<Order>>>> handler) =>
        new(
            TestSetup.CreateOrderService(db),
            new StubSearchService(handler),
            null!,
            null!);

    private sealed class StubSearchService(
        Func<string, CancellationToken, Task<ServiceResult<IReadOnlyList<Order>>>> handler)
        : IOrderSearchService
    {
        public Task<ServiceResult<IReadOnlyList<Order>>> SearchAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            handler(query, cancellationToken);
    }
}
