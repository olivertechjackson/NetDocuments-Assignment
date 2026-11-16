namespace GameOfLife.Api.Contracts;

/// <summary>
/// Settled outcome returned by /final. <paramref name="Status"/> is "Stable" or "Oscillating";
/// <paramref name="CyclePeriod"/> is present only when oscillating (period ≥ 2) and is omitted
/// from the JSON entirely when null. <paramref name="Generation"/> is the generation at which
/// the returned state first occurred.
/// </summary>
public sealed record FinalStateResponse(Guid Id, string Status, int Generation, int? CyclePeriod, int[][] State);
