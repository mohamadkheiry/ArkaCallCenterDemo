using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ArkaCallCenter.Infrastructure.Data;

/// <summary>
/// فقط برای ابزار EF (ساخت migration) در زمان طراحی استفاده می‌شود.
/// از نسخه‌ی ثابت MySQL استفاده می‌کند تا نیازی به دیتابیس زنده نباشد.
/// </summary>
public class ArkaDbContextFactory : IDesignTimeDbContextFactory<ArkaDbContext>
{
    public ArkaDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("MYSQL_CONNECTION")
                   ?? "Server=localhost;Port=3306;Database=arka_callcenter;User=root;Password=changeme;TreatTinyAsBoolean=true;";
        var options = new DbContextOptionsBuilder<ArkaDbContext>()
            .UseMySql(conn, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;
        return new ArkaDbContext(options);
    }
}
