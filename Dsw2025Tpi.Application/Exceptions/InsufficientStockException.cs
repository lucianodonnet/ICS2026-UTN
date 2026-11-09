namespace Dsw2025Tpi.Application.Exceptions;

// Se pide más cantidad de un producto que la disponible en stock.
// Hereda de InvalidOperationException para que OrdersController la siga respondiendo con 400.
public class InsufficientStockException : InvalidOperationException
{
    public InsufficientStockException(string productName, int available, int requested)
        : base($"Stock insuficiente para {productName}: disponible {available}, solicitado {requested}.")
    {
    }
}
