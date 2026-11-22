using Microsoft.EntityFrameworkCore;

namespace Chubb.Claims.Infrastructure;

public sealed class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options) : DbContext(options)
{
}
