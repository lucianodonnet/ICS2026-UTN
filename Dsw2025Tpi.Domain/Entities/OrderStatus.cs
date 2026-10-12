using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dsw2025Tpi.Domain.Entities;

public enum OrderStatus
{
    PENDING = 1,
    PROCESSING = 2,
    SHIPPED = 3,
    DELIVERED = 4,
    CANCELLED = 5
}
