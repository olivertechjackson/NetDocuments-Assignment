namespace GameOfLife.Api.Contracts;

/// <summary>A computed generation of a board (returned by /next and /states/{steps}).</summary>
public sealed record BoardStateResponse(Guid Id, int Generation, int[][] State);
