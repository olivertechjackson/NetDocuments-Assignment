namespace GameOfLife.Core.Domain;

/// <summary>
/// An uploaded board in its generation-0 state. Boards are immutable baselines:
/// every simulation endpoint computes derived generations from <see cref="Cells"/>
/// without ever modifying it.
/// </summary>
public sealed class Board
{
    public Guid Id { get; }

    public int Rows { get; }

    public int Cols { get; }

    /// <summary>Row-major cell states; the cell at (r, c) is <c>Cells[r * Cols + c]</c>.</summary>
    public bool[] Cells { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public Board(Guid id, int rows, int cols, bool[] cells, DateTimeOffset createdAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(cols, 1);
        ArgumentNullException.ThrowIfNull(cells);

        if (cells.Length != rows * cols)
        {
            throw new ArgumentException(
                $"Cell array length {cells.Length} does not match dimensions {rows}x{cols}.",
                nameof(cells));
        }

        Id = id;
        Rows = rows;
        Cols = cols;
        Cells = cells;
        CreatedAtUtc = createdAtUtc;
    }
}
