using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dsw2025Tpi.Domain.Entities;

public class OrderItem : EntityBase
{
    public OrderItem(int quantity, decimal unitPrice, Guid orderId, Guid productId)
    {
        ProductId = productId;
        OrderId = orderId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }


    private int _quantity;
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException("La cantidad tiene que ser mayor a 0");
            }
            _quantity = value;
        }
    }


    private decimal _unitPrice;

    public decimal UnitPrice
    {
        get => _unitPrice;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException("El precio unitario debe ser mayor a 0");
            }
            _unitPrice = value;
        }
    }

    public decimal Subtotal => Quantity * UnitPrice;

    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

}

