using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chubb.Claims.Infrastructure;

/// <summary>Design-time EF factory that requires a connection supplied by the operator environment.</summary>
public sealed class ClaimsDbContextFactory : IDesignTimeDbContextFactory<ClaimsDbContext>
{
    public ClaimsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CLAIMS_DATABASE_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Set CLAIMS_DATABASE_CONNECTION to a SQL Server connection string for EF design-time operations.");
        var options = new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(connectionString).Options;
        return new ClaimsDbContext(options);
    }
}
