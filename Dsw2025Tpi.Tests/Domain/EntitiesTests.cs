using Dsw2025Tpi.Domain.Entities;

namespace Dsw2025Tpi.Tests.Domain;

public class EntitiesTests
{
    [Fact]
    public void OrderItem_Subtotal_EsCantidadPorPrecioUnitario()
    {
        var item = new OrderItem(3, 10.5m, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(31.5m, item.Subtotal);
    }

    [Fact]
    public void OrderItem_CantidadCero_LanzaArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderItem(0, 10m, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void Order_Nueva_QuedaPendienteYSinItems()
    {
        var order = new Order("Envío", "Facturación", null, Guid.NewGuid());

        Assert.Equal(OrderStatus.PENDING, order.Status);
        Assert.Empty(order.OrderItems);
        Assert.Equal(0m, order.TotalAmount);
    }

    [Fact]
    public void Order_TotalAmount_SumaLosSubtotales()
    {
        var order = new Order("Envío", "Facturación", null, Guid.NewGuid());
        order.OrderItems.Add(new OrderItem(2, 100m, order.Id, Guid.NewGuid()));
        order.OrderItems.Add(new OrderItem(1, 50.25m, order.Id, Guid.NewGuid()));

        Assert.Equal(250.25m, order.TotalAmount);
    }

    [Fact]
    public void Product_Nuevo_QuedaActivo()
    {
        var product = new Product("SKU-1", "INT-1", "Producto", "Descripción", 10m, 5);

        Assert.True(product.IsActive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Product_PrecioNoPositivo_LanzaArgumentException(decimal price)
    {
        Assert.Throws<ArgumentException>(() => new Product("SKU-1", "INT-1", "Producto", "Descripción", price, 5));
    }

    [Fact]
    public void Product_StockNegativo_LanzaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Product("SKU-1", "INT-1", "Producto", "Descripción", 10m, -1));
    }
}
