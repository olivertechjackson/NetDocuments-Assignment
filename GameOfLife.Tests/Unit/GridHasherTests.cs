using System.Security.Cryptography;
using System.Text;
using GameOfLife.Core.Hashing;
using GameOfLife.Tests.TestData;

namespace GameOfLife.Tests.Unit;

public sealed class GridHasherTests
{
    [Fact]
    public void ComputeHash_SameGrid_ProducesSameHash()
    {
        bool[] cells = Patterns.Flatten(Patterns.BlinkerVertical);

        string first = GridHasher.ComputeHash(cells, 3, 3);
        string second = GridHasher.ComputeHash(cells, 3, 3);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeHash_DifferentCells_ProducesDifferentHash()
    {
        string vertical = GridHasher.ComputeHash(Patterns.Flatten(Patterns.BlinkerVertical), 3, 3);
        string horizontal = GridHasher.ComputeHash(Patterns.Flatten(Patterns.BlinkerHorizontal), 3, 3);

        Assert.NotEqual(vertical, horizontal);
    }

    [Fact]
    public void ComputeHash_SameBitsDifferentDimensions_ProducesDifferentHash()
    {
        // Identical flattened bits "0110" shaped as 1x4 vs 2x2 must not collide,
        // because the hash input is prefixed with the grid dimensions.
        bool[] bits = [false, true, true, false];

        string oneByFour = GridHasher.ComputeHash(bits, 1, 4);
        string twoByTwo = GridHasher.ComputeHash(bits, 2, 2);

        Assert.NotEqual(oneByFour, twoByTwo);
    }

    [Fact]
    public void ComputeHash_MatchesCanonicalSha256OfDimensionPrefixedCellString()
    {
        bool[] bits = [false, true, true, false];

        string expected = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes("2x2:0110")));

        Assert.Equal(expected, GridHasher.ComputeHash(bits, 2, 2));
    }

    [Fact]
    public void ComputeHash_MismatchedLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => GridHasher.ComputeHash(new bool[5], 2, 2));
    }
}
