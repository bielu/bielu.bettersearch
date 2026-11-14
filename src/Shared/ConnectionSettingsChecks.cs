namespace Bielu.BetterSearch.Services;

/// <summary>
/// The settings checks the providers share, run by each provider's options validator at startup and whenever the
/// options are read, and again on each call for the connection settings. Each check returns a message that names the
/// setting to fix, or <c>null</c> when the setting is fine; <see cref="ThrowIfAny"/> turns the messages into the
/// <see cref="ArgumentException"/> the client factories turn into a failed <c>Result</c>. The connection settings
/// checked are already merged, so a message names both the shared setting and the per-index one. Messages never
/// include a connection setting's value, which can hold credentials.
/// </summary>
internal static class ConnectionSettingsChecks
{
    /// <summary>A failure when the connection string is not an absolute http or https URL.</summary>
    /// <remarks><c>index</c> is the <c>Indexes</c> entry the settings come from, or <c>null</c> when that is not known or they are the shared settings.</remarks>
    public static string? HttpUrlFailure(string optionsKey, string? index, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return $"ConnectionString is missing. {Where(optionsKey, index, "ConnectionString")}";
        }

        return Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? null
            : $"ConnectionString is not an http or https URL. {Where(optionsKey, index, "ConnectionString")}";
    }

    /// <summary>The connection string as an absolute http or https URL.</summary>
    public static Uri HttpUrl(string optionsKey, string? connectionString)
    {
        ThrowIfAny([HttpUrlFailure(optionsKey, null, connectionString)]);
        return new Uri(connectionString!, UriKind.Absolute);
    }

    /// <summary>
    /// A failure when <paramref name="details"/>, or one of the <paramref name="credentials"/> taken from it, which
    /// <paramref name="authenticationType"/> needs, is missing.
    /// </summary>
    public static string? CredentialsFailure(string optionsKey, string? index, object authenticationType, object? details,
        params (string Name, string? Value)[] credentials)
    {
        if (details is null)
        {
            return $"{authenticationType} authentication needs AuthenticationDetails. {Where(optionsKey, index, "AuthenticationDetails")}";
        }

        var missing = credentials.Where(credential => string.IsNullOrWhiteSpace(credential.Value))
            .Select(credential => $"AuthenticationDetails:{credential.Name}")
            .ToList();
        return missing.Count > 0
            ? $"{authenticationType} authentication needs {string.Join(" and ", missing)}. {Where(optionsKey, index, missing[0])}"
            : null;
    }

    /// <summary>The failure for an authentication type that is not one of <typeparamref name="TEnum"/>'s values.</summary>
    public static string UnknownAuthenticationTypeFailure<TEnum>(string optionsKey, string? index, TEnum? authenticationType) where TEnum : struct, Enum =>
        $"AuthenticationType {authenticationType} is not one of {string.Join(", ", Enum.GetNames<TEnum>())}. {Where(optionsKey, index, "AuthenticationType")}";

    /// <summary>
    /// Failures in the TLS settings: a <paramref name="certificateFingerprint"/> that is not a SHA-256 fingerprint, 64 hex
    /// digits with or without colons, or one set together with <paramref name="skipCertificateValidation"/>.
    /// </summary>
    public static IEnumerable<string> TlsFailures(string optionsKey, string? index, string? certificateFingerprint, bool? skipCertificateValidation)
    {
        if (string.IsNullOrWhiteSpace(certificateFingerprint))
        {
            yield break;
        }

        if (NormalizeFingerprint(certificateFingerprint) is null)
        {
            yield return $"CertificateFingerprint is not a SHA-256 fingerprint: it needs 64 hex digits, with or without colons. {Where(optionsKey, index, "CertificateFingerprint")}";
        }

        if (skipCertificateValidation == true)
        {
            yield return $"CertificateFingerprint and DevelopmentOnlySkipCertificateValidation can't both be set: a skipped validation never checks the fingerprint. Remove one. {Where(optionsKey, index, "DevelopmentOnlySkipCertificateValidation")}";
        }
    }

    /// <summary>
    /// A failure when the <c>Indexes</c> entry <paramref name="index"/> sets a TLS setting without its own connection
    /// string: the TLS settings go with the connection string, so the entry would use the shared ones and ignore its own.
    /// </summary>
    public static string? TlsWithoutConnectionStringFailure(string optionsKey, string index, string? connectionString,
        string? certificateFingerprint, bool? skipCertificateValidation)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var setting = !string.IsNullOrWhiteSpace(certificateFingerprint) ? "CertificateFingerprint"
            : skipCertificateValidation is not null ? "DevelopmentOnlySkipCertificateValidation"
            : null;
        return setting is null
            ? null
            : $"{optionsKey}:Indexes:{index}:{setting} only applies with the index's own ConnectionString; without one, the index uses the shared server and its TLS settings. Set {optionsKey}:Indexes:{index}:ConnectionString, or set {setting} in {optionsKey}:SharedIndexSettings.";
    }

    /// <summary>The fingerprint as 64 upper case hex digits without colons, or <c>null</c> when it is not a SHA-256 fingerprint.</summary>
    public static string? NormalizeFingerprint(string fingerprint)
    {
        var hex = fingerprint.Trim().Replace(":", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return hex.Length == 64 && hex.All(Uri.IsHexDigit) ? hex : null;
    }

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

    private static string Where(string optionsKey, string? index, string setting) => index is null
        ? $"Set {optionsKey}:SharedIndexSettings:{setting}, or {optionsKey}:Indexes:<index>:{setting} for one index."
        : $"Set {optionsKey}:Indexes:{index}:{setting}, or {optionsKey}:SharedIndexSettings:{setting} for every index.";
}
