using System.Text.Json;
using GameOfLife.Api.Contracts;

namespace GameOfLife.Tests.Unit;

public sealed class FlexibleCellArrayConverterTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private static CreateBoardRequest? Deserialize(string json) =>
        JsonSerializer.Deserialize<CreateBoardRequest>(json, WebOptions);

    [Fact]
    public void Read_IntegerMatrix_IsParsedAsIs()
    {
        CreateBoardRequest? request = Deserialize("""{"state":[[0,1],[1,0]]}""");

        Assert.NotNull(request?.State);
        Assert.Equal([[0, 1], [1, 0]], request.State);
    }

    [Fact]
    public void Read_BooleanMatrix_IsNormalizedToZeroesAndOnes()
    {
        CreateBoardRequest? request = Deserialize("""{"state":[[true,false],[false,true]]}""");

        Assert.NotNull(request?.State);
        Assert.Equal([[1, 0], [0, 1]], request.State);
    }

    [Fact]
    public void Read_MixedIntegerAndBooleanCells_IsSupported()
    {
        CreateBoardRequest? request = Deserialize("""{"state":[[1,false],[true,0]]}""");

        Assert.Equal([[1, 0], [1, 0]], request?.State);
    }

    [Fact]
    public void Read_NullState_YieldsNullProperty()
    {
        CreateBoardRequest? request = Deserialize("""{"state":null}""");

        Assert.NotNull(request);
        Assert.Null(request.State);
    }

    [Theory]
    [InlineData("""{"state":[["1","0"]]}""")] // strings are not cells
    [InlineData("""{"state":[[0.5,1]]}""")]   // non-integer numbers are not cells
    [InlineData("""{"state":[0,1]}""")]       // rows must themselves be arrays
    [InlineData("""{"state":[null]}""")]      // null rows are rejected at parse time
    [InlineData("""{"state":"grid"}""")]      // the state must be an array
    public void Read_MalformedGrids_ThrowJsonException(string json)
    {
        Assert.Throws<JsonException>(() => Deserialize(json));
    }
}
