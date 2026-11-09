using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Dsw2025Tpi.IntegrationTests.Orders;

// Creación de orden (Cliente): POST /api/orders contra la base SQLite.
[Collection(IntegrationTestsCollection.Name)]
public class OrdersCreationIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;

    public OrdersCreationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(Customer Customer, Product Product)> SeedCustomerAndProductAsync(int stock)
    {
        var customer = new Customer(Guid.NewGuid(), "Cliente Test", "cliente@test.com");
        var product = new Product("MOU001", "INT-001", "Mouse Gamer", "Descripción", 1500m, stock);
        await _factory.SeedAsync(customer, product);
        return (customer, product);
    }

    private static object OrderPayload(Guid customerId, Guid productId, int quantity) => new
    {
        shippingAddress = "Av. Siempre Viva 742",
        billingAddress = "Av. Siempre Viva 742",
        notes = (string?)null,
        customerId,
        orderItems = new[] { new { quantity, productId } }
    };

    private Task<HttpClient> CreateCustomerClientAsync() =>
        _factory.CreateAuthenticatedClientAsync(CustomWebApplicationFactory.CustomerUserName, CustomWebApplicationFactory.CustomerPassword);

    private Task<int> GetStockAsync(Guid productId) =>
        _factory.ExecuteDbContextAsync(db => db.Products.Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync());

    private Task<int> CountOrdersAsync() => _factory.ExecuteDbContextAsync(db => db.Orders.CountAsync());

    [Fact]
    public async Task CreateOrder_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (customer, product) = await SeedCustomerAndProductAsync(stock: 10);
        var client = _factory.CreateClient(); // sin header Authorization

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", OrderPayload(customer.Id, product.Id, 1));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CountOrdersAsync()).Should().Be(0);
        (await GetStockAsync(product.Id)).Should().Be(10);
    }

    [Fact]
    public async Task CreateOrder_WhenAuthenticatedWithValidData_ReturnsCreatedAndPersistsOrderAndStockInSqlite()
    {
        // Arrange
        var (customer, product) = await SeedCustomerAndProductAsync(stock: 10);
        var client = await CreateCustomerClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", OrderPayload(customer.Id, product.Id, 3));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var savedOrder = await _factory.ExecuteDbContextAsync(db =>
            db.Orders.Include(o => o.OrderItems).SingleAsync());
        savedOrder.CustomerId.Should().Be(customer.Id);
        savedOrder.OrderItems.Should().ContainSingle()
            .Which.Should().Match<OrderItem>(i => i.ProductId == product.Id && i.Quantity == 3);
        response.Headers.Location!.ToString().Should().EndWith($"/api/orders/{savedOrder.Id}");

        (await GetStockAsync(product.Id)).Should().Be(7);
    }

    [Fact]
    public async Task CreateOrder_WhenStockIsInsufficient_ReturnsBadRequestWithErrorMessage()
    {
        // Arrange
        var (customer, product) = await SeedCustomerAndProductAsync(stock: 2);
        var client = await CreateCustomerClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", OrderPayload(customer.Id, product.Id, 5));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Contain("json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("detail").GetString().Should()
            .Contain("Stock insuficiente").And.Contain("disponible 2").And.Contain("solicitado 5");

        (await CountOrdersAsync()).Should().Be(0);
        (await GetStockAsync(product.Id)).Should().Be(2);
    }
}
