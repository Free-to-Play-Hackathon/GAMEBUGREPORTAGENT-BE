using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GameBug.Infrastructure.Persistence;

public class GameBugDbContextFactory : IDesignTimeDbContextFactory<GameBugDbContext>
{
    public GameBugDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("src/GameBug.Api/appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.GetConnectionString("Database")
            ?? "Host=localhost;Port=5433;Database=gamebug_db;Username=postgres;Password=postgres_password";

        var optionsBuilder = new DbContextOptionsBuilder<GameBugDbContext>();
        optionsBuilder.UseNpgsql(connectionString, x => x.UseVector());

        return new GameBugDbContext(optionsBuilder.Options);
    }
}
