using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GameOfLife.Tests.TestData;

namespace GameOfLife.Tests.Integration;

public sealed class BoardsApiTests : IClassFixture<GameOfLifeApiFactory>
{
    private readonly HttpClient _client;

    public BoardsApiTests(GameOfLifeApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static readonly Guid UnknownId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static void AssertProblemJson(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // --- POST /api/boards ---

    [Fact]
    public async Task PostBoard_ValidGrid_Returns201WithLocationAndEchoedState()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/boards",
            new { state = Patterns.BlinkerVertical });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        JsonElement body = await response.ReadJsonAsync();
        Guid id = body.GetProperty("id").GetGuid();

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(3, body.GetProperty("rows").GetInt32());
        Assert.Equal(3, body.GetProperty("cols").GetInt32());
        Assert.Equal(0, body.GetProperty("generation").GetInt32());
        Assert.Equal(Patterns.BlinkerVertical, body.StateMatrix());

        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/boards/{id}", response.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostBoard_StateCells_AreAlwaysSerializedAsNumericZeroOrOne()
    {
        // Internally cells are booleans; the contract requires 0/1 JSON numbers.
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/boards",
            new { state = Patterns.Block });

        JsonElement body = await response.ReadJsonAsync();

        foreach (JsonElement row in body.GetProperty("state").EnumerateArray())
        {
            foreach (JsonElement cell in row.EnumerateArray())
            {
                Assert.Equal(JsonValueKind.Number, cell.ValueKind);
                Assert.True(cell.GetInt32() is 0 or 1);
            }
        }
    }

    [Fact]
    public async Task PostBoard_BooleanMatrix_IsAcceptedAndNormalizedToIntegers()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/boards",
            new { state = new[] { new[] { true, false }, new[] { false, true } } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal([[1, 0], [0, 1]], body.StateMatrix());
    }

    [Theory]
    [InlineData("""{"state":null}""")]
    [InlineData("""{}""")]
    [InlineData("""{"state":[]}""")]
    [InlineData("""{"state":[[]]}""")]
    [InlineData("""{"state":[[0,1],[0]]}""")]
    [InlineData("""{"state":[[0,2]]}""")]
    [InlineData("""{"state":[[0,-1]]}""")]
    [InlineData("""{"state":[["0","1"]]}""")]
    public async Task PostBoard_InvalidPayload_Returns400ProblemDetails(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync("/api/boards", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertProblemJson(response);
    }

    [Fact]
    public async Task PostBoard_ExceedingMaxGridDimension_Returns400()
    {
        using var factory = new GameOfLifeApiFactory(
            dbPath: null,
            new Dictionary<string, string?> { ["Game:MaxGridDimension"] = "5" });
        using HttpClient client = factory.CreateClient();

        int[][] sixBySix = Enumerable.Range(0, 6).Select(_ => new int[6]).ToArray();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/boards", new { state = sixBySix });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertProblemJson(response);
    }

    // --- GET /api/boards/{id}/next ---

    [Fact]
    public async Task GetNext_Blinker_ReturnsHorizontalPhaseAsGenerationOne()
    {
        (Guid id, _) = await _client.CreateBoardAsync(Patterns.BlinkerVertical);

        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{id}/next");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal(1, body.GetProperty("generation").GetInt32());
        Assert.Equal(Patterns.BlinkerHorizontal, body.StateMatrix());
    }

    [Fact]
    public async Task GetNext_UnknownBoard_Returns404ProblemDetails()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{UnknownId}/next");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertProblemJson(response);
    }

    // --- GET /api/boards/{id}/states/{steps} ---

    [Fact]
    public async Task GetStates_BlinkerTwoSteps_ReturnsInitialPatternAsGenerationTwo()
    {
        (Guid id, _) = await _client.CreateBoardAsync(Patterns.BlinkerVertical);

        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{id}/states/2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(2, body.GetProperty("generation").GetInt32());
        Assert.Equal(Patterns.BlinkerVertical, body.StateMatrix());
    }

    [Fact]
    public async Task GetStates_StillLifeAtMaxAllowedSteps_ShortCircuitsAndReportsRequestedGeneration()
    {
        (Guid id, _) = await _client.CreateBoardAsync(Patterns.Block);

        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{id}/states/10000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(10_000, body.GetProperty("generation").GetInt32());
        Assert.Equal(Patterns.Block, body.StateMatrix());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_001)]
    public async Task GetStates_OutOfRangeSteps_Returns400ProblemDetails(int steps)
    {
        (Guid id, _) = await _client.CreateBoardAsync(Patterns.Block);

        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{id}/states/{steps}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertProblemJson(response);
    }

    [Fact]
    public async Task GetStates_NonIntegerSteps_Returns404BecauseRouteDoesNotMatch()
    {
        (Guid id, _) = await _client.CreateBoardAsync(Patterns.Block);

        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{id}/states/abc");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStates_UnknownBoard_Returns404()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{UnknownId}/states/3");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertProblemJson(response);
    }

    // --- GET /api/boards/{id} ---

    [Fact]
    public async Task GetById_ReturnsGenerationZeroBaseline()
    {
        (Guid id, _) = await _client.CreateBoardAsync(Patterns.ToadPhase1);

        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(0, body.GetProperty("generation").GetInt32());
        Assert.Equal(4, body.GetProperty("rows").GetInt32());
        Assert.Equal(4, body.GetProperty("cols").GetInt32());
        Assert.Equal(Patterns.ToadPhase1, body.StateMatrix());
    }

    [Fact]
    public async Task GetById_UnknownBoard_Returns404()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{UnknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertProblemJson(response);
    }
}
