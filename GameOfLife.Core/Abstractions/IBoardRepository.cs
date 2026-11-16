using GameOfLife.Core.Domain;

namespace GameOfLife.Core.Abstractions;

/// <summary>Persistent store for immutable generation-0 boards.</summary>
public interface IBoardRepository
{
    Task AddAsync(Board board, CancellationToken cancellationToken);

    Task<Board?> GetAsync(Guid id, CancellationToken cancellationToken);
}
