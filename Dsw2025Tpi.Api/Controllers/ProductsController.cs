using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ApplicationException = Dsw2025Tpi.Application.Exceptions.ApplicationException;

namespace Dsw2025Tpi.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly ProductsManagementService _service;

    public ProductsController(ProductsManagementService service)
    {
        _service = service;
    }

    /* [HttpGet()] codigo anterior donde muestra todos los productos sin paginacion
    [AllowAnonymous]
    public async Task<IActionResult> GetAllProducts()
    {
        var products = await _service.GetAllProducts();
        if (products == null || !products.Any()) return NoContent();
        return Ok(products);
    } */

    [HttpGet()]
    [AllowAnonymous]
    public async Task<IActionResult> GetProducts([FromQuery] ProductModel.FilterProduct filter)
    {
        // Ahora le pasamos 'filter' al paréntesis
        var result = await _service.GetProducts(filter);
        // 2. Validamos si trajo algo
        if (result == null) return NoContent();
        // 3. Devolvemos el resultado (que ya incluye la lista y el total)
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> GetProductById(Guid id)
    {
        try
        {
            
            var product = await _service.GetProductById(id);
            
            return Ok(product);
        }
        catch (EntityNotFoundException ioe)
        {
            
            return NotFound(ioe.Message);
        }
    }

    [HttpPost()]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddProduct([FromBody] ProductModel.RequestProductModel request)
    {
        try
        {
            var product = await _service.AddProduct(request);
            return Ok(product);
        }
        catch (ArgumentException ae)
        {
            return BadRequest(ae.Message);
        }
        catch (ApplicationException ioe)
        {
            return BadRequest(ioe.Message);
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductModel.RequestProductModel request)
    {
        try
        {
            
            var product = await _service.UpdateProduct(id, request);
            
            return Ok(product);
        }
        catch (EntityNotFoundException ioe)
        {
            
            return NotFound(ioe.Message);
        }
        catch (ArgumentException ae)
        {
            
            return BadRequest(ae.Message);
        }
        catch (ApplicationException ioe)
        {
            return BadRequest(ioe.Message);
        }
    }

    [HttpPatch("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PatchProduct(Guid id)
    {
        try
        {
            
            await _service.PatchProduct(id);
            return NoContent();
        }
        catch (ApplicationException ioe)
        {
            
            return NotFound(ioe.Message);
        }
        catch (ArgumentException ae)
        {
            
            return BadRequest(ae.Message);
        }
    }

    /*dashboard!!!*/

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAuthProducts([FromQuery] ProductModel.FilterProduct request)
    {
        var products = await _service.GetProducts(request);
        if (products == null)
        {
            Response.Headers.Append("X-Message", "There are no active products");
            return NoContent();
        }
        return Ok(products);
    }
}
