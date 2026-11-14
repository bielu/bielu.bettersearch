namespace Bielu.BetterSearch.Services;

/// <summary>
/// The settings checks the providers share, run by each provider's options validator at startup and whenever the
/// options are read, and again on each call for the connection settings. Each check returns a message that names the
/// setting to fix, or <c>null</c> when the setting is fine; <see cref="ThrowIfAny"/> turns the messages into the
/// <see cref="ArgumentException"/> the client factories turn into a failed <c>Result</c>. The connection settings
/// checked are already merged, so each check takes the <see cref="SettingSource"/> of the value: a message names only
/// the setting whose change reaches that value, or both the shared and the per-index one when the source is not known.
/// Messages never include a connection setting's value, which can hold credentials.
/// </summary>
internal static class ConnectionSettingsChecks
{
    /// <summary>A failure when the connection string is not an absolute http or https URL.</summary>
    public static string? HttpUrlFailure(string optionsKey, SettingSource source, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return $"ConnectionString is missing. {Where(optionsKey, source, "ConnectionString")}";
        }

        return Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? null
            : $"ConnectionString is not an http or https URL. {Where(optionsKey, source, "ConnectionString")}";
    }

    /// <summary>The connection string as an absolute http or https URL.</summary>
    public static Uri HttpUrl(string optionsKey, string? connectionString)
    {
        ThrowIfAny([HttpUrlFailure(optionsKey, SettingSource.Unknown, connectionString)]);
        return new Uri(connectionString!, UriKind.Absolute);
    }

    /// <summary>
    /// A failure when <paramref name="details"/>, or one of the <paramref name="credentials"/> taken from it, which
    /// <paramref name="authenticationType"/> needs, is missing.
    /// </summary>
    public static string? CredentialsFailure(string optionsKey, SettingSource source, object authenticationType, object? details,
        params (string Name, string? Value)[] credentials)
    {
        if (details is null)
        {
            return $"{authenticationType} authentication needs AuthenticationDetails. {Where(optionsKey, source, "AuthenticationDetails")}";
        }

        var missing = credentials.Where(credential => string.IsNullOrWhiteSpace(credential.Value))
            .Select(credential => $"AuthenticationDetails:{credential.Name}")
            .ToList();
        return missing.Count > 0
            ? $"{authenticationType} authentication needs {string.Join(" and ", missing)}. {Where(optionsKey, source, missing[0])}"
            : null;
    }

    /// <summary>The failure for an authentication type that is not one of <typeparamref name="TEnum"/>'s values.</summary>
    public static string UnknownAuthenticationTypeFailure<TEnum>(string optionsKey, SettingSource source, TEnum? authenticationType) where TEnum : struct, Enum =>
        $"AuthenticationType {authenticationType} is not one of {string.Join(", ", Enum.GetNames<TEnum>())}. {Where(optionsKey, source, "AuthenticationType")}";

    /// <summary>A failure when the limit at <paramref name="path"/>, such as <c>Bielu:BetterSearch:OpenSearch:MaxResultWindow</c>, is not greater than 0.</summary>
    public static string? GreaterThanZeroFailure(string path, long value) =>
        value > 0 ? null : $"{path} is {value}, but it must be greater than 0.";

    /// <summary>A failure when the time span at <paramref name="path"/> is negative.</summary>
    public static string? NotNegativeFailure(string path, TimeSpan value) =>
        value >= TimeSpan.Zero ? null : $"{path} is {value}, but it must not be negative.";

    /// <summary>
    /// The failure for options with no connection settings at all, which is what a missing configuration section, or
    /// one whose name is misspelled, binds to.
    /// </summary>
    public static string NoSettingsFailure(string optionsKey) =>
        $"No {optionsKey} settings were found. Add the {optionsKey} configuration section, with at least {optionsKey}:SharedIndexSettings:ConnectionString.";

    /// <summary>Throws an <see cref="ArgumentException"/> holding every one of <paramref name="failures"/> that is not <c>null</c>.</summary>
    public static void ThrowIfAny(IEnumerable<string?> failures)
    {
        var messages = failures.OfType<string>().ToList();
        if (messages.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", messages));
        }
    }

    // An Indexes entry takes AuthenticationDetails only with its own AuthenticationType, so the one-index alternative names both.
    private static string Where(string optionsKey, SettingSource source, string setting) => source switch
    {
        { Index: null } when setting.StartsWith("AuthenticationDetails", StringComparison.Ordinal) =>
            $"Set {optionsKey}:SharedIndexSettings:{setting}, or {optionsKey}:Indexes:<index>:AuthenticationType with {optionsKey}:Indexes:<index>:AuthenticationDetails for one index.",
        { Index: null } => $"Set {optionsKey}:SharedIndexSettings:{setting}, or {optionsKey}:Indexes:<index>:{setting} for one index.",
        { Inherited: true } => $"Set {optionsKey}:SharedIndexSettings:{setting}, which {optionsKey}:Indexes:{source.Index} inherits.",
        _ => $"Set {optionsKey}:Indexes:{source.Index}:{setting}.",
    };
}

/// <summary>
/// Where a checked connection setting comes from, so its failure names the one setting whose change reaches it: an
/// <c>Indexes</c> entry's own value wins over the shared one, and an entry that sets no value of its own inherits it.
/// </summary>
/// <param name="Index">The <c>Indexes</c> entry the merged settings are for, or <c>null</c> when that is not known or they are the shared settings.</param>
/// <param name="Inherited">Whether that entry takes the value from <c>SharedIndexSettings</c> rather than setting it.</param>
internal readonly record struct SettingSource(string? Index, bool Inherited)
{
    /// <summary>Not known to come from an <c>Indexes</c> entry, so a message names both places to set it.</summary>
    public static SettingSource Unknown => default;
}
