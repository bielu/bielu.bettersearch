using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Bielu.BetterSearch.PostgreSql.Tests;

/// <summary>
/// Starts PostgreSQL through the examples AppHost, once for the whole test run, and installs the
/// <c>fuzzystrmatch</c> extension that <c>FuzzyQuery</c> needs. Only the database runs as a container; the code
/// under test runs in the test process, so coverage is collected for it as usual.
/// </summary>
public sealed class PostgreSqlResource : IHostedService
{
    private const string ServerName = "postgres";
    private const string DatabaseName = "bettersearch";

    private readonly SemaphoreSlim _starting = new(1, 1);
    private DistributedApplication? _app;

    /// <summary>The connection string of the test database, once started.</summary>
    public string? ConnectionString { get; private set; }

    /// <summary>
    /// The connection string, starting the server first when the host has not yet. Startup validation reads the
    /// options before the host starts its hosted services, so the options ask for it through this.
    /// </summary>
    public string EnsureStarted()
    {
        StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        return ConnectionString!;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _starting.WaitAsync(cancellationToken);
        try
        {
            if (ConnectionString is not null)
            {
                return;
            }

            // A first run may need to pull the image.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));

            var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Bielu_BetterSearch_Examples_AppHost>(timeout.Token);
            // The AppHost runs every engine and UI; these tests only need the server, its database and its password.
            foreach (var other in builder.Resources.Where(r => r.Name is not (ServerName or DatabaseName) && r is not ParameterResource).ToList())
            {
                builder.Resources.Remove(other);
            }

            _app = await builder.BuildAsync(timeout.Token);
            await _app.StartAsync(timeout.Token);
            await _app.ResourceNotifications.WaitForResourceHealthyAsync(DatabaseName, timeout.Token);

            var connectionString = await _app.GetConnectionStringAsync(DatabaseName, timeout.Token)
                                   ?? throw new InvalidOperationException("The AppHost gave no connection string for the test database.");
            await using (var dataSource = NpgsqlDataSource.Create(connectionString))
            await using (var command = dataSource.CreateCommand("CREATE EXTENSION IF NOT EXISTS fuzzystrmatch"))
            {
                await command.ExecuteNonQueryAsync(timeout.Token);
            }

            // Set last, as a set connection string means the database is ready to use.
            ConnectionString = connectionString;
        }
        finally
        {
            _starting.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
