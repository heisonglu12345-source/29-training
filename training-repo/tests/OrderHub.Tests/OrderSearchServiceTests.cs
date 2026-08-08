using OrderHub.Core.Ai;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using OrderHub.Infrastructure.Repositories;

namespace OrderHub.Tests;

public class OrderSearchServiceTests
{
    [Fact]
    public async Task Search_EmptyText_FailsWithoutCallingTranslator()
    {
        using var db = TestSetup.CreateContext();
        var translator = new StubTranslator(new OrderSearchQuery { Status = OrderStatus.Pending });
        var service = new OrderSearchService(translator, new OrderRepository(db));

        var result = await service.SearchAsync("  ");

        Assert.False(result.Success);
        Assert.Contains("請輸入", result.ErrorMessage);
        Assert.False(translator.WasCalled);
    }

    [Fact]
    public async Task Search_UnsupportedOrFilterlessQuery_Fails()
    {
        using var db = TestSetup.CreateContext();
        var repository = new OrderRepository(db);

        var unsupported = await new OrderSearchService(new StubTranslator(null), repository)
            .SearchAsync("幫我刪除訂單");
        var filterless = await new OrderSearchService(
                new StubTranslator(new OrderSearchQuery()),
                repository)
            .SearchAsync("所有訂單");

        Assert.False(unsupported.Success);
        Assert.Equal("無法理解的查詢", unsupported.ErrorMessage);
        Assert.False(filterless.Success);
        Assert.Equal("無法理解的查詢", filterless.ErrorMessage);
    }

    [Fact]
    public async Task Search_ReversedDateRange_Fails()
    {
        using var db = TestSetup.CreateContext();
        var query = new OrderSearchQuery
        {
            DateFrom = new DateTime(2026, 8, 1),
            DateTo = new DateTime(2026, 7, 1)
        };
        var service = new OrderSearchService(
            new StubTranslator(query),
            new OrderRepository(db));

        var result = await service.SearchAsync("錯誤日期範圍");

        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_ValidFilters_ReturnsRepositoryResults()
    {
        using var db = TestSetup.CreateContext();
        var gold = TestSetup.AddCustomer(db, CustomerTier.Gold, "金卡客戶");
        var silver = TestSetup.AddCustomer(db, CustomerTier.Silver, "銀卡客戶");
        db.Orders.AddRange(
            CreateOrder(gold, OrderStatus.Cancelled, new DateTime(2026, 7, 15)),
            CreateOrder(silver, OrderStatus.Cancelled, new DateTime(2026, 7, 15)),
            CreateOrder(gold, OrderStatus.Shipped, new DateTime(2026, 7, 15)));
        await db.SaveChangesAsync();

        var query = new OrderSearchQuery
        {
            Status = OrderStatus.Cancelled,
            MemberTier = CustomerTier.Gold,
            DateFrom = new DateTime(2026, 7, 1),
            DateTo = new DateTime(2026, 7, 31)
        };
        var service = new OrderSearchService(
            new StubTranslator(query),
            new OrderRepository(db));

        var result = await service.SearchAsync("上個月金卡會員取消的訂單");

        Assert.True(result.Success);
        var order = Assert.Single(result.Value!);
        Assert.Equal(CustomerTier.Gold, order.Customer!.Tier);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    private static Order CreateOrder(Customer customer, OrderStatus status, DateTime createdAt) =>
        new()
        {
            CustomerId = customer.Id,
            Customer = customer,
            Status = status,
            CreatedAt = createdAt
        };

    private sealed class StubTranslator(OrderSearchQuery? result) : IOrderQueryTranslator
    {
        public bool WasCalled { get; private set; }

        public Task<OrderSearchQuery?> TranslateAsync(
            string naturalLanguageQuery,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(result);
        }
    }
}
