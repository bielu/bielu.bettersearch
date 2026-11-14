namespace Bielu.BetterSearch.PostgreSql.Services;

/// <summary>A translated query: the condition the provider renders into SQL.</summary>
public sealed record PostgreSqlSearchRequest
{
    internal PostgreSqlSearchRequest(PostgreSqlFilter filter)
    {
        Filter = filter;
    }

    internal PostgreSqlFilter Filter { get; }
}
