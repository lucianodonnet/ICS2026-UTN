using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dsw2025Tpi.Api;
using Dsw2025Tpi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dsw2025Tpi.IntegrationTests.Infrastructure;

// Levanta la API completa en memoria (entorno "Testing") y reemplaza SQL Server por SQLite
// en memoria compartida. Las conexiones "keep-alive" quedan abiertas para que las bases
// vivan durante toda la sesión de pruebas.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUserName = "admin.test";
    public const string AdminPassword = "Admin.Test123!";
    public const string CustomerUserName = "cliente.test";
    public const string CustomerPassword = "Cliente.Test123!";

    // Cada contexto usa su propia base en memoria: EnsureCreated no crea tablas
    // en una base que ya tiene tablas de otro contexto.
    private readonly string _dataConnectionString = $"DataSource=data-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly string _authConnectionString = $"DataSource=auth-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _dataKeepAlive;
    private readonly SqliteConnection _authKeepAlive;

    public CustomWebApplicationFactory()
    {
        _dataKeepAlive = new SqliteConnection(_dataConnectionString);
        _authKeepAlive = new SqliteConnection(_authConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            UseSqlite<Dsw2025TpiContext>(services, _dataConnectionString);
            UseSqlite<AuthenticateContext>(services, _authConnectionString);
        });
    }

    private static void UseSqlite<TContext>(IServiceCollection services, string connectionString)
        where TContext : DbContext
    {
        // Se quita la configuración de SQL Server que registra la API y se registra SQLite
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<TContext>>();
        services.AddDbContext<TContext>(options => options.UseSqlite(connectionString));
    }

    public async Task InitializeAsync()
    {
        await _dataKeepAlive.OpenAsync();
        await _authKeepAlive.OpenAsync();

        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;
        var dataContext = provider.GetRequiredService<Dsw2025TpiContext>();
        var authContext = provider.GetRequiredService<AuthenticateContext>();

        // Garantiza que las pruebas nunca corran contra la base SQL Server de la API
        if (!dataContext.Database.IsSqlite() || !authContext.Database.IsSqlite())
            throw new InvalidOperationException("Las pruebas de integración deben ejecutarse sobre SQLite.");

        await dataContext.Database.EnsureCreatedAsync();
        await authContext.Database.EnsureCreatedAsync();

        await CreateUserAsync(provider, AdminUserName, AdminPassword, "Admin");
        await CreateUserAsync(provider, CustomerUserName, CustomerPassword, "User");
    }

    private static async Task CreateUserAsync(IServiceProvider provider, string userName, string password, string role)
    {
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));

        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = userName, Email = $"{userName}@test.com", EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, role);
    }

    // Deja vacías las tablas de negocio; los usuarios y roles se conservan entre pruebas
    public Task ResetDatabaseAsync() => ExecuteDbContextAsync(async db =>
    {
        await db.OrderItems.ExecuteDeleteAsync();
        await db.Orders.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        await db.Customers.ExecuteDeleteAsync();
    });

    public Task SeedAsync(params object[] entities) => ExecuteDbContextAsync(async db =>
    {
        db.AddRange(entities);
        await db.SaveChangesAsync();
    });

    public async Task ExecuteDbContextAsync(Func<Dsw2025TpiContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>());
    }

    public async Task<T> ExecuteDbContextAsync<T>(Func<Dsw2025TpiContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>());
    }

    // Inicia sesión por la API (POST /api/auth/login) y devuelve un cliente con el token JWT
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string userName, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = userName, password });
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    private sealed record LoginResponse(string Token);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _dataKeepAlive.DisposeAsync();
        await _authKeepAlive.DisposeAsync();
    }
}
