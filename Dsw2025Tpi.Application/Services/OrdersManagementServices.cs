using Azure.Core;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Validation;
using Dsw2025Tpi.Data.Repositories;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dsw2025Tpi.Application.Services
{
    public class OrdersManagementService
    {
        private readonly IRepository _repository;


        public OrdersManagementService(IRepository repository)
        {
            _repository = repository;
        }
        public async Task<OrderModel.ResponseOrderModel?> GetOrderById(Guid id)
        {
            var order = await _repository.GetById<Order>(id, nameof(Order.OrderItems), "OrderItems.Product");
            if (order == null) throw new EntityNotFoundException($"Order not found");

            var responseItems = order.OrderItems.Select(i => new OrderItemModel.ResponseOrderItemModel(
                    i.Id,
                    i.Quantity,
                    i.UnitPrice,
                    i.OrderId,
                    i.ProductId,
                    i.Subtotal
                )).ToList();

            return order != null ?
                new OrderModel.ResponseOrderModel(order.Id, order.Date, order.ShippingAddress, order.BillingAddress, order.Notes, order.CustomerId, order.Customer?.Name, order.Status, order.TotalAmount, responseItems) :
                null;
        }

        public async Task<OrderModel.ResponsePagination?> GetAllOrders(OrderModel.SearchOrder request)
        {
            // 1. Parseo de Estado (Igual que tenías)
            OrderStatus? status = null;
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                if (!Enum.TryParse<OrderStatus>(request.Status, true, out var parsedStatus))
                {
                    // Opción: Ignorar filtro inválido o lanzar error. Aquí lanzamos.
                    throw new ArgumentException($"Estado de orden invalido: {request.Status}");
                }
                status = parsedStatus;
            }

            // 2. Validación de Cliente
            if (request.CustomerId.HasValue)
            {
                var customer = await _repository.GetById<Customer>(request.CustomerId.Value);
                if (customer == null)
                    throw new EntityNotFoundException($"Customer con el ID {request.CustomerId} not found.");
            }

            // 3. Obtener datos filtrados
            var orders = await _repository.GetFiltered<Order>(
                o =>
                    o.Status != OrderStatus.CANCELLED &&
                    (!request.CustomerId.HasValue || o.CustomerId == request.CustomerId.Value) &&
                    (!status.HasValue || o.Status == status.Value),
                include: new[] { "OrderItems.Product", "Customer" }
            );

            // 4. Calcular Total (Para la paginación)
            var totalCount = orders.Count();

            // 5. Validar y Corregir Paginación
            int pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
            int pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

            // 6. Paginar y Mapear
            var paginatedItems = orders
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(order => new OrderModel.ResponseOrderModel(
                    order.Id,
                    order.Date,
                    order.ShippingAddress,
                    order.BillingAddress,
                    order.Notes,
                    order.CustomerId,
                    order.Customer?.Name,
                    order.Status,
                    order.TotalAmount,
                    order.OrderItems.Select(i => new OrderItemModel.ResponseOrderItemModel(
                        i.Id,
                        i.Quantity,
                        i.UnitPrice,
                        i.OrderId,
                        i.ProductId,
                        i.Subtotal
                    )).ToList()
                ))
                .ToList();

            // 7. Devolver Objeto Paginado
            return new OrderModel.ResponsePagination(paginatedItems, totalCount);
        }

        public async Task<OrderModel.ResponseOrderModel> AddOrder(OrderModel.RequestOrderModel request)
        {
            OrderValidator.Validate(request);

            if (request.OrderItems == null || !request.OrderItems.Any())
                throw new ArgumentException("La orden tiene que tener mas de un item.");

            var customer = await _repository.GetById<Customer>(request.CustomerId);
            if (customer == null)
                throw new EntityNotFoundException($"Customer con ID {request.CustomerId} not found.");

            var order = new Order(
                request.ShippingAddress,
                request.BillingAddress,
                request.Notes,
                request.CustomerId
            );

            var orderItems = new List<OrderItem>();

            foreach (var item in request.OrderItems)
            {
                var product = await _repository.GetById<Product>(item.ProductId)
                    ?? throw new EntityNotFoundException($"Producto not found: {item.ProductId}");

                if (product.StockQuantity < item.Quantity)
                    throw new InvalidOperationException($"Stock insuficiente: {product.Name}");

                product.StockQuantity -= item.Quantity;
                await _repository.Update(product);

                var orderItem = new OrderItem(
                    item.Quantity,
                    product.CurrentUnitPrice,
                    order.Id,
                    product.Id
                );
                orderItems.Add(orderItem);



            }

            order.OrderItems = orderItems;
            await _repository.Add(order);

            var responseItems = orderItems.Select(oi => new OrderItemModel.ResponseOrderItemModel(
                oi.Id,
                oi.Quantity,
                oi.UnitPrice,
                oi.OrderId,
                oi.ProductId,
                oi.Subtotal
            )).ToList();

            return new OrderModel.ResponseOrderModel(
                order.Id,
                order.Date,
                order.ShippingAddress,
                order.BillingAddress,
                order.Notes,
                order.CustomerId,
                order.Customer?.Name,
                order.Status,
                order.TotalAmount,
                responseItems
            );
        }

        public async Task<OrderModel.ResponseOrderModel> UpdateOrderStatus(Guid id, string newStatus)
        {
            var order = await _repository.GetById<Order>(id, nameof(Order.OrderItems), "OrderItems.Product");

            if (order == null)
                throw new EntityNotFoundException($"Order con el ID: {id} not found");

            if (!Enum.TryParse<OrderStatus>(newStatus, true, out var status) || int.TryParse(newStatus, out _))
                throw new ArgumentException("El estado no es valido");

            if (status == OrderStatus.CANCELLED)
            {
                foreach (var item in order.OrderItems)
                {
                    var product = await _repository.GetById<Product>(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                        await _repository.Update(product);
                    }
                }
            }

            order.Status = status;

            await _repository.Update(order);

            var responseItems = order.OrderItems.Select(oi => new OrderItemModel.ResponseOrderItemModel(
            oi.Id,
            oi.Quantity,
            oi.UnitPrice,
            oi.OrderId,
            oi.ProductId,
            oi.Subtotal
            )).ToList();

            return new OrderModel.ResponseOrderModel
           (
                order.Id,
                order.Date,
                order.ShippingAddress,
                order.BillingAddress,
                order.Notes,
                order.CustomerId,
                order.Customer?.Name,
                order.Status,
                order.TotalAmount,
                responseItems
            );
        }
    }
}

