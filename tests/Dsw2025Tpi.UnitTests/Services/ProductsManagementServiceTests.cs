using System.Linq.Expressions;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AppException = Dsw2025Tpi.Application.Exceptions.ApplicationException;
using InvalidDataException = Dsw2025Tpi.Application.Exceptions.InvalidDataException;

namespace Dsw2025Tpi.UnitTests.Services;

public class ProductsManagementServiceTests
{
    private readonly Mock<IRepository> _repository = new();
    private readonly ProductsManagementService _service;

    public ProductsManagementServiceTests()
    {
        _service = new ProductsManagementService(_repository.Object, NullLogger<ProductsManagementService>.Instance);
    }

    private static Product CreateProduct(string sku, string name = "Producto", string internalCode = "INT-0", bool isActive = true) =>
        new(sku, internalCode, name, "Descripción", 100m, 10) { IsActive = isActive };

    // El mock aplica el filtro que arma el servicio sobre la lista configurada:
    // así se verifica que el servicio pida a la base los productos correctos.
    private void SetupProducts(params Product[] products) =>
        _repository
            .Setup(r => r.GetFiltered(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
            .ReturnsAsync((Expression<Func<Product, bool>> predicate, string[] _) =>
                products.Where(predicate.Compile()).ToList());

    private void SetupExistingProductsForSku(params Product[] products) =>
        _repository
            .Setup(r => r.First(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
            .ReturnsAsync((Expression<Func<Product, bool>> predicate, string[] _) =>
                products.FirstOrDefault(predicate.Compile()));

    // ---------------------------------------------------------------
    // Creación de productos (Admin)
    // ---------------------------------------------------------------

    [Fact]
    public async Task AddProduct_WithValidData_ReturnsProductAndCallsRepositoryAdd()
    {
        // Arrange
        var request = new ProductModel.RequestProductModel("SKU-0001", "INT-001", "Mouse Gamer", "Mouse inalámbrico", 1500m, 10);
        SetupExistingProductsForSku();
        _repository.Setup(r => r.Add(It.IsAny<Product>())).ReturnsAsync((Product p) => p);

        // Act
        var result = await _service.AddProduct(request);

        // Assert
        result.Sku.Should().Be(request.Sku);
        result.Name.Should().Be(request.Name);
        result.CurrentUnitPrice.Should().Be(request.CurrentUnitPrice);
        result.StockQuantity.Should().Be(request.StockQuantity);
        result.IsActive.Should().BeTrue();
        _repository.Verify(r => r.Add(It.Is<Product>(p =>
            p.Id == result.Id &&
            p.Sku == request.Sku &&
            p.InternalCode == request.InternalCode &&
            p.Name == request.Name &&
            p.CurrentUnitPrice == request.CurrentUnitPrice &&
            p.StockQuantity == request.StockQuantity)), Times.Once);
    }

    [Fact]
    public async Task AddProduct_WithDuplicateSku_ThrowsDuplicatedEntityException()
    {
        // Arrange
        SetupExistingProductsForSku(CreateProduct("SKU-0001"));
        var request = new ProductModel.RequestProductModel("SKU-0001", "INT-002", "Otro producto", "Descripción", 200m, 5);

        // Act
        var act = () => _service.AddProduct(request);

        // Assert
        await act.Should().ThrowAsync<DuplicatedEntityException>().WithMessage("*SKU-0001*");
        _repository.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 100.0)]
    [InlineData(-5, 100.0)]
    [InlineData(10, -1.0)]
    public async Task AddProduct_WithInvalidStockOrPrice_ThrowsInvalidDataException(int stock, double price)
    {
        // Arrange
        var request = new ProductModel.RequestProductModel("SKU-0001", "INT-001", "Producto", "Descripción", (decimal)price, stock);

        // Act
        var act = () => _service.AddProduct(request);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>().Where(e => !string.IsNullOrWhiteSpace(e.Message));
        // La validación ocurre antes de consultar o guardar: el repositorio no se usa
        _repository.VerifyNoOtherCalls();
    }

    // ---------------------------------------------------------------
    // Listado de productos (Admin)
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetProducts_WhenIncludeInactiveIsFalse_ReturnsOnlyActiveProducts()
    {
        // Arrange
        var inactive = CreateProduct("SKU-0002", isActive: false);
        SetupProducts(CreateProduct("SKU-0001"), inactive, CreateProduct("SKU-0003"));

        // Act
        var result = await _service.GetProducts(new ProductModel.FilterProduct(null, null, null, null), includeInactive: false);

        // Assert
        result.ProductItems.Should().HaveCount(2).And.OnlyContain(p => p.IsActive);
        result.ProductItems.Should().NotContain(p => p.Id == inactive.Id);
        result.TotalItems.Should().Be(2);
    }

    [Fact]
    public async Task GetProducts_WhenIncludeInactiveIsTrue_ReturnsActiveAndInactiveProducts()
    {
        // Arrange
        var inactive = CreateProduct("SKU-0002", isActive: false);
        SetupProducts(CreateProduct("SKU-0001"), inactive);

        // Act
        var result = await _service.GetProducts(new ProductModel.FilterProduct(null, null, null, null), includeInactive: true);

        // Assert
        result.ProductItems.Should().HaveCount(2).And.Contain(p => p.Id == inactive.Id);
    }

    [Fact]
    public async Task GetProducts_WithPaginationFilter_AppliesSkipAndTakeCorrectly()
    {
        // Arrange
        var products = Enumerable.Range(1, 15).Select(i => CreateProduct($"SKU-{i:D3}")).ToArray();
        SetupProducts(products);

        // Act
        var result = await _service.GetProducts(new ProductModel.FilterProduct(null, null, pageNumber: 2, PageSize: 5));

        // Assert
        result.ProductItems.Select(p => p.Sku).Should()
            .Equal("SKU-006", "SKU-007", "SKU-008", "SKU-009", "SKU-010");
        result.TotalItems.Should().Be(15);
    }

    [Theory]
    [InlineData("mouse")]   // por nombre
    [InlineData("MoUsE")]   // ignorando mayúsculas/minúsculas
    [InlineData("per-001")] // por SKU
    [InlineData("int-a")]   // por código interno
    public async Task GetProducts_WithSearchTerm_FiltersByNameSkuOrInternalCode(string search)
    {
        // Arrange
        var mouse = CreateProduct("PER-001", "Mouse Gamer", "INT-A");
        SetupProducts(
            mouse,
            CreateProduct("KEY-002", "Teclado Mecánico", "INT-B"),
            CreateProduct("MON-003", "Monitor", "INT-C"));

        // Act
        var result = await _service.GetProducts(new ProductModel.FilterProduct(null, search, 1, 10));

        // Assert
        result.ProductItems.Should().ContainSingle().Which.Id.Should().Be(mouse.Id);
        result.TotalItems.Should().Be(1);
    }

    [Fact]
    public async Task GetProducts_WithSearchWithoutMatches_ReturnsEmptyPage()
    {
        // Arrange
        SetupProducts(CreateProduct("SKU-0001", "Monitor"));

        // Act
        var result = await _service.GetProducts(new ProductModel.FilterProduct(null, "inexistente", 1, 10));

        // Assert
        result.ProductItems.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
    }

    // ---------------------------------------------------------------
    // Deshabilitar productos
    // ---------------------------------------------------------------

    [Fact]
    public async Task PatchProduct_WhenProductIsActive_DisablesIt()
    {
        // Arrange
        var product = CreateProduct("SKU-0001");
        _repository.Setup(r => r.GetById<Product>(product.Id, It.IsAny<string[]>())).ReturnsAsync(product);

        // Act
        await _service.PatchProduct(product.Id);

        // Assert
        product.IsActive.Should().BeFalse();
        _repository.Verify(r => r.Update(product), Times.Once);
    }

    [Fact]
    public async Task PatchProduct_WhenProductIsAlreadyDisabled_ThrowsApplicationException()
    {
        // Arrange
        var product = CreateProduct("SKU-0001", isActive: false);
        _repository.Setup(r => r.GetById<Product>(product.Id, It.IsAny<string[]>())).ReturnsAsync(product);

        // Act
        var act = () => _service.PatchProduct(product.Id);

        // Assert
        await act.Should().ThrowExactlyAsync<AppException>();
        _repository.Verify(r => r.Update(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task PatchProduct_WhenProductDoesNotExist_ThrowsEntityNotFoundException()
    {
        // Act
        var act = () => _service.PatchProduct(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
        _repository.Verify(r => r.Update(It.IsAny<Product>()), Times.Never);
    }
}
