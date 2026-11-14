using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.PostgreSql.Configuration;
using Bielu.BetterSearch.Services;
using FluentResults;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.PostgreSql.Services;

/// <summary>
/// Runs the query as a SQL condition on the index's table. Every hit matches the condition exactly and has the same
/// score, 1. Hits are ordered by <c>SortOrder</c> when given, then by id, so pages never overlap.
/// </summary>
public sealed class PostgreSqlSearchProviderAsync(
    IQueryTranslateServiceAsync<PostgreSqlSearchRequest> queryTranslateService,
    IClientFactoryAsync<NpgsqlDataSource> dataSourceFactory,
    IOptionsMonitor<PostgreSqlOptions> optionsMonitor) : ISearchProviderAsync
{
    /// <summary>Used when <c>PageSize</c> is not set, as for Elasticsearch.</summary>
    public const int DefaultPageSize = 10;

    /// <summary>The score of every hit: each one matches the condition exactly, so none ranks above another.</summary>
    internal const double FilterScore = 1.0;

    internal const string UnsupportedFacets = "The PostgreSQL provider does not support facets yet.";

    internal const string UnsupportedHighlighting = "The PostgreSQL provider does not support highlighting yet.";

    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query.FacetQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult>(UnsupportedFacets);
        }

        if (query.HighlightQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult>(UnsupportedHighlighting);
        }

        var prepared = await PrepareAsync(query).ConfigureAwait(false);
        if (prepared.IsFailed)
        {
            return prepared.ToResult<ISearchResult>();
        }

        var (table, filter, options) = prepared.Value;
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize > 0 ? query.PageSize : DefaultPageSize;
        var sortOrder = SortOrders.Ordered(query);
        if (sortOrder.IsFailed)
        {
            return sortOrder.ToResult<ISearchResult>();
        }

        try
        {
            var parameters = new SqlParameters(options.TextSearchConfiguration);
            var where = filter.Render(parameters);
            var orderBy = string.Join(", ", sortOrder.Value.Select(sort => OrderBy(parameters, sort.FieldName, sort.Desc)).Append("d.id COLLATE \"C\""));
            var limit = parameters.Add(pageSize, NpgsqlDbType.Integer);
            var offset = parameters.Add((long)(page - 1) * pageSize, NpgsqlDbType.Bigint);
            var dataSource = await dataSourceFactory.GetOrCreateClientAsync(query.Index).ConfigureAwait(false);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            // One snapshot for the page and the count, so the total matches the page.
            await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
            var properties = query.Properties?.Where(property => !string.IsNullOrWhiteSpace(property)).ToHashSet(StringComparer.Ordinal);
            var hits = new List<ISearchHit>();
            await using (var command = PostgreSqlCommands.Command(connection,
                             $"SELECT d.id, d.type, d.fields::text FROM {table} d WHERE {where} ORDER BY {orderBy} LIMIT {limit} OFFSET {offset}", parameters, transaction))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    hits.Add(MapHit(reader.GetString(0), reader.GetString(1), reader.GetString(2), properties));
                }
            }

            var total = await CountAsync(connection, transaction, table, filter, options, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Ok<ISearchResult>(new BaseSearchResults
            {
                Items = hits,
                Pagination = new Pagination
                {
                    TotalCount = total,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize)
                }
            });
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<ISearchResult>(Failure("PostgreSQL search", query.Index, e));
        }
    }

    public async Task<Result<SearchDocument?>> GetDocumentAsync(string index, string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(id))
        {
            return Result.Fail<SearchDocument?>("GetDocumentAsync needs an id.");
        }

        var options = optionsMonitor.TryCurrentValue();
        if (options.IsFailed)
        {
            return options.ToResult<SearchDocument?>();
        }

        var table = PostgreSqlNames.Table(options.Value.Schema, index);
        if (table.IsFailed)
        {
            return table.ToResult<SearchDocument?>();
        }

        try
        {
            var dataSource = await dataSourceFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = PostgreSqlCommands.Command(connection, $"SELECT d.type, d.fields::text FROM {table.Value} d WHERE d.id = $1");
            command.Parameters.Add(new NpgsqlParameter { Value = id, NpgsqlDbType = NpgsqlDbType.Text });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Result.Ok<SearchDocument?>(null);
            }

            return Result.Ok<SearchDocument?>(new SearchDocument
            {
                Id = id,
                Type = reader.GetString(0),
                Index = index,
                Fields = PostgreSqlDocument.ReadFields(reader.GetString(1))
            });
        }
        catch (Exception e) when (PostgreSqlCommands.IsMissingTable(e))
        {
            return Result.Ok<SearchDocument?>(null);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<SearchDocument?>(Failure("Getting a document", index, e));
        }
    }

    public async Task<Result<long>> CountAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = await PrepareAsync(query).ConfigureAwait(false);
        if (prepared.IsFailed)
        {
            return prepared.ToResult<long>();
        }

        var (table, filter, options) = prepared.Value;
        try
        {
            var dataSource = await dataSourceFactory.GetOrCreateClientAsync(query.Index).ConfigureAwait(false);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            return Result.Ok(await CountAsync(connection, null, table, filter, options, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<long>(Failure("PostgreSQL count", query.Index, e));
        }
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string table, PostgreSqlFilter filter,
        PostgreSqlOptions options, CancellationToken cancellationToken)
    {
        var parameters = new SqlParameters(options.TextSearchConfiguration);
        await using var command = PostgreSqlCommands.Command(connection, $"SELECT count(*) FROM {table} d WHERE {filter.Render(parameters)}", parameters, transaction);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>The table, the translated query and the options, or a failure that names what is wrong.</summary>
    private async Task<Result<(string Table, PostgreSqlFilter Filter, PostgreSqlOptions Options)>> PrepareAsync(ISearchQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Index))
        {
            return Result.Fail("The query needs an Index.");
        }

        // Invalid options fail with the validator's messages, each naming its setting, before anything is sent.
        var options = optionsMonitor.TryCurrentValue();
        if (options.IsFailed)
        {
            return options.ToResult();
        }

        var table = PostgreSqlNames.Table(options.Value.Schema, query.Index);
        if (table.IsFailed)
        {
            return table.ToResult();
        }

        var filter = await queryTranslateService.TranslateMainQueryAsync(query).ConfigureAwait(false);
        return filter.IsFailed
            ? filter.ToResult()
            : Result.Ok((table.Value, filter.Value.Filter, options.Value));
    }

    private static Error Failure(string action, string index, Exception e)
    {
        if (PostgreSqlCommands.IsMissingTable(e))
        {
            return new Error($"{action} failed: the index {index} does not exist.").CausedBy(e);
        }

        return PostgreSqlCommands.IsMissingFunction(e) && e.Message.Contains("levenshtein", StringComparison.Ordinal)
            ? new Error(PostgreSqlCommands.MissingFuzzyExtension).CausedBy(e)
            : new Error($"{action} on {index} failed: {PostgreSqlCommands.Describe(e)}").CausedBy(e);
    }

    /// <summary>
    /// Orders by the field's numbers, then its dates, then its strings by their UTF-8 bytes, each by the smallest
    /// value ascending and the largest descending, as Elasticsearch orders a field with several values. Documents
    /// without the field come last either way.
    /// </summary>
    private static string OrderBy(SqlParameters parameters, string field, bool desc)
    {
        var aggregate = desc ? "max" : "min";
        var direction = desc ? "DESC" : "ASC";
        string Key(string value, string condition) =>
            $"(SELECT {aggregate}({value}) FROM {parameters.Elements(field)} e WHERE {condition}) {direction} NULLS LAST";
        return string.Join(", ",
            Key("(e)::numeric", "jsonb_typeof(e) = 'number'"),
            Key(Queries.RangeFilter.Timestamp, $"{Queries.RangeFilter.Timestamp} IS NOT NULL"),
            Key("(e #>> '{}') COLLATE \"C\"", "jsonb_typeof(e) = 'string'"));
    }

    private static SearchHit MapHit(string id, string type, string fieldsJson, HashSet<string>? properties)
    {
        var fields = PostgreSqlDocument.ReadFields(fieldsJson);
        if (properties is { Count: > 0 })
        {
            fields = fields.Where(field => properties.Contains(field.Key)).ToDictionary();
        }

        return new SearchHit(new BaseSearchModel
        {
            Id = id,
            ContentType = type,
            CustomProperties = StoredFields.ToCustomProperties(fields)
        }, FilterScore);
    }
}
