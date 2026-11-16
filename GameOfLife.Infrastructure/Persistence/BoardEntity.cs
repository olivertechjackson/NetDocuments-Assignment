namespace GameOfLife.Infrastructure.Persistence;

/// <summary>EF Core persistence model for a generation-0 board.</summary>
public sealed class BoardEntity
{
    public Guid Id { get; set; }

    public int Rows { get; set; }

    public int Cols { get; set; }

    /// <summary>Row-major cell states as a '0'/'1' string of length <c>Rows * Cols</c>.</summary>
    public string Cells { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
