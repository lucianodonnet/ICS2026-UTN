using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Dsw2025Tpi.IntegrationTests.Products;

// Creación de productos (Admin): POST /api/products contra la base SQLite.
[Collection(IntegrationTestsCollection.Name)]
public class ProductsCreationIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public ProductsCreationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static object ValidPayload(string sku = "MOU001") => new
    {
        sku,
        internalCode = "INT-001",
        name = "Mouse Gamer",
        description = "Mouse inalámbrico",
        currentUnitPrice = 1500m,
        stockQuantity = 10
    };

    private Task<HttpClient> CreateAdminClientAsync() =>
        _factory.CreateAuthenticatedClientAsync(CustomWebApplicationFactory.AdminUserName, CustomWebApplicationFactory.AdminPassword);

    private Task<int> CountProductsAsync() => _factory.ExecuteDbContextAsync(db => db.Products.CountAsync());

    [Fact]
    public async Task AddProduct_WhenUnauthenticatedOrNotAdmin_ReturnsUnauthorizedOrForbidden()
    {
        // Arrange
        var anonymousClient = _factory.CreateClient();
        var customerClient = await _factory.CreateAuthenticatedClientAsync(
            CustomWebApplicationFactory.CustomerUserName, CustomWebApplicationFactory.CustomerPassword);

        // Act
        var anonymousResponse = await anonymousClient.PostAsJsonAsync("/api/products", ValidPayload());
        var customerResponse = await customerClient.PostAsJsonAsync("/api/products", ValidPayload());

        // Assert
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        customerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden); // requiere [Authorize(Roles = "Admin")]
        (await CountProductsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddProduct_WhenAuthenticatedAsAdmin_PersistsInSqliteAndReturnsCreated()
    {
        // Arrange
        var client = await CreateAdminClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/products", ValidPayload());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var saved = await _factory.ExecuteDbContextAsync(db => db.Products.SingleAsync(p => p.Sku == "MOU001"));
        saved.InternalCode.Should().Be("INT-001");
        saved.Name.Should().Be("Mouse Gamer");
        saved.Description.Should().Be("Mouse inalámbrico");
        saved.CurrentUnitPrice.Should().Be(1500m);
        saved.StockQuantity.Should().Be(10);
        saved.IsActive.Should().BeTrue();
        response.Headers.Location!.ToString().Should().EndWith($"/api/products/{saved.Id}");
    }

    [Fact]
    public async Task AddProduct_WhenSkuAlreadyExistsInDatabase_ReturnsConflictStatusCode()
    {
        // Arrange
        await _factory.SeedAsync(new Product("MOU001", "INT-900", "Mouse existente", "Descripción", 900m, 5));
        var client = await CreateAdminClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/products", ValidPayload(sku: "MOU001"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict); // y no un 500
        (await response.Content.ReadAsStringAsync()).Should().Contain("MOU001");
        (await CountProductsAsync()).Should().Be(1);
    }
}
