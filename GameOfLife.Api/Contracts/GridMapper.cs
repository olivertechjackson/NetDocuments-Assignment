namespace GameOfLife.Api.Contracts;

/// <summary>
/// The single conversion point from the domain's flat <see cref="bool"/> cells to the
/// jagged 0/1 integer matrix exposed over HTTP. Routing every response through here
/// guarantees clients always see numeric 0/1 cells regardless of internal representation.
/// </summary>
public static class GridMapper
{
    public static int[][] ToJagged(ReadOnlySpan<bool> cells, int rows, int cols)
    {
        if (cells.Length != rows * cols)
        {
            throw new ArgumentException(
                $"Cell span length {cells.Length} does not match dimensions {rows}x{cols}.",
                nameof(cells));
        }

        var result = new int[rows][];

        for (int r = 0; r < rows; r++)
        {
            var row = new int[cols];
            int rowBase = r * cols;

            for (int c = 0; c < cols; c++)
            {
                row[c] = cells[rowBase + c] ? 1 : 0;
            }

            result[r] = row;
        }

        return result;
    }
}
