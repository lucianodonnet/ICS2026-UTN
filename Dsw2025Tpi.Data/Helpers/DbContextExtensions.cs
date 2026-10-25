using Dsw2025Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Dsw2025Tpi.Data.Helpers;

public static class DbContextExtensions
{
    public static void Seedwork<T>(this Dsw2025TpiContext context, string? dataSource) where T : class
    {
        // Si ya hay datos, no hacer nada
        if (context.Set<T>().Any()) return;

        // Si no se pasa dataSource o está vacío, salir
        if (string.IsNullOrWhiteSpace(dataSource)) return;

        var path = Path.Combine(AppContext.BaseDirectory, dataSource);

        // Si el archivo no existe, salir
        if (!File.Exists(path)) return;

        var json = File.ReadAllText(path);
        var entities = JsonSerializer.Deserialize<List<T>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (entities == null || entities.Count == 0) return;

        // Caso especial para Orders
        if (typeof(T) == typeof(Order))
        {
            var orders = entities.Cast<Order>().ToList();
            foreach (var order in orders)
            {
                var customerExists = context.Customers.Any(c => c.Id == order.CustomerId);
                if (!customerExists)
                {
                    Console.WriteLine($"Warning: Customer {order.CustomerId} not found for order {order.Id}. Skipping order.");
                    continue;
                }

                var orderToAdd = new Order(order.ShippingAddress, order.BillingAddress, order.Notes, order.CustomerId)
                {
                    Date = order.Date,
                    Status = order.Status
                };

                context.Orders.Add(orderToAdd);
            }
            context.SaveChanges();

            foreach (var order in orders)
            {
                if (order.OrderItems?.Any() == true)
                {
                    foreach (var item in order.OrderItems)
                    {
                        var product = context.Products.Find(item.ProductId);
                        if (product != null)
                        {
                            var orderItem = new OrderItem(item.Quantity, product.CurrentUnitPrice, order.Id, item.ProductId);
                            context.OrderItems.Add(orderItem);
                        }
                    }
                }
            }
            context.SaveChanges();

            // Actualizar precios
            var savedOrders = context.Orders.Include(o => o.OrderItems).ToList();
            foreach (var order in savedOrders)
            {
                foreach (var item in order.OrderItems)
                {
                    var product = context.Products.Find(item.ProductId);
                    if (product != null)
                    {
                        item.UnitPrice = product.CurrentUnitPrice;
                    }
                }
            }
            context.SaveChanges();
        }
        else
        {
            // Para otras entidades
            context.Set<T>().AddRange(entities);
            context.SaveChanges();
        }
    }
}