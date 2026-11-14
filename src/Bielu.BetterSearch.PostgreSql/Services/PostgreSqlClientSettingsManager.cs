using Bielu.BetterSearch.PostgreSql.Configuration;
using Bielu.BetterSearch.Services;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bielu.BetterSearch.PostgreSql.Services;

internal interface IPostgreSqlClientSettingsManager
{
    /// <summary>
    /// The connection string for <paramref name="indexName"/>: its <c>Indexes</c> entry's, else the shared one, or
    /// <c>null</c> when neither is set, to use the registered <see cref="NpgsqlDataSource"/>.
    /// </summary>
    string? GetConnectionString(string indexName);
}

internal sealed class PostgreSqlClientSettingsManager(IOptionsMonitor<PostgreSqlOptions> optionsMonitor) : IPostgreSqlClientSettingsManager
{
    public string? GetConnectionString(string indexName)
    {
        var options = optionsMonitor.CurrentValue;
        var connectionString = options.Indexes != null && options.Indexes.TryGetValue(indexName, out var indexSpecific) &&
                               !string.IsNullOrWhiteSpace(indexSpecific?.ConnectionString)
            ? indexSpecific.ConnectionString
            : options.SharedIndexSettings?.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        ConnectionSettingsChecks.ThrowIfAny(ConnectionFailures(connectionString, null));
        return connectionString;
    }

    /// <summary>
    /// The failures of <paramref name="connectionString"/>, each naming the setting to fix and never the value, which
    /// can hold a password: it must parse as an Npgsql connection string and name a host.
    /// </summary>
    /// <remarks><c>index</c> is the <c>Indexes</c> entry the setting comes from, or <c>null</c> for the shared settings.</remarks>
    internal static IEnumerable<string> ConnectionFailures(string connectionString, string? index)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (Exception e) when (e is ArgumentException or FormatException or InvalidCastException)
        {
            return [$"ConnectionString is not a valid Npgsql connection string. {Where(index)}"];
        }

        return string.IsNullOrWhiteSpace(builder.Host)
            ? [$"ConnectionString names no Host. {Where(index)}"]
            : [];
    }

    internal static string MissingConnectionFailure(string? index) =>
        $"ConnectionString is missing, and no NpgsqlDataSource is registered. {Where(index)}";

    private static string Where(string? index)
    {
        var key = PostgreSqlOptions.PostgreSqlOptionsKey;
        return index is null
            ? $"Set {key}:SharedIndexSettings:ConnectionString, or {key}:Indexes:<index>:ConnectionString for one index."
            : $"Set {key}:Indexes:{index}:ConnectionString, or {key}:SharedIndexSettings:ConnectionString for every index.";
    }
}
