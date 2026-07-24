using Microsoft.EntityFrameworkCore;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;
using OrderHub.Core.Models;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly OrderHubDbContext _db;

    public ProductRepository(OrderHubDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync() =>
        await _db.Products.OrderBy(p => p.Sku).ToListAsync();

    public async Task<IReadOnlyList<Product>> GetActiveAsync() =>
        await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Sku).ToListAsync();

    public async Task<IReadOnlyList<LowStockProduct>> GetLowStockAsync(int threshold, DateTime soldSince) =>
        await _db.Products
            .Where(p => p.IsActive && p.StockQuantity < threshold)
            .OrderBy(p => p.StockQuantity)
            .ThenBy(p => p.Sku)
            .Select(p => new LowStockProduct
            {
                Sku = p.Sku,
                Name = p.Name,
                StockQuantity = p.StockQuantity,
                SoldQuantityLast30Days = _db.OrderItems
                    .Where(i => i.ProductId == p.Id
                        && i.Order != null
                        && i.Order.CreatedAt >= soldSince
                        && i.Order.Status != OrderStatus.Cancelled)
                    .Sum(i => (int?)i.Quantity) ?? 0
            })
            .ToListAsync();

    public Task<Product?> GetByIdAsync(int id) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
