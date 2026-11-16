namespace GameOfLife.Core.Validation;

/// <summary>
/// Outcome of validating an uploaded grid. When valid, carries the flattened
/// row-major cells ready for the domain <c>Board</c>.
/// </summary>
public sealed record BoardValidationResult(bool IsValid, string? Error, bool[]? Cells, int Rows, int Cols)
{
    public static BoardValidationResult Failure(string error) => new(false, error, null, 0, 0);

    public static BoardValidationResult Success(bool[] cells, int rows, int cols) => new(true, null, cells, rows, cols);
}
