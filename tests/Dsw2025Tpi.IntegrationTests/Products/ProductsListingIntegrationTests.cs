using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Dsw2025Tpi.IntegrationTests.Products;

// Listado de productos (Admin): prueba los endpoints GET /api/products y
// GET /api/products/admin contra la base SQLite, con todo el pipeline de la API.
[Collection(IntegrationTestsCollection.Name)]
public class ProductsListingIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public ProductsListingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Product CreateProduct(string sku, string name, bool isActive = true) =>
        new(sku, $"INT-{sku}", name, "Descripción", 1500m, 10) { IsActive = isActive };

    [Fact]
    public async Task GetProducts_PublicEndpoint_ReturnsOkWithPaginatedJsonStructure()
    {
        // Arrange
        await _factory.SeedAsync(CreateProduct("MOU001", "Mouse Gamer"), CreateProduct("TEC001", "Teclado Mecánico"));
        var client = _factory.CreateClient(); // sin credenciales: el endpoint es [AllowAnonymous]

        // Act
        var response = await client.GetAsync("/api/products?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Contrato JSON: propiedades productItems y totalItems
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("productItems").ValueKind.Should().Be(JsonValueKind.Array);
        json.RootElement.GetProperty("totalItems").GetInt32().Should().Be(2);

        // El cuerpo deserializa en la estructura de paginación de la API
        var page = await response.Content.ReadFromJsonAsync<ProductModel.ResponsePagination>();
        page!.TotalItems.Should().Be(2);
        page.ProductItems.Select(p => p.Sku).Should().BeEquivalentTo("MOU001", "TEC001");
        page.ProductItems.Should().OnlyContain(p => p.CurrentUnitPrice == 1500m && p.IsActive);
    }

    [Theory]
    [InlineData(10, 2)] // la página trae los 2 productos que coinciden
    [InlineData(1, 1)]  // pageSize se toma de la query string
    public async Task GetProducts_WithQueryStringFilters_ReturnsFilteredResultsFromSqlite(int pageSize, int expectedItems)
    {
        // Arrange
        await _factory.SeedAsync(
            CreateProduct("MOU001", "Mouse Gamer"),
            CreateProduct("MOU002", "Mouse Inalámbrico"),
            CreateProduct("TEC001", "Teclado Mecánico"));
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/products?search=mouse&pageNumber=1&pageSize={pageSize}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ProductModel.ResponsePagination>();
        page!.TotalItems.Should().Be(2);
        page.ProductItems.Should().HaveCount(expectedItems)
            .And.OnlyContain(p => p.Name!.StartsWith("Mouse"));
    }

    [Fact]
    public async Task GetAuthProducts_AdminEndpoint_WhenCalledByAdmin_IncludesInactiveProducts()
    {
        // Arrange
        await _factory.SeedAsync(
            CreateProduct("MON001", "Monitor"),
            CreateProduct("MON002", "Monitor Discontinuado", isActive: false));
        var adminClient = await _factory.CreateAuthenticatedClientAsync(
            CustomWebApplicationFactory.AdminUserName, CustomWebApplicationFactory.AdminPassword);

        // Act
        var adminResponse = await adminClient.GetAsync("/api/products/admin?pageNumber=1&pageSize=10");
        var publicResponse = await _factory.CreateClient().GetAsync("/api/products?pageNumber=1&pageSize=10");

        // Assert
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminPage = await adminResponse.Content.ReadFromJsonAsync<ProductModel.ResponsePagination>();
        adminPage!.ProductItems.Should().Contain(p => p.Sku == "MON002" && !p.IsActive);
        adminPage.TotalItems.Should().Be(2);

        // A diferencia del endpoint público, que solo muestra los activos
        var publicPage = await publicResponse.Content.ReadFromJsonAsync<ProductModel.ResponsePagination>();
        publicPage!.ProductItems.Should().ContainSingle().Which.Sku.Should().Be("MON001");
    }
}
