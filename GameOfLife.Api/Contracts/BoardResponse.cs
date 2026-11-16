namespace GameOfLife.Api.Contracts;

/// <summary>Full board representation returned by POST and GET-by-id (always generation 0).</summary>
public sealed record BoardResponse(Guid Id, int Rows, int Cols, int Generation, int[][] State);
