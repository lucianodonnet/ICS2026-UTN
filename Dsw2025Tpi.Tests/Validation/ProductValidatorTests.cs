using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Validation;
using AppException = Dsw2025Tpi.Application.Exceptions.ApplicationException;

namespace Dsw2025Tpi.Tests.Validation;

public class ProductValidatorTests
{
    private static ProductModel.RequestProductModel ValidRequest() =>
        new("SKU-0001", "INT-001", "Producto", "Descripción", 100m, 10);

    [Fact]
    public void Validate_RequestValido_NoLanzaExcepcion()
    {
        var exception = Record.Exception(() => ProductValidator.Validate(ValidRequest()));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_SkuVacio_LanzaApplicationException(string sku)
    {
        var request = ValidRequest() with { Sku = sku };

        Assert.Throws<AppException>(() => ProductValidator.Validate(request));
    }

    [Fact]
    public void Validate_NombreVacio_LanzaApplicationException()
    {
        var request = ValidRequest() with { Name = "" };

        Assert.Throws<AppException>(() => ProductValidator.Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PrecioNoPositivo_LanzaApplicationException(decimal price)
    {
        var request = ValidRequest() with { CurrentUnitPrice = price };

        Assert.Throws<AppException>(() => ProductValidator.Validate(request));
    }

    [Fact]
    public void Validate_StockNegativo_LanzaArgumentException()
    {
        var request = ValidRequest() with { StockQuantity = -1 };

        Assert.Throws<ArgumentException>(() => ProductValidator.Validate(request));
    }

    [Fact]
    public void Validate_StockCero_EsValido()
    {
        var request = ValidRequest() with { StockQuantity = 0 };

        var exception = Record.Exception(() => ProductValidator.Validate(request));

        Assert.Null(exception);
    }
}
