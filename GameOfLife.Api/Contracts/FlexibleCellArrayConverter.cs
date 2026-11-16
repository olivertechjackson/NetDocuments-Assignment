using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameOfLife.Api.Contracts;

/// <summary>
/// Reads a 2D grid whose cells may be JSON integers (0/1 expected) or booleans
/// (<c>true</c> → 1, <c>false</c> → 0), per the API contract that accepts either an
/// integer matrix or a boolean matrix. Shape and 0/1 range are validated afterwards by
/// <c>BoardStateValidator</c>; this converter only normalizes the token types.
/// </summary>
public sealed class FlexibleCellArrayConverter : JsonConverter<int[][]>
{
    public override int[][] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("The board state must be a 2D array of 0/1 integers or booleans.");
        }

        var rows = new List<int[]>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return [.. rows];
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Each board row must be an array of 0/1 integers or booleans.");
            }

            rows.Add(ReadRow(ref reader));
        }

        throw new JsonException("Unexpected end of JSON while reading the board state.");
    }

    private static int[] ReadRow(ref Utf8JsonReader reader)
    {
        var row = new List<int>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            row.Add(reader.TokenType switch
            {
                JsonTokenType.Number when reader.TryGetInt32(out int value) => value,
                JsonTokenType.True => 1,
                JsonTokenType.False => 0,
                _ => throw new JsonException("Board cells must be integers (0 or 1) or booleans."),
            });
        }

        return [.. row];
    }

    public override void Write(Utf8JsonWriter writer, int[][] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (int[] row in value)
        {
            writer.WriteStartArray();

            foreach (int cell in row)
            {
                writer.WriteNumberValue(cell);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }
}
