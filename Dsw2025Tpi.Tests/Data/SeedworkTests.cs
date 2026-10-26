using System.Text.Json;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Data.Helpers;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dsw2025Tpi.Tests.Data;

// Ejecuta el seed real (los mismos JSON que usa la API) sobre una base en memoria.
public class SeedworkTests
{
    private static readonly string SourcesDirectory = Path.Combine(AppContext.BaseDirectory, "Sources");

    private static Dsw2025TpiContext CreateContext() =>
        new(new DbContextOptionsBuilder<Dsw2025TpiContext>()
            .UseInMemoryDatabase($"seed-{Guid.NewGuid()}")
            .Options);

    private static void SeedAll(Dsw2025TpiContext context)
    {
        context.Seedwork<Product>("Sources/Products.json");
        context.Seedwork<Customer>("Sources/Customers.json");
        context.Seedwork<Order>("Sources/Orders.json");
    }

    private static JsonElement ReadJson(string fileName) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(SourcesDirectory, fileName))).RootElement;

    [Fact]
    public void Seedwork_Products_ConservaLosIdsDelJson()
    {
        using var context = CreateContext();

        context.Seedwork<Product>("Sources/Products.json");

        var expectedIds = ReadJson("Products.json").EnumerateArray()
            .Select(p => p.GetProperty("id").GetGuid())
            .OrderBy(id => id);
        var seededIds = context.Products.Select(p => p.Id).AsEnumerable().OrderBy(id => id);
        Assert.Equal(expectedIds, seededIds);
    }

    [Fact]
    public void Seedwork_Orders_SeCarganConSusItemsVinculadosYTotal()
    {
        using var context = CreateContext();

        SeedAll(context);

        var orders = context.Orders.Include(o => o.OrderItems).ToList();
        var productIds = context.Products.Select(p => p.Id).ToHashSet();
        var expectedItems = ReadJson("Orders.json").EnumerateArray()
            .Sum(o => o.GetProperty("orderItems").GetArrayLength());

        Assert.Equal(ReadJson("Orders.json").GetArrayLength(), orders.Count);
        Assert.Equal(expectedItems, orders.Sum(o => o.OrderItems.Count));
        Assert.All(orders, order =>
        {
            Assert.NotEmpty(order.OrderItems);
            Assert.True(order.TotalAmount > 0);
            Assert.All(order.OrderItems, item =>
            {
                Assert.Equal(order.Id, item.OrderId);
                Assert.True(productIds.Contains(item.ProductId), $"Ítem con producto inexistente: {item.ProductId}");
            });
        });
    }

    [Fact]
    public void Seedwork_Customers_ConservanSuTelefono()
    {
        using var context = CreateContext();

        context.Seedwork<Customer>("Sources/Customers.json");

        Assert.NotEmpty(context.Customers);
        Assert.All(context.Customers, c => Assert.NotEqual("N/A", c.PhoneNumber));
    }

    [Fact]
    public void Seedwork_OrdersConProductosInexistentes_NoGuardaOrdenesVacias()
    {
        using var context = CreateContext();
        // Simula una base sembrada con la versión anterior: productos con Ids que no son los del JSON
        context.Products.Add(new Product("SKU-VIEJO", "INT-VIEJO", "Producto viejo", "Descripción", 10m, 5));
        context.SaveChanges();

        context.Seedwork<Product>("Sources/Products.json");
        context.Seedwork<Customer>("Sources/Customers.json");
        context.Seedwork<Order>("Sources/Orders.json");

        Assert.Empty(context.Orders);
    }

    [Fact]
    public void Seedwork_EjecutadoDosVeces_NoDuplicaDatos()
    {
        using var context = CreateContext();

        SeedAll(context);
        SeedAll(context);

        Assert.Equal(ReadJson("Products.json").GetArrayLength(), context.Products.Count());
        Assert.Equal(ReadJson("Customers.json").GetArrayLength(), context.Customers.Count());
        Assert.Equal(ReadJson("Orders.json").GetArrayLength(), context.Orders.Count());
    }

    [Fact]
    public void Seedwork_ArchivoInexistente_NoCargaNada()
    {
        using var context = CreateContext();

        context.Seedwork<Product>("Sources/NoExiste.json");

        Assert.Empty(context.Products);
    }
}
