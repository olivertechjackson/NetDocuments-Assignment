using GameOfLife.Core.Abstractions;
using GameOfLife.Core.Domain;
using GameOfLife.Core.Hashing;

namespace GameOfLife.Core.Simulation;

/// <summary>
/// Runs the engine over an immutable board baseline using two reusable buffers,
/// so a request allocates exactly two cell arrays regardless of how many
/// generations it computes.
/// </summary>
public sealed class SimulationService : ISimulationService
{
    private readonly IGameOfLifeEngine _engine;

    public SimulationService(IGameOfLifeEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public bool[] ComputeGeneration(Board board, int steps)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentOutOfRangeException.ThrowIfNegative(steps);

        var current = (bool[])board.Cells.Clone();
        var next = new bool[current.Length];

        for (int i = 0; i < steps; i++)
        {
            bool changed = _engine.Step(current, next, board.Rows, board.Cols);
            (current, next) = (next, current);

            if (!changed)
            {
                // Still life: every remaining generation is identical, so the
                // final answer is already in hand.
                break;
            }
        }

        return current;
    }

    public FinalStateResult ComputeFinalState(Board board, int maxIterations)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxIterations, 1);

        var current = (bool[])board.Cells.Clone();
        var next = new bool[current.Length];

        var firstSeenGeneration = new Dictionary<string, int>
        {
            [GridHasher.ComputeHash(current, board.Rows, board.Cols)] = 0,
        };

        for (int generation = 1; generation <= maxIterations; generation++)
        {
            _engine.Step(current, next, board.Rows, board.Cols);
            (current, next) = (next, current);

            string hash = GridHasher.ComputeHash(current, board.Rows, board.Cols);

            if (firstSeenGeneration.TryGetValue(hash, out int firstSeen))
            {
                int period = generation - firstSeen;

                return period == 1
                    ? new FinalStateResult(FinalStateKind.Stable, firstSeen, null, current)
                    : new FinalStateResult(FinalStateKind.Oscillating, firstSeen, period, current);
            }

            firstSeenGeneration[hash] = generation;
        }

        return new FinalStateResult(FinalStateKind.NotReached, maxIterations, null, current);
    }
}
