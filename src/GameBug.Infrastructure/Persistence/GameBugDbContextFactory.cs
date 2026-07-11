using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GameBug.Infrastructure.Persistence;

public class GameBugDbContextFactory : IDesignTimeDbContextFactory<GameBugDbContext>
{
    public GameBugDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GameBugDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=gamebug_db;Username=postgres;Password=postgres_password", x => x.UseVector());

        return new GameBugDbContext(optionsBuilder.Options);
    }
}
