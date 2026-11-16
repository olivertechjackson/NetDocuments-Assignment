namespace GameOfLife.Core.Simulation;

public enum FinalStateKind
{
    /// <summary>The board stopped changing (period-1 fixed point, including total extinction).</summary>
    Stable,

    /// <summary>The board entered a repeating cycle with period ≥ 2 (e.g. Blinker, Toad).</summary>
    Oscillating,

    /// <summary>Neither stability nor a cycle was observed within the iteration budget.</summary>
    NotReached,
}

/// <summary>
/// Outcome of a final-state search.
/// </summary>
/// <param name="Kind">How (or whether) the simulation settled.</param>
/// <param name="Generation">
/// The generation at which <paramref name="State"/> first occurred: the first unchanging
/// generation for <see cref="FinalStateKind.Stable"/>, the cycle-entry generation for
/// <see cref="FinalStateKind.Oscillating"/>, or the iteration budget for
/// <see cref="FinalStateKind.NotReached"/>.
/// </param>
/// <param name="CyclePeriod">Cycle length; only set for <see cref="FinalStateKind.Oscillating"/> (always ≥ 2).</param>
/// <param name="State">Row-major cell states at <paramref name="Generation"/>.</param>
public sealed record FinalStateResult(FinalStateKind Kind, int Generation, int? CyclePeriod, bool[] State);
