using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace GameOfLife.Core.Hashing;

/// <summary>
/// Canonical grid fingerprint used for cycle detection: the SHA-256 (uppercase hex) of the
/// ASCII string <c>"{rows}x{cols}:"</c> followed by one '0'/'1' character per cell in
/// row-major order. The dimension prefix ensures that grids with identical flattened bits
/// but different shapes (e.g. 1x4 vs 2x2) never collide.
/// </summary>
public static class GridHasher
{
    public static string ComputeHash(ReadOnlySpan<bool> cells, int rows, int cols)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(cols, 1);

        if (cells.Length != rows * cols)
        {
            throw new ArgumentException(
                $"Cell span length {cells.Length} does not match dimensions {rows}x{cols}.",
                nameof(cells));
        }

        Span<byte> prefix = stackalloc byte[32];
        int prefixLength = Encoding.ASCII.GetBytes($"{rows}x{cols}:", prefix);

        int totalLength = prefixLength + cells.Length;
        byte[] rented = ArrayPool<byte>.Shared.Rent(totalLength);

        try
        {
            prefix[..prefixLength].CopyTo(rented);

            for (int i = 0; i < cells.Length; i++)
            {
                rented[prefixLength + i] = cells[i] ? (byte)'1' : (byte)'0';
            }

            Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(rented.AsSpan(0, totalLength), hash);

            return Convert.ToHexString(hash);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
