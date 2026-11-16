using GameOfLife.Core.Engine;
using GameOfLife.Tests.TestData;

namespace GameOfLife.Tests.Unit;

public sealed class FiniteGridEngineTests
{
    private readonly FiniteGridEngine _engine = new();

    private (bool[] Next, bool Changed) Step(int[][] grid)
    {
        int rows = grid.Length;
        int cols = grid[0].Length;
        bool[] current = Patterns.Flatten(grid);
        var next = new bool[current.Length];

        bool changed = _engine.Step(current, next, rows, cols);

        return (next, changed);
    }

    private bool[] Run(int[][] grid, int steps)
    {
        int rows = grid.Length;
        int cols = grid[0].Length;
        bool[] current = Patterns.Flatten(grid);
        var next = new bool[current.Length];

        for (int i = 0; i < steps; i++)
        {
            _engine.Step(current, next, rows, cols);
            (current, next) = (next, current);
        }

        return current;
    }

    // --- Conway's four rules in isolation ---

    [Fact]
    public void Step_LiveCellWithZeroNeighbors_DiesOfUnderpopulation()
    {
        var (next, changed) = Step(Patterns.SingleCell);

        Assert.True(changed);
        Assert.Equal(Patterns.Flatten([[0, 0, 0], [0, 0, 0], [0, 0, 0]]), next);
    }

    [Fact]
    public void Step_LiveCellWithOneNeighbor_DiesOfUnderpopulation()
    {
        // Horizontal domino: each live cell has exactly one neighbor.
        var (next, _) = Step([[1, 1, 0], [0, 0, 0], [0, 0, 0]]);

        Assert.False(next[0]);
        Assert.False(next[1]);
    }

    [Fact]
    public void Step_LiveCellWithTwoNeighbors_Survives()
    {
        // Middle of a 3-in-a-row has exactly two live neighbors.
        var (next, _) = Step([[1, 1, 1]]);

        Assert.Equal(Patterns.Flatten([[0, 1, 0]]), next);
    }

    [Fact]
    public void Step_LiveCellWithThreeNeighbors_Survives()
    {
        // Every cell of a block has exactly three live neighbors.
        var (next, _) = Step(Patterns.Block);

        Assert.Equal(Patterns.Flatten(Patterns.Block), next);
    }

    [Fact]
    public void Step_LiveCellWithFourNeighbors_DiesOfOverpopulation()
    {
        // The center of a plus shape has four live neighbors.
        int[][] plus =
        [
            [0, 1, 0],
            [1, 1, 1],
            [0, 1, 0],
        ];

        var (next, _) = Step(plus);

        Assert.False(next[1 * 3 + 1]);
    }

    [Fact]
    public void Step_DeadCellWithExactlyThreeNeighbors_BecomesAlive()
    {
        // L-triomino: the dead cell at (1,1) has exactly three live neighbors.
        int[][] lTriomino =
        [
            [1, 1, 0],
            [1, 0, 0],
            [0, 0, 0],
        ];

        var (next, _) = Step(lTriomino);

        Assert.True(next[1 * 3 + 1]);
    }

    [Fact]
    public void Step_DeadCellWithTwoNeighbors_StaysDead()
    {
        // Dead cells below a domino have exactly two live neighbors: no birth.
        var (next, _) = Step([[1, 1], [0, 0]]);

        Assert.False(next[1 * 2 + 0]);
        Assert.False(next[1 * 2 + 1]);
    }

    // --- Still lifes, oscillators, and spaceships ---

    [Fact]
    public void Step_Block_IsUnchanged_AndReportsNoChange()
    {
        var (next, changed) = Step(Patterns.Block);

        Assert.False(changed);
        Assert.Equal(Patterns.Flatten(Patterns.Block), next);
    }

    [Fact]
    public void Step_VerticalBlinker_BecomesHorizontal()
    {
        var (next, changed) = Step(Patterns.BlinkerVertical);

        Assert.True(changed);
        Assert.Equal(Patterns.Flatten(Patterns.BlinkerHorizontal), next);
    }

    [Fact]
    public void Step_Blinker_ReturnsToInitialStateAfterTwoSteps()
    {
        bool[] afterTwo = Run(Patterns.BlinkerVertical, steps: 2);

        Assert.Equal(Patterns.Flatten(Patterns.BlinkerVertical), afterTwo);
    }

    [Fact]
    public void Step_Toad_AdvancesToSecondPhase()
    {
        var (next, _) = Step(Patterns.ToadPhase1);

        Assert.Equal(Patterns.Flatten(Patterns.ToadPhase2), next);
    }

    [Fact]
    public void Step_Toad_ReturnsToInitialStateAfterTwoSteps()
    {
        bool[] afterTwo = Run(Patterns.ToadPhase1, steps: 2);

        Assert.Equal(Patterns.Flatten(Patterns.ToadPhase1), afterTwo);
    }

    [Fact]
    public void Step_Glider_TranslatesOneCellDiagonallyEveryFourSteps()
    {
        bool[] afterFour = Run(Patterns.GliderAt(1, 1, 10, 10), steps: 4);

        Assert.Equal(Patterns.Flatten(Patterns.GliderAt(2, 2, 10, 10)), afterFour);
    }

    // --- Finite boundary semantics ---

    [Fact]
    public void Step_CornerPattern_TreatsOutOfBoundsNeighborsAsDead()
    {
        // L-triomino in the top-left corner completes into a block; the far corner
        // (2,2) would be born under toroidal wrap-around but must stay dead here.
        int[][] corner =
        [
            [1, 1, 0],
            [1, 0, 0],
            [0, 0, 0],
        ];

        var (next, _) = Step(corner);

        Assert.Equal(Patterns.Flatten([[1, 1, 0], [1, 1, 0], [0, 0, 0]]), next);
        Assert.False(next[2 * 3 + 2]);
    }

    [Fact]
    public void Step_BlinkerAgainstLeftEdge_DoesNotWrapAround()
    {
        // A vertical blinker in column 0: under toroidal topology the whole grid
        // would fill; on a finite grid it collapses to a horizontal domino.
        int[][] edgeBlinker =
        [
            [1, 0, 0],
            [1, 0, 0],
            [1, 0, 0],
        ];

        var (next, _) = Step(edgeBlinker);

        Assert.Equal(Patterns.Flatten([[0, 0, 0], [1, 1, 0], [0, 0, 0]]), next);
    }

    // --- Fixed points ---

    [Fact]
    public void Step_EmptyGrid_StaysEmpty_AndReportsNoChange()
    {
        var (next, changed) = Step([[0, 0, 0], [0, 0, 0], [0, 0, 0]]);

        Assert.False(changed);
        Assert.All(next, cell => Assert.False(cell));
    }

    [Fact]
    public void Step_SingleLiveCell_DiesToEmptyGrid()
    {
        var (next, changed) = Step(Patterns.SingleCell);

        Assert.True(changed);
        Assert.All(next, cell => Assert.False(cell));
    }

    // --- Argument validation ---

    [Fact]
    public void Step_MismatchedBufferLengths_Throws()
    {
        var current = new bool[9];
        var next = new bool[8];

        Assert.Throws<ArgumentException>(() => _engine.Step(current, next, 3, 3));
    }
}
