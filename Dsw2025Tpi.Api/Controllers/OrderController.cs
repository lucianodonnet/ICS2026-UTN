using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dsw2025Tpi.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly OrdersManagementService _service;

    public OrdersController(OrdersManagementService service)
    {
        _service = service;
    }

    [HttpGet()]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllOrders([FromQuery] OrderModel.SearchOrder request)
    {
        var result = await _service.GetAllOrders(request);

        // Si el servicio devuelve null o la lista de items está vacía
        if (result == null || result.Items == null || !result.Items.Any())
            return NoContent();

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin, User")]
    public async Task<IActionResult> AddOrder([FromBody] OrderModel.RequestOrderModel request)
    {
        try
        {
            
            var Order = await _service.CreateOrder(request);
            
            return CreatedAtAction(nameof(GetOrderById), new { id = Order.Id }, Order);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or EntityNotFoundException)
        {
            // Error en JSON (ProblemDetails): el motivo, por ejemplo la falta de stock, viaja en "detail"
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "No se pudo crear la orden");
        }
    }


    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        try
        {
            
            var order = await _service.GetOrderById(id);
            
            return Ok(order);
        }
        catch (InvalidOperationException ioe)
        {
            
            return NotFound(ioe.Message);
        }
        catch (EntityNotFoundException ioe)
        {
            
            return NotFound(ioe.Message);
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, string newStatus)
    {
        try
        {
            
            var order = await _service.UpdateOrderStatus(id, newStatus);
            
            if (order == null) return NotFound();
            
            return Ok(order);
        }
        catch (ArgumentException ae)
        {
            
            return BadRequest(ae.Message);
        }
        catch (InvalidOperationException ioe)
        {
            
            return NotFound(ioe.Message);
        }
        catch (EntityNotFoundException ioe)
        {
            
            return NotFound(ioe.Message);
        }
    }
}

