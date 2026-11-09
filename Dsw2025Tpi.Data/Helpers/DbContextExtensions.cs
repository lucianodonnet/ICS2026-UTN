using Dsw2025Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Dsw2025Tpi.Data.Helpers;

public static class DbContextExtensions
{
    private static readonly JsonSerializerOptions SeedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { IncludeEntityId }
        }
    };

    public static void Seedwork<T>(this Dsw2025TpiContext context, string? dataSource) where T : class
    {
        // Si ya hay datos, no hacer nada
        if (context.Set<T>().Any()) return;

        // Si no se pasa dataSource o está vacío, salir
        if (string.IsNullOrWhiteSpace(dataSource)) return;

        var path = Path.Combine(AppContext.BaseDirectory, dataSource);

        // Si el archivo no existe, avisar y salir
        if (!File.Exists(path))
        {
            Console.WriteLine($"Advertencia: no se encontró el archivo de seed {path}. No se cargan datos de {typeof(T).Name}.");
            return;
        }

        var json = File.ReadAllText(path);
        var entities = JsonSerializer.Deserialize<List<T>>(json, SeedJsonOptions);

        if (entities == null || entities.Count == 0) return;

        // Caso especial para Orders
        if (typeof(T) == typeof(Order))
        {
            SeedOrders(context, entities.Cast<Order>());
            return;
        }

        // Para otras entidades
        context.Set<T>().AddRange(entities);
        context.SaveChanges();
    }

    // Cada orden se recrea con su constructor y sus ítems se vinculan a la orden nueva,
    // tomando el precio actual de cada producto.
    private static void SeedOrders(Dsw2025TpiContext context, IEnumerable<Order> orders)
    {
        foreach (var order in orders)
        {
            if (!context.Customers.Any(c => c.Id == order.CustomerId))
            {
                Console.WriteLine($"Advertencia: no existe el cliente {order.CustomerId}. Se omite la orden.");
                continue;
            }

            var orderToAdd = new Order(order.ShippingAddress, order.BillingAddress, order.Notes, order.CustomerId)
            {
                Date = order.Date,
                Status = order.Status
            };

            foreach (var item in order.OrderItems ?? [])
            {
                var product = context.Products.Find(item.ProductId);
                if (product == null)
                {
                    Console.WriteLine($"Advertencia: no existe el producto {item.ProductId}. Se omite el ítem.");
                    continue;
                }

                orderToAdd.OrderItems.Add(new OrderItem(item.Quantity, product.CurrentUnitPrice, orderToAdd.Id, product.Id));
            }

            // Una orden sin ítems no se guarda: si se guardara, la tabla dejaría de estar vacía
            // y el seed no volvería a cargar las órdenes cuando los productos estén correctos.
            if (orderToAdd.OrderItems.Count == 0)
            {
                Console.WriteLine($"Advertencia: la orden del cliente {order.CustomerId} no tiene ítems válidos. Se omite.");
                continue;
            }

            context.Orders.Add(orderToAdd);
        }

        context.SaveChanges();
    }

    // EntityBase.Id tiene setter protegido, así que el serializador ignoraba el "id" de los JSON
    // y cada entidad recibía un Guid nuevo: las órdenes del seed no encontraban sus productos.
    private static void IncludeEntityId(JsonTypeInfo typeInfo)
    {
        if (!typeof(EntityBase).IsAssignableFrom(typeInfo.Type)) return;

        var idProperty = typeInfo.Properties.FirstOrDefault(p => p.Name == nameof(EntityBase.Id));
        if (idProperty == null || idProperty.Set != null) return;

        var idSetter = typeof(EntityBase).GetProperty(nameof(EntityBase.Id))!.GetSetMethod(nonPublic: true)!;
        idProperty.Set = (entity, value) => idSetter.Invoke(entity, [value]);
    }
}
