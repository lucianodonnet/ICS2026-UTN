using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;   // <--- Importación faltante
using Dsw2025Tpi.Domain.Interfaces; // <--- Importación faltante
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Dsw2025Tpi.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthenticateController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IRepository _repository; // <--- Necesario para guardar el Customer

    public AuthenticateController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        JwtTokenService jwtTokenService,
        IRepository repository) // <--- Inyectamos el repositorio
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _repository = repository;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginModel request)
    {
        var user = await _userManager.FindByNameAsync(request.Username);

        // 1. CORRECCIÓN: Retornar 401 si no existe usuario
        if (user == null)
        {
            return Unauthorized("Usuario o contraseña incorrecta");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);

        // 2. CORRECCIÓN: Retornar 401 si la contraseña está mal
        if (!result.Succeeded)
        {
            return Unauthorized("Usuario o contraseña incorrecta");
        }

        var roles = await _userManager.GetRolesAsync(user);

        // 3. CORRECCIÓN: Manejo seguro del rol (Error 500 si falla la config)
        var role = roles.FirstOrDefault();
        if (role == null)
        {
            return StatusCode(500, "El usuario no tiene asignado un rol.");
        }

        // Generamos token incluyendo el ID para que el frontend lo use
        var token = _jwtTokenService.GenerateToken(request.Username, role, user.Id);
        return Ok(new { token });
    }

    [HttpPost("register")]
    [AllowAnonymous] // Permitimos registro público
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        // 1. Validar duplicados
        var userExists = await _userManager.FindByNameAsync(model.Username);
        if (userExists != null)
        {
            return BadRequest("El usuario ya existe."); // Código 400
        }

        var user = new IdentityUser { UserName = model.Username, Email = model.Email };

        // 2. Crear en Identity (Tabla AspNetUsers)
        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors); // Código 400
        }

        // 3. Asignar Rol
        try
        {
            // Forzamos rol "Client" para seguridad (o "User" si es el que tenés en BD)
            await _userManager.AddToRoleAsync(user, "User");
        }
        catch (Exception ex)
        {
            // Si falla el rol, borramos el usuario para no dejar basura
            await _userManager.DeleteAsync(user);
            return StatusCode(500, $"Error interno al asignar rol: {ex.Message}");
        }

        // 4. Crear el Customer espejo (Tabla Customers)
        try
        {
            // CORRECCIÓN: Usamos model.Name en lugar de model.Username
            // Si model.Name viene vacío, usamos el Username como fallback
            string customerName = !string.IsNullOrWhiteSpace(model.Name) ? model.Name : model.Username;

            var customer = new Customer(
                Guid.Parse(user.Id),
                customerName, // <--- AQUI USAMOS EL NOMBRE REAL
                model.Email
            );

            await _repository.Add(customer);
        }
        catch (Exception ex)
        {
            // Si falla crear el Customer, borramos el usuario de Identity
            // para mantener la consistencia de datos.
            await _userManager.DeleteAsync(user);
            return StatusCode(500, $"Error al crear el perfil del cliente: {ex.Message}");
        }

        return Ok("Usuario registrado exitosamente.");
    }
}