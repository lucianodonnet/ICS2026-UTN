using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using System;

namespace Dsw2025Tpi.Application.Validation
{
    public static class ProductValidator
    {
        // isNewProduct: al crear un producto el stock inicial debe ser mayor a 0;
        // al modificarlo puede quedar en 0 (producto agotado).
        public static void Validate(ProductModel.RequestProductModel request, bool isNewProduct = false)
        {
            if (string.IsNullOrWhiteSpace(request.Sku))
                throw new Exceptions.InvalidDataException("SKU es requerido");

            if (string.IsNullOrWhiteSpace(request.InternalCode))
                throw new Exceptions.InvalidDataException("El codigo interno es obligatorio.");

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new Exceptions.InvalidDataException("Nombre es requerido.");

            if (string.IsNullOrWhiteSpace(request.Description))
                throw new Exceptions.InvalidDataException("La descripcion es requerida.");

            if (request.CurrentUnitPrice <= 0)
                throw new Exceptions.InvalidDataException("El precio debe ser un valor positivo.");

            if (isNewProduct && request.StockQuantity <= 0)
                throw new Exceptions.InvalidDataException("La cantidad de stock inicial debe ser mayor a 0.");

            if (request.StockQuantity < 0)
                throw new Exceptions.InvalidDataException("La cantidad de stock debe ser mayor o igual a 0.");
        }
    }

}
