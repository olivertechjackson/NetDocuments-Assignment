using System.Net;
using System.Text.Json;
using GameOfLife.Tests.TestData;

namespace GameOfLife.Tests.Integration;

public sealed class FinalEndpointTests : IClassFixture<GameOfLifeApiFactory>
{
    private readonly HttpClient _client;

    public FinalEndpointTests(GameOfLifeApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetFinal_Block_ReturnsStableAtGenerationZero_WithoutCyclePeriodProperty()
    {
        (Guid id, _) = await _client.CreateBoardAsync(Patterns.Block);

        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{id}/final");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal("Stable", body.GetProperty("status").GetString());
        Assert.Equal(0, body.GetProperty("generation").GetInt32());
        Assert.Equal(Patterns.Block, body.StateMatrix());

        // Stable results omit cyclePeriod from the JSON entirely (WhenWritingNull).
        Assert.False(body.TryGetProperty("cyclePeriod", out _));
    }

    [Fact]
    public async Task GetFinal_Blinker_ReturnsOscillatingWithPeriodTwo_AtCycleEntryGeneration()
    {
        (Guid id, _) = await _client.CreateBoardAsync(Patterns.BlinkerVertical);

        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{id}/final");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal("Oscillating", body.GetProperty("status").GetString());
        Assert.Equal(0, body.GetProperty("generation").GetInt32());
        Assert.Equal(2, body.GetProperty("cyclePeriod").GetInt32());
        Assert.Equal(Patterns.BlinkerVertical, body.StateMatrix());
    }

    [Fact]
    public async Task GetFinal_SingleCell_ExtinctionReportsStableAtGenerationOne()
    {
        (Guid id, _) = await _client.CreateBoardAsync(Patterns.SingleCell);

        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{id}/final");

        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal("Stable", body.GetProperty("status").GetString());
        Assert.Equal(1, body.GetProperty("generation").GetInt32());
        Assert.Equal([[0, 0, 0], [0, 0, 0], [0, 0, 0]], body.StateMatrix());
    }

    [Fact]
    public async Task GetFinal_GliderWithTinyIterationBudget_Returns422ProblemDetails()
    {
        using var factory = new GameOfLifeApiFactory(
            dbPath: null,
            new Dictionary<string, string?> { ["Game:MaxFinalIterations"] = "3" });
        using HttpClient client = factory.CreateClient();

        (Guid id, _) = await client.CreateBoardAsync(Patterns.GliderAt(1, 1, 10, 10));

        HttpResponseMessage response = await client.GetAsync($"/api/boards/{id}/final");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(422, body.GetProperty("status").GetInt32());
        Assert.Equal("Board did not reach a final state.", body.GetProperty("title").GetString());
        Assert.Contains("3 iterations", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task GetFinal_UnknownBoard_Returns404()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/boards/{Guid.NewGuid()}/final");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
