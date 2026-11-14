using Bielu.BetterSearch.PostgreSql.Services;
using Bielu.BetterSearch.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bielu.BetterSearch.PostgreSql.Configuration;

/// <summary>
/// Checks the connection settings, the schema and the text search configuration, at startup and whenever the options
/// are read, listing every setting that is wrong and naming each one.
/// </summary>
internal sealed class PostgreSqlOptionsValidator(IServiceProvider services) : IValidateOptions<PostgreSqlOptions>
{
    public ValidateOptionsResult Validate(string? name, PostgreSqlOptions options)
    {
        var key = PostgreSqlOptions.PostgreSqlOptionsKey;
        var failures = ConnectionFailures(options)
            .Concat(new[]
            {
                PostgreSqlNames.IdentifierFailure($"{key}:{nameof(PostgreSqlOptions.Schema)}", options.Schema),
                PostgreSqlNames.TextSearchConfigurationFailure($"{key}:{nameof(PostgreSqlOptions.TextSearchConfiguration)}", options.TextSearchConfiguration),
            }.OfType<string>())
            .ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// Options with no connection string, no <c>Indexes</c> entry and no registered <see cref="NpgsqlDataSource"/>
    /// fail once, naming the section. Otherwise every connection string that is set must parse.
    /// </summary>
    private IEnumerable<string> ConnectionFailures(PostgreSqlOptions options)
    {
        var shared = options.SharedIndexSettings ?? new PostgreSqlIndexSettings();
        var indexes = options.Indexes ?? [];
        var hasDataSource = services.GetService<NpgsqlDataSource>() != null;
        var withOwnConnection = indexes.Where(index => !string.IsNullOrWhiteSpace(index.Value?.ConnectionString)).ToList();
        if (string.IsNullOrWhiteSpace(shared.ConnectionString) && !hasDataSource)
        {
            if (indexes.Count == 0)
            {
                return [ConnectionSettingsChecks.NoSettingsFailure(PostgreSqlOptions.PostgreSqlOptionsKey)];
            }

            // An index without a connection string of its own uses the shared one, which is missing.
            if (withOwnConnection.Count < indexes.Count)
            {
                return [PostgreSqlClientSettingsManager.MissingConnectionFailure(null)];
            }
        }

        var failures = new List<string>();
        if (!string.IsNullOrWhiteSpace(shared.ConnectionString))
        {
            failures.AddRange(PostgreSqlClientSettingsManager.ConnectionFailures(shared.ConnectionString, null));
        }

        failures.AddRange(withOwnConnection.SelectMany(index =>
            PostgreSqlClientSettingsManager.ConnectionFailures(index.Value.ConnectionString, index.Key)));
        return failures;
    }
}
