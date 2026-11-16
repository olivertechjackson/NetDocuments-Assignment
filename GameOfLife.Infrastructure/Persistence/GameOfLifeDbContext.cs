using Microsoft.EntityFrameworkCore;

namespace GameOfLife.Infrastructure.Persistence;

public sealed class GameOfLifeDbContext : DbContext
{
    public GameOfLifeDbContext(DbContextOptions<GameOfLifeDbContext> options)
        : base(options)
    {
    }

    public DbSet<BoardEntity> Boards => Set<BoardEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameOfLifeDbContext).Assembly);
    }
}
