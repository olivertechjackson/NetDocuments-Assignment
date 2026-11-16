namespace GameOfLife.Tests.TestData;

/// <summary>Canonical Game of Life patterns used across unit and integration tests.</summary>
public static class Patterns
{
    /// <summary>2x2 block — the simplest still life (every live cell has exactly 3 neighbors).</summary>
    public static int[][] Block =>
    [
        [1, 1],
        [1, 1],
    ];

    /// <summary>Period-2 oscillator, vertical phase, centered in a 3x3 grid.</summary>
    public static int[][] BlinkerVertical =>
    [
        [0, 1, 0],
        [0, 1, 0],
        [0, 1, 0],
    ];

    /// <summary>The blinker one generation after <see cref="BlinkerVertical"/>.</summary>
    public static int[][] BlinkerHorizontal =>
    [
        [0, 0, 0],
        [1, 1, 1],
        [0, 0, 0],
    ];

    /// <summary>Period-2 oscillator whose two phases both fit a 4x4 bounding box.</summary>
    public static int[][] ToadPhase1 =>
    [
        [0, 0, 0, 0],
        [0, 1, 1, 1],
        [1, 1, 1, 0],
        [0, 0, 0, 0],
    ];

    /// <summary>The toad one generation after <see cref="ToadPhase1"/>.</summary>
    public static int[][] ToadPhase2 =>
    [
        [0, 0, 1, 0],
        [1, 0, 0, 1],
        [1, 0, 0, 1],
        [0, 1, 0, 0],
    ];

    /// <summary>3x3 single live cell — dies of underpopulation in one generation.</summary>
    public static int[][] SingleCell =>
    [
        [0, 0, 0],
        [0, 1, 0],
        [0, 0, 0],
    ];

    /// <summary>
    /// A grid of the given size with a glider's 3x3 bounding box placed at
    /// (<paramref name="top"/>, <paramref name="left"/>). The glider travels
    /// one cell down-right every 4 generations until it hits the boundary.
    /// </summary>
    public static int[][] GliderAt(int top, int left, int rows, int cols)
    {
        int[][] gliderShape =
        [
            [0, 1, 0],
            [0, 0, 1],
            [1, 1, 1],
        ];

        var grid = new int[rows][];

        for (int r = 0; r < rows; r++)
        {
            grid[r] = new int[cols];
        }

        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                grid[top + r][left + c] = gliderShape[r][c];
            }
        }

        return grid;
    }

    /// <summary>Flattens a jagged 0/1 grid into the domain's row-major bool array.</summary>
    public static bool[] Flatten(int[][] grid)
    {
        int rows = grid.Length;
        int cols = grid[0].Length;
        var cells = new bool[rows * cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                cells[r * cols + c] = grid[r][c] == 1;
            }
        }

        return cells;
    }
}
