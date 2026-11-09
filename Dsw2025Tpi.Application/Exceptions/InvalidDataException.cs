namespace Dsw2025Tpi.Application.Exceptions;

// Datos de entrada que no cumplen las reglas de negocio (por ejemplo, precio o stock no válidos).
// Hereda de ApplicationException para que los controladores la sigan respondiendo con 400.
public class InvalidDataException : ApplicationException
{
    public InvalidDataException(string message) : base(message)
    {
    }
}
