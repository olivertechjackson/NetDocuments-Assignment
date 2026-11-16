namespace GameOfLife.Infrastructure.Persistence;

/// <summary>
/// Converts between the domain's row-major <see cref="bool"/> array and the compact
/// '0'/'1' text stored in SQLite. Text keeps the column human-readable when inspecting
/// the database; a bit-packed BLOB would be ~8x smaller but opaque.
/// </summary>
public static class CellSerializer
{
    public static string Serialize(ReadOnlySpan<bool> cells)
    {
        var chars = new char[cells.Length];

        for (int i = 0; i < cells.Length; i++)
        {
            chars[i] = cells[i] ? '1' : '0';
        }

        return new string(chars);
    }

    public static bool[] Deserialize(string text, int rows, int cols)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length != rows * cols)
        {
            throw new InvalidDataException(
                $"Persisted cell data has length {text.Length} but dimensions {rows}x{cols} require {rows * cols}.");
        }

        var cells = new bool[text.Length];

        for (int i = 0; i < text.Length; i++)
        {
            cells[i] = text[i] switch
            {
                '1' => true,
                '0' => false,
                var invalid => throw new InvalidDataException(
                    $"Persisted cell data contains invalid character '{invalid}' at index {i}; expected '0' or '1'."),
            };
        }

        return cells;
    }
}
