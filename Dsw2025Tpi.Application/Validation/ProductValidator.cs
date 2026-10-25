using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using System;

namespace Dsw2025Tpi.Application.Validation
{
    public static class ProductValidator
    {
        public static void Validate(ProductModel.RequestProductModel request)
        {
            if (string.IsNullOrWhiteSpace(request.Sku))
                throw new Exceptions.ApplicationException("SKU es requerido");

            if (string.IsNullOrWhiteSpace(request.InternalCode))
                throw new Exceptions.ApplicationException("El codigo interno es obligatorio.");

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new Exceptions.ApplicationException("Nombre es requerido.");

            if (string.IsNullOrWhiteSpace(request.Description))
                throw new Exceptions.ApplicationException("La descripcion es requerida.");

            if (request.CurrentUnitPrice <= 0)
                throw new Exceptions.ApplicationException("El precio debe ser un valor positivo.");

            if (request.StockQuantity < 0)
                throw new ArgumentException("La cantidad de stock debe ser mayor o igual a 0");
        }
    }

}