using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameOfLife.Tests.Integration;

/// <summary>
/// Boots the real API against a unique temp SQLite file so integration tests exercise
/// true file-backed persistence. Configuration overrides are appended after
/// appsettings.json and therefore win.
/// </summary>
public sealed class GameOfLifeApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _configurationOverrides;
    private readonly Action<IServiceCollection>? _configureTestServices;
    private readonly bool _ownsDatabaseFile;

    public string DbPath { get; }

    public GameOfLifeApiFactory()
        : this(dbPath: null)
    {
    }

    /// <param name="dbPath">SQLite file to use; a unique temp file when null.</param>
    /// <param name="configurationOverrides">Extra configuration keys (e.g. <c>Game:MaxFinalIterations</c>).</param>
    /// <param name="ownsDatabaseFile">
    /// When false, disposing the factory leaves the database file in place — used by the
    /// restart-persistence test, where a second factory reopens the same file and the
    /// test itself cleans up afterwards.
    /// </param>
    /// <param name="configureTestServices">
    /// Replaces registrations for a single test (e.g. a faulting repository). Keeping this
    /// alongside the configuration overrides means one code path configures every test host,
    /// instead of splitting setup between this factory and a <c>WithWebHostBuilder</c> call.
    /// </param>
    // Internal so xUnit's class-fixture activation only sees the public parameterless
    // constructor; tests in this assembly can still build customized factories directly.
    internal GameOfLifeApiFactory(
        string? dbPath,
        IDictionary<string, string?>? configurationOverrides = null,
        bool ownsDatabaseFile = true,
        Action<IServiceCollection>? configureTestServices = null)
    {
        DbPath = dbPath ?? Path.Combine(Path.GetTempPath(), $"gol-tests-{Guid.NewGuid():N}.db");
        _ownsDatabaseFile = ownsDatabaseFile;
        _configureTestServices = configureTestServices;

        _configurationOverrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:GameOfLife"] = $"Data Source={DbPath}",
        };

        if (configurationOverrides is not null)
        {
            foreach ((string key, string? value) in configurationOverrides)
            {
                _configurationOverrides[key] = value;
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(_configurationOverrides));

        if (_configureTestServices is not null)
        {
            builder.ConfigureTestServices(_configureTestServices);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && _ownsDatabaseFile)
        {
            DeleteDatabaseFiles(DbPath);
        }
    }

    /// <summary>
    /// Removes the SQLite database and its WAL/SHM side files. Connection pooling keeps
    /// file handles alive past host disposal on Windows, so pools are cleared first and
    /// deletion is retried briefly; a leaked temp file is tolerated rather than failing
    /// the test run.
    /// </summary>
    public static void DeleteDatabaseFiles(string dbPath)
    {
        SqliteConnection.ClearAllPools();

        foreach (string path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    break;
                }
                catch (IOException) when (attempt < 3)
                {
                    Thread.Sleep(100);
                }
                catch (IOException)
                {
                    // Final attempt failed: leaking a temp file is preferable to a flaky run.
                }
            }
        }
    }
}
