using System.Text.Json.Serialization;

namespace GameOfLife.Api.Contracts;

/// <summary>
/// Upload payload: <c>{ "state": [[0,1],[1,0]] }</c>. Cells may be 0/1 integers or
/// booleans (normalized to 0/1). <see cref="State"/> is nullable so a missing or null
/// property reaches the validator and yields a descriptive 400 instead of a binder error.
/// </summary>
public sealed record CreateBoardRequest(
    [property: JsonConverter(typeof(FlexibleCellArrayConverter))] int[][]? State);
