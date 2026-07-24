using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;
using OrderHub.Core.Models;

namespace OrderHub.Core.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync() => _productRepository.GetAllAsync();

    public Task<IReadOnlyList<Product>> GetActiveAsync() => _productRepository.GetActiveAsync();

    public Task<IReadOnlyList<LowStockProduct>> GetLowStockAsync(int threshold)
    {
        var soldSince = DateTime.UtcNow.AddDays(-30);
        return _productRepository.GetLowStockAsync(threshold, soldSince);
    }
}
