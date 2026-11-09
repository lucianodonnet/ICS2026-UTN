using System.Linq.Expressions;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace Dsw2025Tpi.UnitTests.Services;

public class OrdersManagementServiceTests
{
    private readonly Mock<IRepository> _repository = new();
    private readonly OrdersManagementService _service;

    public OrdersManagementServiceTests()
    {
        _service = new OrdersManagementService(_repository.Object);
        _repository.Setup(r => r.Update(It.IsAny<Product>())).ReturnsAsync((Product p) => p);
        _repository.Setup(r => r.Add(It.IsAny<Order>())).ReturnsAsync((Order o) => o);
    }

    private Customer SetupCustomer()
    {
        var customer = new Customer(Guid.NewGuid(), "Cliente", "cliente@mail.com");
        _repository.Setup(r => r.GetById<Customer>(customer.Id, It.IsAny<string[]>())).ReturnsAsync(customer);
        return customer;
    }

    private Product SetupProduct(int stock, decimal price = 100m)
    {
        var product = new Product("SKU-0001", "INT-001", "Producto", "Descripción", price, stock);
        _repository.Setup(r => r.GetById<Product>(product.Id, It.IsAny<string[]>())).ReturnsAsync(product);
        return product;
    }

    private static OrderModel.RequestOrderModel CreateRequest(Guid customerId, params (int Quantity, Guid ProductId)[] items) =>
        new("Av. Siempre Viva 742", "Av. Siempre Viva 742", null, customerId,
            items.Select(i => new OrderItemModel.RequestOrderItemModel(i.Quantity, i.ProductId)).ToList());

