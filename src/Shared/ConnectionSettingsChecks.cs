namespace Bielu.BetterSearch.Services;

/// <summary>
/// The connection setting checks the providers share. Each throws an <see cref="ArgumentException"/> whose message
/// names the setting to fix, which the client factories turn into a failed <c>Result</c>. The settings checked are
/// already merged, so a message names both the shared setting and the per-index one. Messages never include a
/// setting's value, which can hold credentials.
/// </summary>
internal static class ConnectionSettingsChecks
{
    /// <summary>The connection string as an absolute http or https URL.</summary>
    public static Uri HttpUrl(string optionsKey, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException($"ConnectionString is missing. {Where(optionsKey, "ConnectionString")}");
        }

        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                $"ConnectionString is not an http or https URL. {Where(optionsKey, "ConnectionString")}");
        }

        return uri;
    }

    /// <summary>The authentication details, which <paramref name="authenticationType"/> needs.</summary>
    public static T Details<T>(string optionsKey, object authenticationType, T? details) where T : class =>
        details ?? throw new ArgumentException(
            $"{authenticationType} authentication needs AuthenticationDetails. {Where(optionsKey, "AuthenticationDetails")}");

    /// <summary>Checks that every one of <paramref name="credentials"/>, which <paramref name="authenticationType"/> needs, is set.</summary>
    public static void Credentials(string optionsKey, object authenticationType, params (string Name, string? Value)[] credentials)
    {
        var missing = credentials.Where(credential => string.IsNullOrWhiteSpace(credential.Value))
            .Select(credential => $"AuthenticationDetails:{credential.Name}")
            .ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException(
                $"{authenticationType} authentication needs {string.Join(" and ", missing)}. {Where(optionsKey, missing[0])}");
        }
    }

    /// <summary>The exception for an authentication type that is not one of <typeparamref name="TEnum"/>'s values.</summary>
    public static ArgumentException UnknownAuthenticationType<TEnum>(string optionsKey, TEnum? authenticationType) where TEnum : struct, Enum =>
        new($"AuthenticationType {authenticationType} is not one of {string.Join(", ", Enum.GetNames<TEnum>())}. {Where(optionsKey, "AuthenticationType")}");

    private static string Where(string optionsKey, string setting) =>
        $"Set {optionsKey}:SharedIndexSettings:{setting}, or {optionsKey}:Indexes:<index>:{setting} for one index.";
}
