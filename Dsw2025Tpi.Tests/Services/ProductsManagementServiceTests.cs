using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using AppException = Dsw2025Tpi.Application.Exceptions.ApplicationException;

namespace Dsw2025Tpi.Tests.Services;

public class ProductsManagementServiceTests
{
    private static ProductsManagementService CreateService(InMemoryRepository repository) =>
        new(repository, NullLogger<ProductsManagementService>.Instance);

    private static Product CreateProduct(string sku, bool isActive = true) =>
        new(sku, $"INT-{sku}", $"Producto {sku}", "Descripción", 100m, 10) { IsActive = isActive };

    private static ProductModel.RequestProductModel CreateRequest(string sku) =>
        new(sku, "INT-001", "Producto", "Descripción", 100m, 10);

    [Fact]
    public async Task AddProduct_RequestValido_GuardaProductoActivo()
    {
        var repository = new InMemoryRepository();
        var service = CreateService(repository);

        var response = await service.AddProduct(CreateRequest("SKU-0001"));

        Assert.Equal("SKU-0001", response.Sku);
        Assert.True(response.IsActive);
        Assert.Single(repository.Entities);
    }

    [Fact]
    public async Task AddProduct_SkuDuplicado_LanzaDuplicatedEntityException()
    {
        var repository = new InMemoryRepository(CreateProduct("SKU-0001"));
        var service = CreateService(repository);

        await Assert.ThrowsAsync<DuplicatedEntityException>(() => service.AddProduct(CreateRequest("SKU-0001")));
        Assert.Single(repository.Entities);
    }

    [Fact]
    public async Task AddProduct_RequestInvalido_NoGuardaNada()
    {
        var repository = new InMemoryRepository();
        var service = CreateService(repository);
        var request = CreateRequest("SKU-0001") with { CurrentUnitPrice = 0 };

        await Assert.ThrowsAsync<AppException>(() => service.AddProduct(request));
        Assert.Empty(repository.Entities);
    }

    [Fact]
    public async Task PatchProduct_ProductoActivo_LoDeshabilita()
    {
        var product = CreateProduct("SKU-0001");
        var service = CreateService(new InMemoryRepository(product));

        await service.PatchProduct(product.Id);

        Assert.False(product.IsActive);
    }

    [Fact]
    public async Task PatchProduct_ProductoYaDeshabilitado_LanzaApplicationException()
    {
        var product = CreateProduct("SKU-0001", isActive: false);
        var service = CreateService(new InMemoryRepository(product));

        await Assert.ThrowsAsync<AppException>(() => service.PatchProduct(product.Id));
    }

    [Fact]
    public async Task PatchProduct_IdInexistente_LanzaEntityNotFoundException()
    {
        var service = CreateService(new InMemoryRepository());

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.PatchProduct(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetProducts_FiltroEnabled_DevuelveSoloActivos()
    {
        var service = CreateService(new InMemoryRepository(
            CreateProduct("SKU-0001"),
            CreateProduct("SKU-0002", isActive: false),
            CreateProduct("SKU-0003")));

        var result = await service.GetProducts(new ProductModel.FilterProduct("enabled", null, 1, 10));

        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.All(result.ProductItems, p => Assert.True(p.IsActive));
    }

    [Fact]
    public async Task GetProducts_SegundaPagina_DevuelveElRestoOrdenadoPorSku()
    {
        var service = CreateService(new InMemoryRepository(
            CreateProduct("SKU-0003"),
            CreateProduct("SKU-0001"),
            CreateProduct("SKU-0002")));

        var result = await service.GetProducts(new ProductModel.FilterProduct(null, null, 2, 2));

        Assert.NotNull(result);
        Assert.Equal(3, result.Total);
        var item = Assert.Single(result.ProductItems);
        Assert.Equal("SKU-0003", item.Sku);
    }

    [Fact]
    public async Task GetProducts_BusquedaSinCoincidencias_LanzaEntityNotFoundException()
    {
        var service = CreateService(new InMemoryRepository(CreateProduct("SKU-0001")));

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => service.GetProducts(new ProductModel.FilterProduct(null, "inexistente", 1, 10)));
    }
}
