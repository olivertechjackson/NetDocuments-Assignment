using GameOfLife.Core.Abstractions;
using GameOfLife.Infrastructure.Persistence;
using GameOfLife.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameOfLife.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the SQLite-backed persistence layer.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<GameOfLifeDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IBoardRepository, SqliteBoardRepository>();

        return services;
    }
}
