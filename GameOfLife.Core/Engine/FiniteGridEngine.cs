using GameOfLife.Core.Abstractions;

namespace GameOfLife.Core.Engine;

/// <summary>
/// Conway's Game of Life on a finite, bounded grid: cells outside the boundary are
/// permanently dead, so edge and corner cells simply have fewer live-capable neighbors.
/// </summary>
public sealed class FiniteGridEngine : IGameOfLifeEngine
{
    public bool Step(ReadOnlySpan<bool> current, Span<bool> next, int rows, int cols)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(cols, 1);

        int expectedLength = rows * cols;
        if (current.Length != expectedLength || next.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Buffers must have length {expectedLength} for a {rows}x{cols} grid " +
                $"(got current={current.Length}, next={next.Length}).");
        }

        bool changed = false;

        for (int row = 0; row < rows; row++)
        {
            int rowBase = row * cols;

            for (int col = 0; col < cols; col++)
            {
                int liveNeighbors = CountLiveNeighbors(current, rows, cols, row, col);
                bool alive = current[rowBase + col];

                // Conway's rules: a live cell survives with 2 or 3 live neighbors
                // (dies of under- or overpopulation otherwise); a dead cell is born
                // with exactly 3 live neighbors.
                bool nextAlive = alive
                    ? liveNeighbors is 2 or 3
                    : liveNeighbors is 3;

                next[rowBase + col] = nextAlive;
                changed |= nextAlive != alive;
            }
        }

        return changed;
    }

    private static int CountLiveNeighbors(ReadOnlySpan<bool> cells, int rows, int cols, int row, int col)
    {
        int rowMin = Math.Max(row - 1, 0);
        int rowMax = Math.Min(row + 1, rows - 1);
        int colMin = Math.Max(col - 1, 0);
        int colMax = Math.Min(col + 1, cols - 1);

        int count = 0;

        for (int r = rowMin; r <= rowMax; r++)
        {
            int rBase = r * cols;

            for (int c = colMin; c <= colMax; c++)
            {
                if ((r != row || c != col) && cells[rBase + c])
                {
                    count++;
                }
            }
        }

        return count;
    }
}
