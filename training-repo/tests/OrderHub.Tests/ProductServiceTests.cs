using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task GetAll_ReturnsAllProductsIncludingInactive()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetAllAsync();

        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task GetActive_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetActiveAsync();

        Assert.All(products, p => Assert.True(p.IsActive));
        Assert.Single(products);
    }

    [Fact]
    public async Task GetLowStock_FiltersBelowThresholdAndSortsByStock()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, stock: 9, sku: "SKU-STOCK-09");
        TestSetup.AddProduct(db, stock: 10, sku: "SKU-STOCK-10");
        TestSetup.AddProduct(db, stock: 2, sku: "SKU-STOCK-02");

        var products = await service.GetLowStockAsync(10);

        Assert.Equal(new[] { 2, 9 }, products.Select(p => p.StockQuantity));
        Assert.DoesNotContain(products, p => p.StockQuantity == 10);
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var activeProduct = TestSetup.AddProduct(db, stock: 3, sku: "SKU-ACTIVE");
        TestSetup.AddProduct(db, stock: 1, isActive: false, sku: "SKU-INACTIVE");

        var products = await service.GetLowStockAsync(10);

        var product = Assert.Single(products);
        Assert.Equal(activeProduct.Sku, product.Sku);
    }

    [Fact]
    public async Task GetLowStock_SoldQuantityExcludesCancelledAndOldOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 3, unitPrice: 100m);
        var now = DateTime.UtcNow;

        db.Orders.AddRange(
            CreateOrder(customer.Id, product.Id, OrderStatus.Confirmed, now.AddDays(-5), quantity: 3),
            CreateOrder(customer.Id, product.Id, OrderStatus.Cancelled, now.AddDays(-2), quantity: 7),
            CreateOrder(customer.Id, product.Id, OrderStatus.Shipped, now.AddDays(-31), quantity: 11));
        db.SaveChanges();

        var products = await service.GetLowStockAsync(10);

        var result = Assert.Single(products);
        Assert.Equal(3, result.SoldQuantityLast30Days);
    }

    private static Order CreateOrder(
        int customerId,
        int productId,
        OrderStatus status,
        DateTime createdAt,
        int quantity)
    {
        return new Order
        {
            CustomerId = customerId,
            Status = status,
            CreatedAt = createdAt,
            Items =
            {
                new OrderItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPriceSnapshot = 100m
                }
            }
        };
    }
}
