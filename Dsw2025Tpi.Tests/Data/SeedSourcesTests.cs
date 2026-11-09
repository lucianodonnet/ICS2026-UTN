using System.Text.Json;

namespace Dsw2025Tpi.Tests.Data;

// Los archivos de seed se leen por nombre al arrancar la API (Program.cs).
// En Linux el sistema de archivos distingue mayúsculas, así que el nombre debe ser exacto.
public class SeedSourcesTests
{
    private static readonly string SourcesDirectory = Path.Combine(AppContext.BaseDirectory, "Sources");

    [Theory]
    [InlineData("Products.json")]
    [InlineData("Customers.json")]
    [InlineData("Orders.json")]
    public void ArchivoDeSeed_SeCopiaALaSalidaConElNombreExacto(string fileName)
    {
        var copiedFiles = Directory.GetFiles(SourcesDirectory).Select(f => Path.GetFileName(f));

        Assert.Contains(fileName, copiedFiles);
    }

    [Fact]
    public void Orders_ReferencianClientesYProductosExistentes()
    {
        var customerIds = ReadIds("Customers.json", "id");
        var productIds = ReadIds("Products.json", "id");

        using var orders = JsonDocument.Parse(File.ReadAllText(Path.Combine(SourcesDirectory, "Orders.json")));

        foreach (var order in orders.RootElement.EnumerateArray())
        {
            var customerId = order.GetProperty("customerId").GetString() ?? "";
            Assert.True(customerIds.Contains(customerId), $"La orden referencia un cliente inexistente: {customerId}");

            foreach (var item in order.GetProperty("orderItems").EnumerateArray())
            {
                var productId = item.GetProperty("productId").GetString() ?? "";
                Assert.True(productIds.Contains(productId), $"La orden referencia un producto inexistente: {productId}");
            }
        }
    }

    private static HashSet<string> ReadIds(string fileName, string property)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(SourcesDirectory, fileName)));

        return document.RootElement.EnumerateArray()
            .Select(e => e.GetProperty(property).GetString() ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
