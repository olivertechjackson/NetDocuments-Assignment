using System.Net.Http.Json;
using System.Text.Json;

namespace GameOfLife.Tests.Integration;

internal static class ApiTestHelpers
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        string content = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(content);

        return document.RootElement.Clone();
    }

    public static async Task<(Guid Id, JsonElement Body)> CreateBoardAsync(this HttpClient client, int[][] state)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/boards", new { state });

        response.EnsureSuccessStatusCode();

        JsonElement body = await response.ReadJsonAsync();

        return (body.GetProperty("id").GetGuid(), body);
    }

    /// <summary>Deserializes the response's "state" property into a jagged 0/1 matrix.</summary>
    public static int[][] StateMatrix(this JsonElement body) =>
        body.GetProperty("state").Deserialize<int[][]>()!;
}
