using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dsw2025Tpi.Application.Dtos;

public record CustomerModel
{
    public record RequestCustomer(string? Email, string? Name, string? PhoneNumber);

    public record ResponseCustomer(Guid Id, string? Email, string? Name, string? PhoneNumber);
}
