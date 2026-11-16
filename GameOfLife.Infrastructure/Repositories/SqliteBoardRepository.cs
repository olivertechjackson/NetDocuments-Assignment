using GameOfLife.Core.Abstractions;
using GameOfLife.Core.Domain;
using GameOfLife.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameOfLife.Infrastructure.Repositories;

/// <summary>SQLite-backed <see cref="IBoardRepository"/> via EF Core.</summary>
public sealed class SqliteBoardRepository : IBoardRepository
{
    private readonly GameOfLifeDbContext _dbContext;

    public SqliteBoardRepository(GameOfLifeDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(Board board, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(board);

        var entity = new BoardEntity
        {
            Id = board.Id,
            Rows = board.Rows,
            Cols = board.Cols,
            Cells = CellSerializer.Serialize(board.Cells),
            CreatedAtUtc = board.CreatedAtUtc,
        };

        _dbContext.Boards.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Board?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        BoardEntity? entity = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        bool[] cells = CellSerializer.Deserialize(entity.Cells, entity.Rows, entity.Cols);

        return new Board(entity.Id, entity.Rows, entity.Cols, cells, entity.CreatedAtUtc);
    }
}
