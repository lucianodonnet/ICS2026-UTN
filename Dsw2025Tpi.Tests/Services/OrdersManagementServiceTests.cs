using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Tests.Fakes;

namespace Dsw2025Tpi.Tests.Services;

public class OrdersManagementServiceTests
{
    private static Customer CreateCustomer() => new(Guid.NewGuid(), "Cliente", "cliente@mail.com");

    private static Product CreateProduct(int stock, decimal price = 100m) =>
        new("SKU-0001", "INT-001", "Producto", "Descripción", price, stock);

    private static OrderModel.RequestOrderModel CreateRequest(Guid customerId, Guid productId, int quantity) =>
        new("Av. Siempre Viva 742", "Av. Siempre Viva 742", null, customerId,
            [new OrderItemModel.RequestOrderItemModel(quantity, productId)]);

    [Fact]
    public async Task AddOrder_ConStock_DescuentaStockYCalculaTotal()
    {
        var customer = CreateCustomer();
        var product = CreateProduct(stock: 10);
        var repository = new InMemoryRepository(customer, product);
        var service = new OrdersManagementService(repository);

        var response = await service.AddOrder(CreateRequest(customer.Id, product.Id, 3));

        Assert.Equal(7, product.StockQuantity);
        Assert.Equal(300m, response.TotalAmount);
        Assert.Single(repository.Entities.OfType<Order>());
    }

    [Fact]
    public async Task AddOrder_StockInsuficiente_LanzaInvalidOperationExceptionSinTocarStock()
    {
        var customer = CreateCustomer();
        var product = CreateProduct(stock: 2);
        var repository = new InMemoryRepository(customer, product);
        var service = new OrdersManagementService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddOrder(CreateRequest(customer.Id, product.Id, 5)));
        Assert.Equal(2, product.StockQuantity);
        Assert.Empty(repository.Entities.OfType<Order>());
    }

    [Fact]
    public async Task AddOrder_ClienteInexistente_LanzaEntityNotFoundException()
    {
        var product = CreateProduct(stock: 10);
        var service = new OrdersManagementService(new InMemoryRepository(product));

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => service.AddOrder(CreateRequest(Guid.NewGuid(), product.Id, 1)));
    }

    [Fact]
    public async Task GetAllOrders_SinOrdenes_DevuelvePaginacionVacia()
    {
        var service = new OrdersManagementService(new InMemoryRepository());

        var result = await service.GetAllOrders(new OrderModel.SearchOrder(null, null));

        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetAllOrders_NoIncluyeOrdenesCanceladas()
    {
        var customer = CreateCustomer();
        var pending = new Order("Envío", "Facturación", null, customer.Id);
        var cancelled = new Order("Envío", "Facturación", null, customer.Id) { Status = OrderStatus.CANCELLED };
        var service = new OrdersManagementService(new InMemoryRepository(customer, pending, cancelled));

        var result = await service.GetAllOrders(new OrderModel.SearchOrder(null, null));

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(pending.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task UpdateOrderStatus_Cancelar_DevuelveElStock()
    {
        var customer = CreateCustomer();
        var product = CreateProduct(stock: 7);
        var order = new Order("Envío", "Facturación", null, customer.Id);
        order.OrderItems.Add(new OrderItem(3, product.CurrentUnitPrice, order.Id, product.Id));
        var service = new OrdersManagementService(new InMemoryRepository(customer, product, order));

        var response = await service.UpdateOrderStatus(order.Id, "cancelled");

        Assert.Equal(OrderStatus.CANCELLED, response.Status);
        Assert.Equal(10, product.StockQuantity);
    }

    [Fact]
    public async Task UpdateOrderStatus_EstadoNumerico_LanzaArgumentException()
    {
        var customer = CreateCustomer();
        var order = new Order("Envío", "Facturación", null, customer.Id);
        var service = new OrdersManagementService(new InMemoryRepository(customer, order));

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateOrderStatus(order.Id, "2"));
    }
}
