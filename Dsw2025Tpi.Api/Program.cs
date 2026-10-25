using Dsw2025Tpi.Api.DependencyInjection;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Data.Helpers;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;


namespace Dsw2025Tpi.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

            });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Dsw2025TPI - FLJ",
                Version = "v4",
            });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Ingresar el token",
                Type = SecuritySchemeType.ApiKey
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
        });
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins("http://localhost:5173", "http://localhost:5174") // Agregamos ambos puertos porque no logré otra solucion :/
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });
        builder.Services.AddHealthChecks();

        // Registrar primero el DbContext de autenticación antes de configurar Identity
        builder.Services.AddDbContext<AuthenticateContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("Dsw2025Tpi"));
        });

        builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password = new PasswordOptions
            {
                RequiredLength = 8
            };

        })
               .AddEntityFrameworkStores<AuthenticateContext>()
               .AddDefaultTokenProviders();

        // Registrar servicios del dominio (incluye Dsw2025TpiContext)
        builder.Services.AddDomainServices(builder.Configuration);
        builder.Services.AddSingleton<JwtTokenService>();
        var jwtConfig = builder.Configuration.GetSection("Jwt");
        var keyText = jwtConfig["Key"] ?? throw new ArgumentNullException("JWT Key");
        var key = Encoding.UTF8.GetBytes(keyText);

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtConfig["Issuer"],
                ValidAudience = jwtConfig["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key),
                RoleClaimType = ClaimTypes.Role
            };
        });


        var app = builder.Build();

        var rolesToCreate = builder.Configuration.GetSection("Roles").Get<List<string>>();

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
            dbContext.Database.Migrate();
            var authContext = scope.ServiceProvider.GetRequiredService<AuthenticateContext>();
            authContext.Database.Migrate();
            dbContext.Seedwork<Product>("Sources/products.json");
            dbContext.Seedwork<Customer>("Sources/customers.json");
            dbContext.Seedwork<Order>("Sources/orders.json");
           
            
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            foreach (var roleName in rolesToCreate!)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var adminSection = builder.Configuration.GetSection("DefaultAdminUser");

            // Add null checks and better error handling
            var userName = adminSection["UserName"];
            var email = adminSection["Email"];  
            var password = adminSection["Password"];
            var role = adminSection["Role"];

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(email) || 
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(role))
            {
                throw new InvalidOperationException("DefaultAdminUser configuration is missing or incomplete");
            }

            var adminUser = await userManager.FindByNameAsync(userName);
            if (adminUser == null)
            {
                var newUser = new IdentityUser
                {
                    UserName = userName,
                    Email = email,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(newUser, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newUser, role);
                }
                else
                {
                    throw new InvalidOperationException($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

        }

        // Habilitar Swagger siempre y exponer la UI en la raíz (/) para que se muestre al abrir la aplicación
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = string.Empty; // servir Swagger UI en /
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Dsw2025TPI - FLJ v1");
        });

        app.UseCors("AllowFrontend");
        // Deshabilitar redirección a HTTPS para permitir acceder a Swagger en HTTP cuando no hay endpoint HTTPS configurado
        // app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

      

        app.Logger.LogInformation("Application has started successfully.");

        app.MapControllers();
        app.MapHealthChecks("/healthcheck");

        app.Run();
    }
}