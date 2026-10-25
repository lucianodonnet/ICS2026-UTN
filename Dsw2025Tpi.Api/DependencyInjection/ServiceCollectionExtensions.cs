using Dsw2025Tpi.Domain.Interfaces;
using Dsw2025Tpi.Data.Repositories;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Dsw2025Tpi.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services, ConfigurationManager configuration)
    {
        services.AddScoped<IRepository, EfRepository>();
        services.AddScoped<ProductsManagementService>();
        services.AddScoped<OrdersManagementService>();

        services.AddDbContext<Dsw2025TpiContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Dsw2025Tpi")));

        return services;
    }
}
