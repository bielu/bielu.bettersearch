using Bielu.BetterSearch.Abstractions.Configuration;

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
    public static string? HttpUrlFailure(string optionsKey, string? index, string? connectionString) =>
        HttpUrlFailure(optionsKey, index, "ConnectionString", connectionString);

    /// <summary>
    /// A failure for each of <paramref name="additionalNodes"/> that is not an absolute http or https URL, naming its
    /// position as configuration does, such as <c>AdditionalNodes:1</c> for the second node.
    /// </summary>
    /// <remarks><c>index</c> is the <c>Indexes</c> entry the settings come from, or <c>null</c> when that is not known or they are the shared settings.</remarks>
    public static IEnumerable<string> AdditionalNodesFailures(string optionsKey, string? index, IEnumerable<string?>? additionalNodes) =>
        (additionalNodes ?? [])
            .Select((node, position) => HttpUrlFailure(optionsKey, index, $"AdditionalNodes:{position}", node))
            .OfType<string>();

    /// <summary>The connection string and then each of <paramref name="additionalNodes"/>, as URLs already checked by <see cref="HttpUrlFailure(string, string?, string?)"/> and <see cref="AdditionalNodesFailures"/>.</summary>
    public static IReadOnlyList<Uri> NodeUrls(string connectionString, IEnumerable<string>? additionalNodes) =>
        [new Uri(connectionString, UriKind.Absolute), .. (additionalNodes ?? []).Select(node => new Uri(node, UriKind.Absolute))];

    private static string? HttpUrlFailure(string optionsKey, string? index, string setting, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return $"{setting} is missing. {Where(optionsKey, index, setting)}";
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? null
            : $"{setting} is not an http or https URL. {Where(optionsKey, index, setting)}";
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

    /// <summary>A failure when the pool type is set to a value that is not one of <see cref="NodePoolType"/>'s.</summary>
    public static string? PoolTypeFailure(string optionsKey, string? index, NodePoolType? poolType) =>
        poolType is not { } type || Enum.IsDefined(type)
            ? null
            : $"PoolType {type} is not one of {string.Join(", ", Enum.GetNames<NodePoolType>())}. {Where(optionsKey, index, "PoolType")}";

    /// <summary>
    /// The connection setting failures of the nodes: the connection string, each of the additional nodes, an additional
    /// node whose scheme differs from the connection string's, which the Elasticsearch and OpenSearch clients reject, and
    /// the pool type.
    /// </summary>
    public static IEnumerable<string> NodeFailures(string optionsKey, string? index, string? connectionString,
        IEnumerable<string?>? additionalNodes, NodePoolType? poolType)
    {
        var nodes = (additionalNodes ?? []).ToList();
        var failures = new[] { HttpUrlFailure(optionsKey, index, connectionString) }
            .Concat(AdditionalNodesFailures(optionsKey, index, nodes))
            .Append(PoolTypeFailure(optionsKey, index, poolType))
            .OfType<string>()
            .ToList();
        if (failures.Count > 0)
        {
            return failures;
        }

        var scheme = new Uri(connectionString!, UriKind.Absolute).Scheme;
        return nodes
            .Select((node, position) => new Uri(node!, UriKind.Absolute).Scheme == scheme
                ? null
                : $"AdditionalNodes:{position} does not use {scheme}, as ConnectionString does, and every node must use the same scheme. {Where(optionsKey, index, $"AdditionalNodes:{position}")}")
            .OfType<string>();
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
