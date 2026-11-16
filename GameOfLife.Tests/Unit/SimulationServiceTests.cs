using GameOfLife.Core.Abstractions;
using GameOfLife.Core.Domain;
using GameOfLife.Core.Engine;
using GameOfLife.Core.Simulation;
using GameOfLife.Tests.TestData;

namespace GameOfLife.Tests.Unit;

public sealed class SimulationServiceTests
{
    private static Board MakeBoard(int[][] grid)
    {
        return new Board(
            Guid.NewGuid(),
            grid.Length,
            grid[0].Length,
            Patterns.Flatten(grid),
            DateTimeOffset.UtcNow);
    }

    /// <summary>Counts engine invocations to make short-circuiting observable.</summary>
    private sealed class CountingEngine : IGameOfLifeEngine
    {
        private readonly IGameOfLifeEngine _inner = new FiniteGridEngine();

        public int StepCount { get; private set; }

        public bool Step(ReadOnlySpan<bool> current, Span<bool> next, int rows, int cols)
        {
            StepCount++;
            return _inner.Step(current, next, rows, cols);
        }
    }

    private readonly SimulationService _service = new(new FiniteGridEngine());

    // --- ComputeGeneration ---

    [Fact]
    public void ComputeGeneration_Blinker_OneStep_IsHorizontal()
    {
        bool[] state = _service.ComputeGeneration(MakeBoard(Patterns.BlinkerVertical), steps: 1);

        Assert.Equal(Patterns.Flatten(Patterns.BlinkerHorizontal), state);
    }

    [Fact]
    public void ComputeGeneration_Blinker_EvenSteps_EqualsInitialState()
    {
        bool[] state = _service.ComputeGeneration(MakeBoard(Patterns.BlinkerVertical), steps: 4);

        Assert.Equal(Patterns.Flatten(Patterns.BlinkerVertical), state);
    }

    [Fact]
    public void ComputeGeneration_ZeroSteps_ReturnsBaseline()
    {
        bool[] state = _service.ComputeGeneration(MakeBoard(Patterns.BlinkerVertical), steps: 0);

        Assert.Equal(Patterns.Flatten(Patterns.BlinkerVertical), state);
    }

    [Fact]
    public void ComputeGeneration_StillLife_WithHugeStepCount_InvokesEngineExactlyOnce()
    {
        var countingEngine = new CountingEngine();
        var service = new SimulationService(countingEngine);

        bool[] state = service.ComputeGeneration(MakeBoard(Patterns.Block), steps: 10_000);

        Assert.Equal(1, countingEngine.StepCount);
        Assert.Equal(Patterns.Flatten(Patterns.Block), state);
    }

    [Fact]
    public void ComputeGeneration_DoesNotMutateTheBoardBaseline()
    {
        Board board = MakeBoard(Patterns.BlinkerVertical);
        var baselineSnapshot = (bool[])board.Cells.Clone();

        _service.ComputeGeneration(board, steps: 5);

        Assert.Equal(baselineSnapshot, board.Cells);
    }

    [Fact]
    public void ComputeGeneration_NegativeSteps_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.ComputeGeneration(MakeBoard(Patterns.Block), steps: -1));
    }

    // --- ComputeFinalState ---

    [Fact]
    public void ComputeFinalState_Block_IsStableAtGenerationZero_WithNoCyclePeriod()
    {
        FinalStateResult result = _service.ComputeFinalState(MakeBoard(Patterns.Block), maxIterations: 1000);

        Assert.Equal(FinalStateKind.Stable, result.Kind);
        Assert.Equal(0, result.Generation);
        Assert.Null(result.CyclePeriod);
        Assert.Equal(Patterns.Flatten(Patterns.Block), result.State);
    }

    [Fact]
    public void ComputeFinalState_SingleCell_ExtinctionIsStableAtGenerationOne()
    {
        FinalStateResult result = _service.ComputeFinalState(MakeBoard(Patterns.SingleCell), maxIterations: 1000);

        Assert.Equal(FinalStateKind.Stable, result.Kind);
        Assert.Equal(1, result.Generation);
        Assert.Null(result.CyclePeriod);
        Assert.All(result.State, cell => Assert.False(cell));
    }

    [Fact]
    public void ComputeFinalState_Blinker_OscillatesWithPeriodTwo_FromGenerationZero()
    {
        FinalStateResult result = _service.ComputeFinalState(MakeBoard(Patterns.BlinkerVertical), maxIterations: 1000);

        Assert.Equal(FinalStateKind.Oscillating, result.Kind);
        Assert.Equal(0, result.Generation);
        Assert.Equal(2, result.CyclePeriod);
        Assert.Equal(Patterns.Flatten(Patterns.BlinkerVertical), result.State);
    }

    [Fact]
    public void ComputeFinalState_Toad_OscillatesWithPeriodTwo()
    {
        FinalStateResult result = _service.ComputeFinalState(MakeBoard(Patterns.ToadPhase1), maxIterations: 1000);

        Assert.Equal(FinalStateKind.Oscillating, result.Kind);
        Assert.Equal(2, result.CyclePeriod);
    }

    [Fact]
    public void ComputeFinalState_Glider_WithTinyIterationBudget_IsNotReached()
    {
        Board glider = MakeBoard(Patterns.GliderAt(1, 1, 10, 10));

        FinalStateResult result = _service.ComputeFinalState(glider, maxIterations: 3);

        Assert.Equal(FinalStateKind.NotReached, result.Kind);
        Assert.Equal(3, result.Generation);
        Assert.Null(result.CyclePeriod);
    }

    [Fact]
    public void ComputeFinalState_Glider_EventuallySettles_WhenBudgetAllows()
    {
        // On a finite grid the glider crashes into the wall and decays into a
        // stable or oscillating remnant well within 100 generations on a 10x10 board.
        Board glider = MakeBoard(Patterns.GliderAt(1, 1, 10, 10));

        FinalStateResult result = _service.ComputeFinalState(glider, maxIterations: 1000);

        Assert.NotEqual(FinalStateKind.NotReached, result.Kind);
    }

    [Fact]
    public void ComputeFinalState_InvalidIterationBudget_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.ComputeFinalState(MakeBoard(Patterns.Block), maxIterations: 0));
    }
}
