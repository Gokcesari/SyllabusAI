using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace SyllabusAI.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? TryReadConnectionStringFromServiceSettings(env)
            ?? "Server=(localdb)\\mssqllocaldb;Database=SyllabusAI_DesignTime;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        optionsBuilder.UseSqlServer(connectionString);
        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string? TryReadConnectionStringFromServiceSettings(string env)
    {
        var cwd = Directory.GetCurrentDirectory();
        var serviceDir = Path.GetFullPath(Path.Combine(cwd, "..", "SyllabusAI.Service"));
        var prod = Path.Combine(serviceDir, "appsettings.json");
        var dev = Path.Combine(serviceDir, $"appsettings.{env}.json");
        var fromEnv = TryReadConnectionFromFile(dev);
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;
        return TryReadConnectionFromFile(prod);
    }

    private static string? TryReadConnectionFromFile(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("ConnectionStrings", out var cs)) return null;
            if (!cs.TryGetProperty("DefaultConnection", out var conn)) return null;
            return conn.GetString();
        }
        catch
        {
            return null;
        }
    }
}
