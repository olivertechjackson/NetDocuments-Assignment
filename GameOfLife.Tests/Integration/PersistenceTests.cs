using System.Net;
using System.Text.Json;
using GameOfLife.Tests.TestData;

namespace GameOfLife.Tests.Integration;

public sealed class PersistenceTests : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"gol-tests-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        GameOfLifeApiFactory.DeleteDatabaseFiles(_dbPath);
    }

    [Fact]
    public async Task Board_SurvivesApplicationRestart()
    {
        Guid id;

        // "First run" of the application: upload a board, then shut the host down.
        using (var firstRun = new GameOfLifeApiFactory(_dbPath, ownsDatabaseFile: false))
        using (HttpClient client = firstRun.CreateClient())
        {
            (id, _) = await client.CreateBoardAsync(Patterns.BlinkerVertical);
        }

        // "Second run": a brand-new host over the same SQLite file must still serve
        // the board (and EnsureCreated must be idempotent against the existing schema).
        using var secondRun = new GameOfLifeApiFactory(_dbPath, ownsDatabaseFile: false);
        using HttpClient restartedClient = secondRun.CreateClient();

        HttpResponseMessage baseline = await restartedClient.GetAsync($"/api/boards/{id}");

        Assert.Equal(HttpStatusCode.OK, baseline.StatusCode);

        JsonElement baselineBody = await baseline.ReadJsonAsync();

        Assert.Equal(0, baselineBody.GetProperty("generation").GetInt32());
        Assert.Equal(Patterns.BlinkerVertical, baselineBody.StateMatrix());

        HttpResponseMessage next = await restartedClient.GetAsync($"/api/boards/{id}/next");

        Assert.Equal(HttpStatusCode.OK, next.StatusCode);

        JsonElement nextBody = await next.ReadJsonAsync();

        Assert.Equal(1, nextBody.GetProperty("generation").GetInt32());
        Assert.Equal(Patterns.BlinkerHorizontal, nextBody.StateMatrix());
    }

    [Fact]
    public async Task RepeatedReads_AreDeterministicAndNeverMutateTheBaseline()
    {
        using var factory = new GameOfLifeApiFactory(_dbPath, ownsDatabaseFile: false);
        using HttpClient client = factory.CreateClient();

        (Guid id, _) = await client.CreateBoardAsync(Patterns.BlinkerVertical);

        string firstNext = await client.GetStringAsync($"/api/boards/{id}/next");
        string secondNext = await client.GetStringAsync($"/api/boards/{id}/next");

        // GET /next computes from the stored baseline, so repeated calls are byte-identical.
        Assert.Equal(firstNext, secondNext);

        HttpResponseMessage baseline = await client.GetAsync($"/api/boards/{id}");
        JsonElement baselineBody = await baseline.ReadJsonAsync();

        Assert.Equal(0, baselineBody.GetProperty("generation").GetInt32());
        Assert.Equal(Patterns.BlinkerVertical, baselineBody.StateMatrix());
    }
}
