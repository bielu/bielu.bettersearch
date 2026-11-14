using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.PostgreSql.Configuration;
using Bielu.BetterSearch.Services;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.PostgreSql.Services;

/// <summary>
/// Writes to one table per index. Each write commits before it returns, so a document is searchable once a write
/// succeeds. A missing table is created, with the provider's columns, by the first write to it.
/// </summary>
public sealed class PostgreSqlIndexingProviderAsync(
    IClientFactoryAsync<NpgsqlDataSource> dataSourceFactory,
    IOptionsMonitor<PostgreSqlOptions> optionsMonitor,
    ILogger<PostgreSqlIndexingProviderAsync> logger) : IIndexingProviderAsync
{
    /// <summary>The most documents one insert sends; a larger write sends several, in one transaction.</summary>
    internal const int InsertBatchSize = 1000;

    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
        (await IndexMultipleDocumentsAsync([document], cancellationToken).ConfigureAwait(false)).ToResult();

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var searchDocuments = documents.ToList();
        var errors = new List<IError>();
        foreach (var indexGroup in searchDocuments.GroupBy(x => x.Index))
        {
            var indexed = await UpsertAsync(indexGroup.Key, indexGroup.ToList(), cancellationToken).ConfigureAwait(false);
            errors.AddRange(indexed.Errors);
        }

        return errors.Count == 0 ? Result.Ok(searchDocuments.Count) : Result.Fail<int>(errors);
    }

    /// <summary>
    /// Upserts <paramref name="documents"/> in one transaction, so either all of them are stored or none is. Every
    /// document is converted before any is sent. When an id repeats, the last document with it wins.
    /// </summary>
    private async Task<Result> UpsertAsync(string index, IReadOnlyList<SearchDocument> documents, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.TryCurrentValue();
        if (options.IsFailed)
        {
            return options.ToResult();
        }

        var table = PostgreSqlNames.Table(options.Value.Schema, index);
        if (table.IsFailed)
        {
            return table.ToResult();
        }

        // None of the group is written when one document fails, so every id is reported as not indexed.
        var ids = documents.Select(document => document.Id).ToList();
        try
        {
            var rows = new Dictionary<string, (string Type, string Fields)>(StringComparer.Ordinal);
            foreach (var document in documents)
            {
                var fields = PostgreSqlDocument.FieldsJson(document.Fields);
                if (fields.IsFailed)
                {
                    return Result.Fail(DocumentIds.IndexFailedError(index, 0, documents.Count, ids, $"document {document.Id}: {fields.Errors[0].Message}"));
                }

                rows.Remove(document.Id);
                rows[document.Id] = (document.Type ?? string.Empty, fields.Value);
            }

            var dataSource = await dataSourceFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                    foreach (var batch in rows.Chunk(InsertBatchSize))
                    {
                        await using var command = PostgreSqlCommands.Command(connection,
                            $"INSERT INTO {table.Value} (id, type, fields) SELECT * FROM unnest($1::text[], $2::text[], $3::jsonb[]) " +
                            "ON CONFLICT (id) DO UPDATE SET type = excluded.type, fields = excluded.fields", transaction: transaction);
                        command.Parameters.Add(new NpgsqlParameter { Value = batch.Select(row => row.Key).ToArray(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
                        command.Parameters.Add(new NpgsqlParameter { Value = batch.Select(row => row.Value.Type).ToArray(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
                        command.Parameters.Add(new NpgsqlParameter { Value = batch.Select(row => row.Value.Fields).ToArray(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Jsonb });
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return Result.Ok();
                }
                catch (Exception e) when (attempt == 0 && PostgreSqlCommands.IsMissingTable(e))
                {
                    // The first write to an index creates its table; a concurrent writer may create it first.
                    var created = await CreateTableAsync(connection, options.Value, index, table.Value, cancellationToken).ConfigureAwait(false);
                    if (created.IsFailed)
                    {
                        return Result.Fail(DocumentIds.IndexFailedError(index, 0, documents.Count, ids, created.Errors[0].Message));
                    }
                }
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(DocumentIds.IndexFailedError(index, 0, documents.Count, ids, PostgreSqlCommands.Describe(e)).CausedBy(e));
        }
    }

    public async Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default)
    {
        var removed = await DeleteAsync(type, "d.id = $1", new NpgsqlParameter { Value = id, NpgsqlDbType = NpgsqlDbType.Text }, cancellationToken).ConfigureAwait(false);
        return removed.IsFailed
            ? Result.Fail(new Error($"Removing document {id} from {type} failed: {removed.Errors[0].Message}").CausedBy(removed.Errors))
            : Result.Ok();
    }

    public async Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = DocumentIds.Distinct(ids);
        if (distinctIds.IsFailed || distinctIds.Value.Count == 0)
        {
            return distinctIds.IsFailed ? distinctIds.ToResult<int>() : Result.Ok(0);
        }

        var removed = await DeleteAsync(index, "d.id = ANY($1)",
            new NpgsqlParameter { Value = distinctIds.Value.ToArray(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text }, cancellationToken).ConfigureAwait(false);
        return removed.IsFailed
            ? Result.Fail<int>(DocumentIds.FailedError(index, 0, distinctIds.Value.Count, distinctIds.Value, removed.Errors[0].Message))
            : Result.Ok((int)removed.Value);
    }

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index,
        CancellationToken cancellationToken = default)
    {
        var removed = await DeleteAsync(index.IndexName, "TRUE", null, cancellationToken).ConfigureAwait(false);
        return removed.IsFailed
            ? Result.Fail<long?>($"Removing every document from {index.IndexName} failed: {removed.Errors[0].Message}")
            : Result.Ok<long?>(removed.Value);
    }

    /// <summary>Deletes the rows matching <paramref name="where"/> and returns how many; a missing table fails, as on the other engines.</summary>
    private async Task<Result<long>> DeleteAsync(string index, string where, NpgsqlParameter? parameter, CancellationToken cancellationToken)
    {
        try
        {
            var options = optionsMonitor.TryCurrentValue();
            if (options.IsFailed)
            {
                return options.ToResult<long>();
            }

            var table = PostgreSqlNames.Table(options.Value.Schema, index);
            if (table.IsFailed)
            {
                return table.ToResult<long>();
            }

            var dataSource = await dataSourceFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = PostgreSqlCommands.Command(connection, $"DELETE FROM {table.Value} d WHERE {where}");
            if (parameter != null)
            {
                command.Parameters.Add(parameter);
            }

            return Result.Ok((long)await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception e) when (PostgreSqlCommands.IsMissingTable(e))
        {
            return Result.Fail<long>(new Error($"the index {index} does not exist").CausedBy(e));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<long>(new Error(PostgreSqlCommands.Describe(e)).CausedBy(e));
        }
    }

    public async Task<Result<bool>> UpdateDocumentAsync(string index, string id, IDictionary<string, IList<object>> fields,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Result.Fail<bool>("UpdateDocumentAsync needs an id.");
        }

        if (fields is null || fields.Count == 0)
        {
            return Result.Fail<bool>("UpdateDocumentAsync needs at least one field to update.");
        }

        if (fields.FirstOrDefault(field => field.Value is null || field.Value.Any(value => value is null)) is { Key: { } invalid })
        {
            return Result.Fail<bool>($"UpdateDocumentAsync field {invalid} is null or holds a null value.");
        }

        try
        {
            var json = PostgreSqlDocument.FieldsJson(fields);
            if (json.IsFailed)
            {
                return json.ToResult<bool>();
            }

            var options = optionsMonitor.TryCurrentValue();
            if (options.IsFailed)
            {
                return options.ToResult<bool>();
            }

            var table = PostgreSqlNames.Table(options.Value.Schema, index);
            if (table.IsFailed)
            {
                return table.ToResult<bool>();
            }

            var dataSource = await dataSourceFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            // One statement, so a concurrent update of other fields of the same document is not lost.
            await using var command = PostgreSqlCommands.Command(connection, $"UPDATE {table.Value} d SET fields = d.fields || $1 WHERE d.id = $2");
            command.Parameters.Add(new NpgsqlParameter { Value = json.Value, NpgsqlDbType = NpgsqlDbType.Jsonb });
            command.Parameters.Add(new NpgsqlParameter { Value = id, NpgsqlDbType = NpgsqlDbType.Text });
            return Result.Ok(await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0);
        }
        catch (Exception e) when (PostgreSqlCommands.IsMissingTable(e))
        {
            return Result.Ok(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<bool>(new Error($"Updating document {id} in {index} failed: {PostgreSqlCommands.Describe(e)}").CausedBy(e));
        }
    }

    public async Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        var exist = await IndexExistsAsync(index, cancellationToken).ConfigureAwait(false);
        // A failed check, such as a refused login, is returned as it is rather than hidden behind a create attempt.
        if (exist.IsFailed || exist.Value)
        {
            return exist;
        }

        // A concurrent caller may create the table first, which CreateIndexAsync reports as Ok(false); it exists either way.
        var created = await CreateIndexAsync(index, cancellationToken).ConfigureAwait(false);
        return created.IsSuccess ? Result.Ok(true) : created;
    }

    public async Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = optionsMonitor.TryCurrentValue();
            if (options.IsFailed)
            {
                return options.ToResult<bool>();
            }

            var table = PostgreSqlNames.Table(options.Value.Schema, index);
            if (table.IsFailed)
            {
                return table.ToResult<bool>();
            }

            var dataSource = await dataSourceFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = PostgreSqlCommands.Command(connection, $"DROP TABLE {table.Value}");
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return Result.Ok(true);
        }
        catch (Exception e) when (PostgreSqlCommands.IsMissingTable(e))
        {
            return Result.Ok(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<bool>(new Error($"Deleting index {index} failed: {PostgreSqlCommands.Describe(e)}").CausedBy(e));
        }
    }

    public async Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = optionsMonitor.TryCurrentValue();
            if (options.IsFailed)
            {
                return options.ToResult<bool>();
            }

            var table = PostgreSqlNames.Table(options.Value.Schema, index);
            if (table.IsFailed)
            {
                return table.ToResult<bool>();
            }

            var dataSource = await dataSourceFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            return Result.Ok(await TableExistsAsync(connection, table.Value, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<bool>(new Error($"Checking index {index} failed: {PostgreSqlCommands.Describe(e)}").CausedBy(e));
        }
    }

    public async Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = optionsMonitor.TryCurrentValue();
            if (options.IsFailed)
            {
                return options.ToResult<bool>();
            }

            var table = PostgreSqlNames.Table(options.Value.Schema, index);
            if (table.IsFailed)
            {
                return table.ToResult<bool>();
            }

            var dataSource = await dataSourceFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            return await CreateTableAsync(connection, options.Value, index, table.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<bool>(new Error($"Creating index {index} failed: {PostgreSqlCommands.Describe(e)}").CausedBy(e));
        }
    }

    /// <summary>
    /// Creates the table and its GIN index in one transaction; <c>Ok(false)</c> when it already exists, as on the
    /// other providers. <c>search</c> holds the words of every string value, as the configured text search
    /// configuration splits them, so a word query is answered from the GIN index before each match is checked
    /// against its field.
    /// </summary>
    private async Task<Result<bool>> CreateTableAsync(NpgsqlConnection connection, PostgreSqlOptions options, string index, string table,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(options.Schema, "public", StringComparison.Ordinal))
            {
                await using var schema = PostgreSqlCommands.Command(connection, $"CREATE SCHEMA IF NOT EXISTS {PostgreSqlNames.Quote(options.Schema)}");
                try
                {
                    await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    // A concurrent caller created the schema.
                }
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var configuration = PostgreSqlNames.Literal(options.TextSearchConfiguration);
            await using (var create = PostgreSqlCommands.Command(connection,
                             $"CREATE TABLE {table} (id text PRIMARY KEY, type text NOT NULL DEFAULT '', fields jsonb NOT NULL DEFAULT '{{}}', " +
                             $"search tsvector GENERATED ALWAYS AS (jsonb_to_tsvector({configuration}::regconfig, fields, '[\"string\"]')) STORED)",
                             transaction: transaction))
            {
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var gin = PostgreSqlCommands.Command(connection, $"CREATE INDEX ON {table} USING gin (search)", transaction: transaction))
            {
                await gin.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Ok(true);
        }
        catch (Exception e) when (PostgreSqlCommands.IsExistingTable(e))
        {
            // The name may be taken by something other than a table, such as a view or a composite type.
            return await TableExistsAsync(connection, table, cancellationToken).ConfigureAwait(false)
                ? Result.Ok(false)
                : Result.Fail<bool>(new Error($"Creating index {index} failed: {table} is already taken by a database object that is not a table, " +
                                              $"so it can't hold an index ({PostgreSqlCommands.Describe(e)}).").CausedBy(e));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Creating index {Index} failed", index);
            return Result.Fail<bool>(new Error($"Creating index {index} failed: {PostgreSqlCommands.Describe(e)}").CausedBy(e));
        }
    }

    /// <summary>Whether <paramref name="table"/> is a table; a view, sequence or composite type of that name is not.</summary>
    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = PostgreSqlCommands.Command(connection,
            "SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_class WHERE oid = to_regclass($1) AND relkind IN ('r', 'p'))");
        command.Parameters.Add(new NpgsqlParameter { Value = table, NpgsqlDbType = NpgsqlDbType.Text });
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }
}
