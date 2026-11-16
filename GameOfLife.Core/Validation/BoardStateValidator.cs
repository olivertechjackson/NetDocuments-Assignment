namespace GameOfLife.Core.Validation;

/// <summary>
/// Semantic validation of uploaded grids: shape (non-empty, rectangular, within limits)
/// and cell values (0 or 1). Produces the flattened row-major representation on success.
/// </summary>
public static class BoardStateValidator
{
    public static BoardValidationResult Validate(int[][]? state, int maxGridDimension)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxGridDimension, 1);

        if (state is null || state.Length == 0)
        {
            return BoardValidationResult.Failure("Board state must be a non-empty 2D array of 0/1 values.");
        }

        int rows = state.Length;

        if (rows > maxGridDimension)
        {
            return BoardValidationResult.Failure(
                $"Board has {rows} rows but the maximum allowed dimension is {maxGridDimension}.");
        }

        int[]? firstRow = state[0];

        if (firstRow is null || firstRow.Length == 0)
        {
            return BoardValidationResult.Failure("Board rows must be non-empty arrays of 0/1 values.");
        }

        int cols = firstRow.Length;

        if (cols > maxGridDimension)
        {
            return BoardValidationResult.Failure(
                $"Board has {cols} columns but the maximum allowed dimension is {maxGridDimension}.");
        }

        var cells = new bool[rows * cols];

        for (int r = 0; r < rows; r++)
        {
            int[]? row = state[r];

            if (row is null)
            {
                return BoardValidationResult.Failure($"Row {r} is null; every row must be an array of 0/1 values.");
            }

            if (row.Length != cols)
            {
                return BoardValidationResult.Failure(
                    $"All rows must have the same length: row 0 has {cols} cells but row {r} has {row.Length}.");
            }

            for (int c = 0; c < cols; c++)
            {
                int value = row[c];

                if (value is not (0 or 1))
                {
                    return BoardValidationResult.Failure(
                        $"Cell values must be 0 or 1, but row {r}, column {c} contains {value}.");
                }

                cells[r * cols + c] = value == 1;
            }
        }

        return BoardValidationResult.Success(cells, rows, cols);
    }
}
