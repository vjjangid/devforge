using Npgsql;

namespace DevForge.IntegrationTests.Infrastructure;

/// <summary>
/// Creates a throwaway PostgreSQL database for one test class and drops it afterwards.
/// Point <c>DEVFORGE_TEST_CONNECTION</c> at any server where the user may create databases;
/// by default it uses the Compose postgres service (<c>docker compose up -d postgres</c>).
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string ConnectionVariable = "DEVFORGE_TEST_CONNECTION";
    private const string DefaultServer = "Host=localhost;Port=5440;Username=devforge;Password=devforge";
    private const string MaintenanceDatabase = "postgres";

    private readonly string _server = Environment.GetEnvironmentVariable(ConnectionVariable) ?? DefaultServer;
    private readonly string _database = $"devforge_test_{Guid.NewGuid():N}";

    public string ConnectionString => new NpgsqlConnectionStringBuilder(_server) { Database = _database }.ConnectionString;

    public Task InitializeAsync() => ExecuteOnServerAsync($"CREATE DATABASE \"{_database}\"");

    public Task DisposeAsync() => ExecuteOnServerAsync($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)");

    private async Task ExecuteOnServerAsync(string sql)
    {
        var maintenance = new NpgsqlConnectionStringBuilder(_server) { Database = MaintenanceDatabase, Pooling = false };

        await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
