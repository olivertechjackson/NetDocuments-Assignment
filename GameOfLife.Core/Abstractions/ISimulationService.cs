using GameOfLife.Core.Domain;
using GameOfLife.Core.Simulation;

namespace GameOfLife.Core.Abstractions;

/// <summary>Pure, deterministic simulation over an immutable board baseline.</summary>
public interface ISimulationService
{
    /// <summary>
    /// Returns the cell states after exactly <paramref name="steps"/> generations.
    /// Still lifes short-circuit internally; the result is identical either way.
    /// The board's own cells are never modified.
    /// </summary>
    bool[] ComputeGeneration(Board board, int steps);

    /// <summary>
    /// Simulates until the board becomes stable, enters an oscillating cycle, or
    /// <paramref name="maxIterations"/> generations have been computed without either happening.
    /// </summary>
    FinalStateResult ComputeFinalState(Board board, int maxIterations);
}
