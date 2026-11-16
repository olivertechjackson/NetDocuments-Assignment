using System.Net;
using System.Text.Json;
using GameOfLife.Core.Abstractions;
using GameOfLife.Core.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameOfLife.Tests.Integration;

public sealed class ExceptionHandlingTests
{
    private sealed class ThrowingRepository : IBoardRepository
    {
        public Task AddAsync(Board board, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated storage failure.");

        public Task<Board?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated storage failure.");
    }

    [Fact]
    public async Task UnhandledException_Returns500ProblemDetails_WithoutLeakingInternals()
    {
        using var factory = new GameOfLifeApiFactory(
            dbPath: null,
            configureTestServices: services =>
            {
                services.RemoveAll<IBoardRepository>();
                services.AddScoped<IBoardRepository, ThrowingRepository>();
            });
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/boards/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        string raw = await response.Content.ReadAsStringAsync();

        using JsonDocument document = JsonDocument.Parse(raw);
        JsonElement body = document.RootElement;

        Assert.Equal(500, body.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred.", body.GetProperty("title").GetString());

        // The exception message and stack trace must stay server-side.
        Assert.DoesNotContain("Simulated storage failure.", raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
    }
}