    private void VerifyNothingWasPersisted()
    {
        _repository.Verify(r => r.Update(It.IsAny<Product>()), Times.Never);
        _repository.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // Creación de orden (Cliente)
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_WithValidItemsAndStock_CalculatesTotalAndDecrementsProductStock()
    {
        // Arrange
        var customer = SetupCustomer();
        var mouse = SetupProduct(stock: 10, price: 100m);
        var cable = SetupProduct(stock: 5, price: 50.25m);
        var request = CreateRequest(customer.Id, (3, mouse.Id), (2, cable.Id));

        // Act
        var result = await _service.CreateOrder(request);

        // Assert
        mouse.StockQuantity.Should().Be(7);
        cable.StockQuantity.Should().Be(3);
        result.TotalAmount.Should().Be(3 * 100m + 2 * 50.25m);
        result.TotalAmount.Should().Be(result.OrderItems.Sum(i => i.SubTotal));
        _repository.Verify(r => r.Update(It.Is<Product>(p => p.Id == mouse.Id && p.StockQuantity == 7)), Times.Once);
        _repository.Verify(r => r.Update(It.Is<Product>(p => p.Id == cable.Id && p.StockQuantity == 3)), Times.Once);
        _repository.Verify(r => r.Add(It.Is<Order>(o =>
            o.CustomerId == customer.Id &&
            o.OrderItems.Count == 2 &&
            o.TotalAmount == 400.50m)), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WhenProductStockIsInsufficient_ThrowsInsufficientStockException()
    {
        // Arrange
        var customer = SetupCustomer();
        var product = SetupProduct(stock: 2);
        var request = CreateRequest(customer.Id, (5, product.Id));

        // Act
        var act = () => _service.CreateOrder(request);

        // Assert
        await act.Should().ThrowAsync<InsufficientStockException>().WithMessage("*disponible 2*solicitado 5*");
        product.StockQuantity.Should().Be(2);
        VerifyNothingWasPersisted();
    }

    [Fact]
    public async Task CreateOrder_WhenCustomerDoesNotExist_ThrowsCustomerNotFoundException()
    {
        // Arrange
        var product = SetupProduct(stock: 10);
        var request = CreateRequest(Guid.NewGuid(), (1, product.Id));

        // Act
        var act = () => _service.CreateOrder(request);

        // Assert
        await act.Should().ThrowAsync<CustomerNotFoundException>();
        // Se corta de inmediato: ni siquiera se buscan los productos
        _repository.Verify(r => r.GetById<Product>(It.IsAny<Guid>(), It.IsAny<string[]>()), Times.Never);
        product.StockQuantity.Should().Be(10);
        VerifyNothingWasPersisted();
    }

    [Fact]
    public async Task CreateOrder_WhenSecondItemHasInsufficientStock_DoesNotAlterAnyProduct()
    {
        // Arrange
        var customer = SetupCustomer();
        var withStock = SetupProduct(stock: 10);
        var withoutStock = SetupProduct(stock: 1);
        var request = CreateRequest(customer.Id, (3, withStock.Id), (2, withoutStock.Id));

        // Act
        var act = () => _service.CreateOrder(request);

        // Assert
        await act.Should().ThrowAsync<InsufficientStockException>();
        withStock.StockQuantity.Should().Be(10);
        withoutStock.StockQuantity.Should().Be(1);
        VerifyNothingWasPersisted();
    }

    [Fact]
    public async Task CreateOrder_WhenSameProductInSeveralItemsExceedsStock_ThrowsInsufficientStockException()
    {
        // Arrange
        var customer = SetupCustomer();
        var product = SetupProduct(stock: 5);
        var request = CreateRequest(customer.Id, (3, product.Id), (3, product.Id));

        // Act
        var act = () => _service.CreateOrder(request);

        // Assert
        await act.Should().ThrowAsync<InsufficientStockException>().WithMessage("*solicitado 6*");
        product.StockQuantity.Should().Be(5);
        VerifyNothingWasPersisted();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CreateOrder_WithNonPositiveQuantity_ThrowsWithoutAlteringStock(int quantity)
    {
        // Arrange
        var customer = SetupCustomer();
        var product = SetupProduct(stock: 10);
        var request = CreateRequest(customer.Id, (quantity, product.Id));

        // Act
        var act = () => _service.CreateOrder(request);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
        product.StockQuantity.Should().Be(10);
        VerifyNothingWasPersisted();
    }

    // ---------------------------------------------------------------
    // Listado y estado de órdenes
    // ---------------------------------------------------------------

    private void SetupOrders(params Order[] orders) =>
        _repository
            .Setup(r => r.GetFiltered(It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<string[]>()))
            .ReturnsAsync((Expression<Func<Order, bool>> predicate, string[] _) =>
                orders.Where(predicate.Compile()).ToList());

    [Fact]
    public async Task GetAllOrders_WithoutOrders_ReturnsEmptyPage()
    {
        // Arrange
        SetupOrders();

        // Act
        var result = await _service.GetAllOrders(new OrderModel.SearchOrder(null, null));

        // Assert
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllOrders_ExcludesCancelledOrders()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var pending = new Order("Envío", "Facturación", null, customerId);
        var cancelled = new Order("Envío", "Facturación", null, customerId) { Status = OrderStatus.CANCELLED };
        SetupOrders(pending, cancelled);

        // Act
        var result = await _service.GetAllOrders(new OrderModel.SearchOrder(null, null));

        // Assert
        result!.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Id.Should().Be(pending.Id);
    }

    [Fact]
    public async Task UpdateOrderStatus_ToCancelled_ReturnsStockToProducts()
    {
        // Arrange
        var product = SetupProduct(stock: 7);
        var order = new Order("Envío", "Facturación", null, Guid.NewGuid());
        order.OrderItems.Add(new OrderItem(3, product.CurrentUnitPrice, order.Id, product.Id));
        _repository.Setup(r => r.GetById<Order>(order.Id, It.IsAny<string[]>())).ReturnsAsync(order);
        _repository.Setup(r => r.Update(It.IsAny<Order>())).ReturnsAsync((Order o) => o);

        // Act
        var result = await _service.UpdateOrderStatus(order.Id, "cancelled");

        // Assert
        result.Status.Should().Be(OrderStatus.CANCELLED);
        product.StockQuantity.Should().Be(10);
        _repository.Verify(r => r.Update(It.Is<Product>(p => p.Id == product.Id && p.StockQuantity == 10)), Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithNumericStatus_ThrowsArgumentException()
    {
        // Arrange
        var order = new Order("Envío", "Facturación", null, Guid.NewGuid());
        _repository.Setup(r => r.GetById<Order>(order.Id, It.IsAny<string[]>())).ReturnsAsync(order);

        // Act
        var act = () => _service.UpdateOrderStatus(order.Id, "2");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        _repository.Verify(r => r.Update(It.IsAny<Order>()), Times.Never);
    }
}
