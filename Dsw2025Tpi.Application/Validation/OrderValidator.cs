using Dsw2025Tpi.Application.Dtos;
using System;

namespace Dsw2025Tpi.Application.Validation
{
    public static class OrderValidator
    {
        public static void Validate(OrderModel.RequestOrderModel request)
        {
            if (request == null)
                throw new InvalidOperationException("La orden no puede ser vacia.");

            if (request.CustomerId == Guid.Empty)
                throw new InvalidOperationException("El ID cliente es obligatorio.");

            if (string.IsNullOrWhiteSpace(request.ShippingAddress) || request.ShippingAddress.Length > 256)
                throw new InvalidOperationException("La direccion de compra es requerida.");

            if (string.IsNullOrWhiteSpace(request.BillingAddress) || request.BillingAddress.Length > 256)
                throw new InvalidOperationException("La direccion de envio es requerida.");

            if (request.OrderItems == null || request.OrderItems.Count == 0)
                throw new InvalidOperationException("Se debe incluir almenos un item en la orden.");
        }
    }
}
