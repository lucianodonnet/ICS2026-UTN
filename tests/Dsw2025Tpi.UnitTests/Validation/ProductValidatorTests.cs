using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Validation;
using InvalidDataException = Dsw2025Tpi.Application.Exceptions.InvalidDataException;

namespace Dsw2025Tpi.UnitTests.Validation;

public class ProductValidatorTests
{
    private static ProductModel.RequestProductModel ValidRequest() =>
        new("SKU-0001", "INT-001", "Producto", "Descripción", 100m, 10);

    [Fact]
    public void Validate_RequestValido_NoLanzaExcepcion()
    {
        var exception = Record.Exception(() => ProductValidator.Validate(ValidRequest(), isNewProduct: true));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_SkuVacio_LanzaInvalidDataException(string sku)
    {
        var request = ValidRequest() with { Sku = sku };

        Assert.Throws<InvalidDataException>(() => ProductValidator.Validate(request));
    }

    [Fact]
    public void Validate_NombreVacio_LanzaInvalidDataException()
    {
        var request = ValidRequest() with { Name = "" };

        Assert.Throws<InvalidDataException>(() => ProductValidator.Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PrecioNoPositivo_LanzaInvalidDataException(decimal price)
    {
        var request = ValidRequest() with { CurrentUnitPrice = price };

        Assert.Throws<InvalidDataException>(() => ProductValidator.Validate(request));
    }

    [Fact]
    public void Validate_StockNegativo_LanzaInvalidDataException()
    {
        var request = ValidRequest() with { StockQuantity = -1 };

        Assert.Throws<InvalidDataException>(() => ProductValidator.Validate(request));
    }

    [Fact]
    public void Validate_StockCeroAlCrear_LanzaInvalidDataException()
    {
        var request = ValidRequest() with { StockQuantity = 0 };

        Assert.Throws<InvalidDataException>(() => ProductValidator.Validate(request, isNewProduct: true));
    }

    [Fact]
    public void Validate_StockCeroAlModificar_EsValido()
    {
        // Un producto existente puede quedar agotado
        var request = ValidRequest() with { StockQuantity = 0 };

        var exception = Record.Exception(() => ProductValidator.Validate(request, isNewProduct: false));

        Assert.Null(exception);
    }
}
