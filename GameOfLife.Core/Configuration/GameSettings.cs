using System.ComponentModel.DataAnnotations;

namespace GameOfLife.Core.Configuration;

/// <summary>
/// Simulation limits bound from the <c>Game</c> configuration section and validated at startup.
/// </summary>
public sealed class GameSettings
{
    public const string SectionName = "Game";

    /// <summary>Maximum number of rows and maximum number of columns accepted for an uploaded board.</summary>
    [Range(1, 10_000)]
    public int MaxGridDimension { get; set; } = 500;

    /// <summary>Maximum value of <c>steps</c> accepted by the N-states-ahead endpoint.</summary>
    [Range(1, 1_000_000)]
    public int MaxAllowedSteps { get; set; } = 10_000;

    /// <summary>Maximum generations the final-state endpoint will simulate before giving up.</summary>
    [Range(1, 1_000_000)]
    public int MaxFinalIterations { get; set; } = 1_000;
}
