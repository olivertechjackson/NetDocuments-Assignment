using GameOfLife.Core.Validation;

namespace GameOfLife.Tests.Unit;

public sealed class BoardStateValidatorTests
{
    private const int MaxDimension = 3;

    [Fact]
    public void Validate_NullState_Fails()
    {
        BoardValidationResult result = BoardStateValidator.Validate(null, MaxDimension);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Validate_EmptyOuterArray_Fails()
    {
        BoardValidationResult result = BoardStateValidator.Validate([], MaxDimension);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyFirstRow_Fails()
    {
        BoardValidationResult result = BoardStateValidator.Validate([[]], MaxDimension);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_NullRow_Fails()
    {
        BoardValidationResult result = BoardStateValidator.Validate([[0, 1], null!], MaxDimension);

        Assert.False(result.IsValid);
        Assert.Contains("null", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_JaggedRows_Fails()
    {
        BoardValidationResult result = BoardStateValidator.Validate([[0, 1], [0]], MaxDimension);

        Assert.False(result.IsValid);
        Assert.Contains("same length", result.Error);
    }

    [Fact]
    public void Validate_CellValueTwo_Fails()
    {
        BoardValidationResult result = BoardStateValidator.Validate([[0, 2]], MaxDimension);

        Assert.False(result.IsValid);
        Assert.Contains("0 or 1", result.Error);
    }

    [Fact]
    public void Validate_NegativeCellValue_Fails()
    {
        BoardValidationResult result = BoardStateValidator.Validate([[0, -1]], MaxDimension);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_TooManyRows_Fails()
    {
        int[][] fourRows = [[0], [0], [0], [0]];

        BoardValidationResult result = BoardStateValidator.Validate(fourRows, MaxDimension);

        Assert.False(result.IsValid);
        Assert.Contains("rows", result.Error);
    }

    [Fact]
    public void Validate_TooManyColumns_Fails()
    {
        int[][] fourCols = [[0, 0, 0, 0]];

        BoardValidationResult result = BoardStateValidator.Validate(fourCols, MaxDimension);

        Assert.False(result.IsValid);
        Assert.Contains("columns", result.Error);
    }

    [Fact]
    public void Validate_GridAtExactMaxDimension_Succeeds()
    {
        int[][] grid = [[1, 0, 1], [0, 1, 0], [1, 0, 1]];

        BoardValidationResult result = BoardStateValidator.Validate(grid, MaxDimension);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Validate_ValidGrid_FlattensRowMajorWithCorrectDimensions()
    {
        // Asymmetric 2x3 grid so a row/column mix-up cannot pass by accident.
        int[][] grid = [[1, 0, 0], [0, 0, 1]];

        BoardValidationResult result = BoardStateValidator.Validate(grid, maxGridDimension: 10);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Rows);
        Assert.Equal(3, result.Cols);

        bool[]? cells = result.Cells;

        Assert.NotNull(cells);
        Assert.Equal([true, false, false, false, false, true], cells);
    }
}
