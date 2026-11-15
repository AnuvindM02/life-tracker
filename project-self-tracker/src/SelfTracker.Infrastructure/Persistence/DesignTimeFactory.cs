using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SelfTracker.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef migrations add`; never connects.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=LAPTOP-6CDSA325\\SQLEXPRESS;Database=SelfTracker;Integrated Security=True;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options, new SystemClock(TimeProvider.System));
    }
}
