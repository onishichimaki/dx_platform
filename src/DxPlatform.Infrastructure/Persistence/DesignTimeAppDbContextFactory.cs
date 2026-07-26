using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DxPlatform.Infrastructure.Persistence;

public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=DxPlatformDesignTime;Trusted_Connection=True;Encrypt=False",
                sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;
        return new AppDbContext(options);
    }
}

