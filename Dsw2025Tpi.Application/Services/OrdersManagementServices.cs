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
                    throw new CustomerNotFoundException(request.CustomerId.Value);
            }

            // 3. Obtener datos filtrados
            var orders = await _repository.GetFiltered<Order>(
                o =>
                    o.Status != OrderStatus.CANCELLED &&
                    (!request.CustomerId.HasValue || o.CustomerId == request.CustomerId.Value) &&
                    (!status.HasValue || o.Status == status.Value),
                include: new[] { "OrderItems.Product", "Customer" }
            ) ?? Enumerable.Empty<Order>();

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

        public async Task<OrderModel.ResponseOrderModel> CreateOrder(OrderModel.RequestOrderModel request)
        {
            OrderValidator.Validate(request);
            foreach (var item in request.OrderItems)
                OrderItemValidator.Validate(item);

            var customer = await _repository.GetById<Customer>(request.CustomerId)
                ?? throw new CustomerNotFoundException(request.CustomerId);

            // 1. Se valida todo antes de modificar nada: si un ítem no tiene stock suficiente,
            //    ningún producto queda descontado. Si un producto aparece en varias líneas,
            //    se controla la suma de sus cantidades.
            var products = new Dictionary<Guid, Product>();
            foreach (var item in request.OrderItems)
            {
                if (!products.ContainsKey(item.ProductId))
                {
                    products[item.ProductId] = await _repository.GetById<Product>(item.ProductId)
                        ?? throw new EntityNotFoundException($"Producto not found: {item.ProductId}");
                }
            }

            var requestedByProduct = request.OrderItems
                .GroupBy(item => item.ProductId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

            foreach (var (productId, requested) in requestedByProduct)
            {
                var product = products[productId];
                if (product.StockQuantity < requested)
                    throw new InsufficientStockException(product.Name, product.StockQuantity, requested);
            }

            // 2. Con todo validado, se arma la orden y se descuenta el stock
            var order = new Order(
                request.ShippingAddress,
                request.BillingAddress,
                request.Notes,
                request.CustomerId
            );

            var orderItems = request.OrderItems
                .Select(item => new OrderItem(item.Quantity, products[item.ProductId].CurrentUnitPrice, order.Id, item.ProductId))
                .ToList();

            foreach (var (productId, requested) in requestedByProduct)
            {
                var product = products[productId];
                product.StockQuantity -= requested;
                await _repository.Update(product);
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
                customer.Name,
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

