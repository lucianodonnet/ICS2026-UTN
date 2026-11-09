using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Validation;

namespace Dsw2025Tpi.Tests.Validation;

public class OrderValidatorTests
{
    private static OrderModel.RequestOrderModel ValidRequest() =>
        new("Av. Siempre Viva 742", "Av. Siempre Viva 742", null, Guid.NewGuid(),
            [new OrderItemModel.RequestOrderItemModel(2, Guid.NewGuid())]);

    [Fact]
    public void Validate_RequestValido_NoLanzaExcepcion()
    {
        var exception = Record.Exception(() => OrderValidator.Validate(ValidRequest()));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_SinCliente_LanzaInvalidOperationException()
    {
        var request = ValidRequest() with { CustomerId = Guid.Empty };

        Assert.Throws<InvalidOperationException>(() => OrderValidator.Validate(request));
    }

    [Fact]
    public void Validate_SinItems_LanzaInvalidOperationException()
    {
        var request = ValidRequest() with { OrderItems = [] };

        Assert.Throws<InvalidOperationException>(() => OrderValidator.Validate(request));
    }

    [Fact]
    public void Validate_DireccionDeEnvioMayorA256_LanzaInvalidOperationException()
    {
        var request = ValidRequest() with { ShippingAddress = new string('a', 257) };

        Assert.Throws<InvalidOperationException>(() => OrderValidator.Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ValidateItem_CantidadNoPositiva_LanzaInvalidOperationException(int quantity)
    {
        var item = new OrderItemModel.RequestOrderItemModel(quantity, Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => OrderItemValidator.Validate(item));
    }

    [Fact]
    public void ValidateItem_SinProducto_LanzaInvalidOperationException()
    {
        var item = new OrderItemModel.RequestOrderItemModel(1, Guid.Empty);

        Assert.Throws<InvalidOperationException>(() => OrderItemValidator.Validate(item));
    }
}
