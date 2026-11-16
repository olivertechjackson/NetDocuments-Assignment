using GameOfLife.Infrastructure.Persistence;
using GameOfLife.Tests.TestData;

namespace GameOfLife.Tests.Unit;

public sealed class CellSerializerTests
{
    [Fact]
    public void SerializeThenDeserialize_RoundTripsExactly()
    {
        bool[] cells = Patterns.Flatten(Patterns.GliderAt(1, 1, 10, 10));

        string text = CellSerializer.Serialize(cells);
        bool[] roundTripped = CellSerializer.Deserialize(text, 10, 10);

        Assert.Equal(cells, roundTripped);
    }

    [Fact]
    public void Serialize_ProducesZeroOneCharacters()
    {
        string text = CellSerializer.Serialize([true, false, true]);

        Assert.Equal("101", text);
    }

    [Fact]
    public void Deserialize_WrongLength_Throws()
    {
        Assert.Throws<InvalidDataException>(() => CellSerializer.Deserialize("0101", rows: 3, cols: 3));
    }

    [Fact]
    public void Deserialize_InvalidCharacter_Throws()
    {
        Assert.Throws<InvalidDataException>(() => CellSerializer.Deserialize("01x1", rows: 2, cols: 2));
    }
}
