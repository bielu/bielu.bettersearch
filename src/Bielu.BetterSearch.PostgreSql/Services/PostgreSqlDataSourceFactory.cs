using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bielu.BetterSearch.PostgreSql.Services;

/// <summary>
/// One <see cref="NpgsqlDataSource"/>, which pools its connections, per distinct connection string, kept for the
/// factory's lifetime and disposed with it. An index with no connection string uses the data source registered in
/// the service collection, which the factory does not own.
/// </summary>
internal sealed class PostgreSqlDataSourceFactory(IPostgreSqlClientSettingsManager settingsManager, IServiceProvider services)
    : IClientFactoryAsync<NpgsqlDataSource>, IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<NpgsqlDataSource>> _dataSources = new(StringComparer.Ordinal);

    public Task<NpgsqlDataSource> GetOrCreateClientAsync(string indexName)
    {
        var connectionString = settingsManager.GetConnectionString(indexName);
        if (connectionString == null)
        {
            return Task.FromResult(services.GetService<NpgsqlDataSource>()
                                   ?? throw new InvalidOperationException(PostgreSqlClientSettingsManager.MissingConnectionFailure(null)));
        }

        var dataSource = _dataSources.GetOrAdd(connectionString,
            key => new Lazy<NpgsqlDataSource>(() => NpgsqlDataSource.Create(key), LazyThreadSafetyMode.ExecutionAndPublication));
        return Task.FromResult(dataSource.Value);
    }

    public void Dispose()
    {
        foreach (var dataSource in _dataSources.Values.Where(dataSource => dataSource.IsValueCreated))
        {
            dataSource.Value.Dispose();
        }

        _dataSources.Clear();
    }
}
