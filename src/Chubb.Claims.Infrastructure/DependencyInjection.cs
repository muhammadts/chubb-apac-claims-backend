using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Chubb.Claims.Application.Abstractions;
using Chubb.Claims.Infrastructure.Persistence;

namespace Chubb.Claims.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ClaimsDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("ClaimsDatabase");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Connection string 'ClaimsDatabase' must be configured before database operations are used.");
            options.UseSqlServer(connectionString);
        });
        services.AddScoped<IClaimUnitOfWork, SqlClaimUnitOfWork>();
        services.AddScoped<IClaimQueryStore, SqlClaimQueryStore>();
        return services;
    }
}
