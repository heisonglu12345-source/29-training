using OrderHub.Core.Ai;
using OrderHub.Core.Domain;
using OrderHub.Infrastructure.Repositories;

namespace OrderHub.Tests;

public class OrderRepositorySearchTests
{
    [Fact]
    public async Task Search_FiltersStatusTierAndInclusiveDateRange()
    {
        using var db = TestSetup.CreateContext();
        var gold = TestSetup.AddCustomer(db, CustomerTier.Gold, "金卡客戶");
        var standard = TestSetup.AddCustomer(db, CustomerTier.Standard, "一般客戶");

        db.Orders.AddRange(
            CreateOrder(gold, OrderStatus.Cancelled, new DateTime(2026, 7, 1, 0, 0, 0)),
            CreateOrder(gold, OrderStatus.Cancelled, new DateTime(2026, 7, 31, 23, 59, 59)),
            CreateOrder(gold, OrderStatus.Cancelled, new DateTime(2026, 8, 1, 0, 0, 0)),
            CreateOrder(standard, OrderStatus.Cancelled, new DateTime(2026, 7, 15)),
            CreateOrder(gold, OrderStatus.Shipped, new DateTime(2026, 7, 15)));
        await db.SaveChangesAsync();

        var results = await new OrderRepository(db).SearchAsync(new OrderSearchQuery
        {
            Status = OrderStatus.Cancelled,
            MemberTier = CustomerTier.Gold,
            DateFrom = new DateTime(2026, 7, 1),
            DateTo = new DateTime(2026, 7, 31)
        });

        Assert.Equal(2, results.Count);
        Assert.All(results, order => Assert.Equal(CustomerTier.Gold, order.Customer!.Tier));
        Assert.All(results, order => Assert.Equal(OrderStatus.Cancelled, order.Status));
        Assert.Equal(new DateTime(2026, 7, 31, 23, 59, 59), results[0].CreatedAt);
    }

    [Fact]
    public async Task Search_WideFilter_ReturnsNewestOneHundred()
    {
        using var db = TestSetup.CreateContext();
        var customer = TestSetup.AddCustomer(db);
        var start = new DateTime(2026, 1, 1);

        db.Orders.AddRange(Enumerable.Range(0, 105).Select(index =>
            CreateOrder(customer, OrderStatus.Pending, start.AddDays(index))));
        await db.SaveChangesAsync();

        var results = await new OrderRepository(db).SearchAsync(new OrderSearchQuery
        {
            Status = OrderStatus.Pending
        });

        Assert.Equal(100, results.Count);
        Assert.Equal(start.AddDays(104), results[0].CreatedAt);
        Assert.Equal(start.AddDays(5), results[^1].CreatedAt);
    }

    private static Order CreateOrder(Customer customer, OrderStatus status, DateTime createdAt) =>
        new()
        {
            CustomerId = customer.Id,
            Customer = customer,
            Status = status,
            CreatedAt = createdAt
        };
}
