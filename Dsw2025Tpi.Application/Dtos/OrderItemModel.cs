using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dsw2025Tpi.Application.Dtos;

public record OrderItemModel
{
    public record RequestOrderItemModel(int Quantity, Guid ProductId);

    public record ResponseOrderItemModel(Guid Id, int Quantity, decimal UnitPrice, Guid OrderId, Guid ProductId, decimal SubTotal);
}
