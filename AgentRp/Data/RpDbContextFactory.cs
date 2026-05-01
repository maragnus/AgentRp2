using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgentRp.Data;

public sealed class RpDbContextFactory : IDesignTimeDbContextFactory<RpDbContext>
{
    public RpDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__agentrp-db2")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__agentrp-db")
            ?? "Server=localhost,1433;Database=agentrp;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<RpDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new RpDbContext(optionsBuilder.Options);
    }
}
