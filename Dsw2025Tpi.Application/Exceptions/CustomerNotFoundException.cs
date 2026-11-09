namespace Dsw2025Tpi.Application.Exceptions;

public class CustomerNotFoundException : EntityNotFoundException
{
    public CustomerNotFoundException(Guid customerId)
        : base($"Customer con ID {customerId} not found.")
    {
    }
}
