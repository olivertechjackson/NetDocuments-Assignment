namespace GameOfLife.Core.Abstractions;

/// <summary>
/// Computes single generation transitions. Implementations define the grid topology
/// (finite bounded, toroidal, ...) so topology can change without touching callers.
/// </summary>
public interface IGameOfLifeEngine
{
    /// <summary>
    /// Writes the generation that follows <paramref name="current"/> into <paramref name="next"/>.
    /// Both spans are row-major with length <c>rows * cols</c>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if <paramref name="next"/> differs from <paramref name="current"/>;
    /// <see langword="false"/> if the board is a still life (all later generations are identical).
    /// </returns>
    bool Step(ReadOnlySpan<bool> current, Span<bool> next, int rows, int cols);
}
