using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Typesense.Services;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Typesense.Configuration;

/// <summary>
/// Checks the connection settings, the request timeout and the schema check interval, at startup and whenever the options are read, listing
/// every setting that is wrong and naming each one. The connection settings are checked as each call checks them, after
/// merging each <c>Indexes</c> entry with the shared settings.
/// </summary>
internal sealed class TypesenseOptionsValidator : IValidateOptions<TypesenseOptions>
{
    public ValidateOptionsResult Validate(string? name, TypesenseOptions options)
    {
        var key = TypesenseOptions.TypesenseOptionsKey;
        var failures = ConnectionFailures(options)
            .Concat(new[]
            {
                ConnectionSettingsChecks.NotNegativeFailure($"{key}:{nameof(TypesenseOptions.SchemaCheckInterval)}", options.SchemaCheckInterval),
                ConnectionSettingsChecks.NotNegativeFailure($"{key}:{nameof(TypesenseOptions.RequestTimeout)}", options.RequestTimeout)
                    ?? ConnectionSettingsChecks.MaxRequestTimeoutFailure($"{key}:{nameof(TypesenseOptions.RequestTimeout)}", options.RequestTimeout),
            }.OfType<string>())
            .ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// Options with no connection string or <c>Indexes</c> entry at all fail once, naming the section. Otherwise the
    /// shared settings are checked as an index not listed under <c>Indexes</c> gets them, unless they set no connection
    /// string and every index is listed with its own. An <c>Indexes</c> entry with its own connection string is checked
    /// after merging; one without has the shared settings, which are checked already.
    /// </summary>
    private static IEnumerable<string> ConnectionFailures(TypesenseOptions options)
    {
        var shared = options.SharedIndexSettings ?? new TypesenseIndexSettings();
        var indexes = options.Indexes ?? [];
        var sharedConnection = !string.IsNullOrWhiteSpace(shared.ConnectionString);
        if (string.IsNullOrEmpty(shared.ConnectionString) && indexes.Count == 0)
        {
            return [ConnectionSettingsChecks.NoSettingsFailure(TypesenseOptions.TypesenseOptionsKey)];
        }

        var withOwnConnection = indexes.Where(index => !string.IsNullOrWhiteSpace(index.Value?.ConnectionString)).ToList();
        var sharedFailures = sharedConnection || indexes.Count == 0 || withOwnConnection.Count < indexes.Count
            ? TypesenseClientSettingsManager.ConnectionFailures(shared, SettingSource.Unknown)
            : [];
        return sharedFailures.Concat(withOwnConnection.SelectMany(index => TypesenseClientSettingsManager.ConnectionFailures(
            TypesenseClientSettingsManager.MergeSettings(index.Value, shared), new SettingSource(index.Key, Inherited: false))));
    }
}
