using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.IndexManagement;
using FluentResults;
using ExistsRequest = Elastic.Clients.Elasticsearch.IndexManagement.ExistsRequest;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Rebuilds through an alias named like the index: a rebuild fills a new index, then one update aliases request points
/// the alias at it and deletes the indexes it pointed at before (<c>remove_index</c>), so the swap and the cleanup
/// succeed or fail together. The rebuild index carries a pending alias (<see cref="IndexNames.RebuildPendingAlias"/>)
/// that completing and aborting each remove with <c>must_exist</c>, so once one has run the other fails, even when they
/// overlap or run in different processes.
/// </summary>
public partial class ElasticSearchIndexingProviderAsync
{
    public Task<Result<IndexRebuild>> StartRebuildAsync(string index, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(index, async client =>
        {
            var target = IndexNames.NewRebuildTarget(Name(index), DateTimeOffset.UtcNow);
            var created = await CreateIndexAsync(client, index, target, cancellationToken, IndexNames.RebuildPendingAlias(target)).ConfigureAwait(false);
            if (created.IsFailed)
            {
                return created.ToResult<IndexRebuild>();
            }

            return created.Value
                ? Result.Ok(new IndexRebuild(index, target))
                : Result.Fail<IndexRebuild>($"The rebuild index {target} already exists; start the rebuild again.");
        }, cancellationToken);

    public async Task<Result<int>> IndexRebuildDocumentsAsync(IndexRebuild rebuild, IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var checkedRebuild = CheckRebuild(rebuild, out _);
        if (checkedRebuild.IsFailed)
        {
            return checkedRebuild.ToResult<int>();
        }

        var batch = documents?.ToList() ?? [];
        var otherIndex = RebuildDocuments.Check(rebuild, batch);
        if (otherIndex.IsFailed)
        {
            return otherIndex.ToResult<int>();
        }

        if (batch.Count == 0)
        {
            return Result.Ok(0);
        }

        return await clientFactory.TryWithClientAsync(rebuild.Index, async client =>
        {
            var request = new BulkRequest(rebuild.TargetIndex)
            {
                Operations = new BulkOperationsCollection(batch.Select(document =>
                    new BulkIndexOperation<SearchDocument>(StoredFields.WithDatesAsText(document)) { Id = document.Id })),
                Refresh = WaitFor(rebuild.Index),
            };
            var response = await client.BulkAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess() || response.Items is null || response.Items.Count != batch.Count)
            {
                return Result.Fail<int>(RequestFailed(ElasticSearchFailures.Describe($"indexing into rebuild index {rebuild.TargetIndex}", response), response.ApiCallDetails?.OriginalException));
            }

            var failed = response.Items.Where(item => item.Error is not null).ToList();
            return failed.Count == 0
                ? Result.Ok(batch.Count)
                : Result.Fail<int>($"{failed.Count} of {batch.Count} documents were not indexed into rebuild index {rebuild.TargetIndex} ({string.Join(", ", failed.Select(item => item.Id))}): {failed[0].Error!.Reason}");
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<IReadOnlyList<string>>> CompleteRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default)
    {
        // The name is read once, so the alias checked here is the one moved and cleaned up even if the prefix reloads.
        var checkedRebuild = CheckRebuild(rebuild, out var name);
        if (checkedRebuild.IsFailed)
        {
            return Task.FromResult(checkedRebuild.ToResult<IReadOnlyList<string>>());
        }

        return clientFactory.TryWithClientAsync<IReadOnlyList<string>>(rebuild.Index, async client =>
        {
            // Refreshed first, so searches find every rebuilt document as soon as the alias points at it. This also
            // fails when the rebuild index is gone, before the alias is touched.
            var refresh = await client.Indices.RefreshAsync(new RefreshRequest(Indices.Index(rebuild.TargetIndex)), cancellationToken).ConfigureAwait(false);
            if (!refresh.IsSuccess())
            {
                return Result.Fail<IReadOnlyList<string>>(RequestFailed(ElasticSearchFailures.Describe($"refreshing rebuild index {rebuild.TargetIndex}", refresh), refresh.ApiCallDetails?.OriginalException));
            }

            // A refresh can answer 200 with failed shards, and then not every rebuilt document is searchable yet.
            if (refresh.Shards is { Failed: > 0 } shards)
            {
                return Result.Fail<IReadOnlyList<string>>(RebuildIndexNotRefreshed(rebuild.TargetIndex, shards.Failed, shards.Total));
            }

            var previous = await PreviousIndexesAsync(client, name, cancellationToken).ConfigureAwait(false);
            if (previous.IsFailed)
            {
                return previous.ToResult<IReadOnlyList<string>>();
            }

            var toDelete = previous.Value.Where(old => old != rebuild.TargetIndex).ToList();
            if (toDelete.Count < previous.Value.Count && toDelete.Count == 0)
            {
                // The alias points at the rebuild index alone: an earlier call completed it.
                return Result.Ok<IReadOnlyList<string>>([]);
            }

            var actions = new List<IndexUpdateAliasesAction>
            {
                new IndexUpdateAliasesAction { Remove = new RemoveAction { Index = rebuild.TargetIndex, Alias = IndexNames.RebuildPendingAlias(rebuild.TargetIndex), MustExist = true } },
                new IndexUpdateAliasesAction { Add = new AddAction { Index = rebuild.TargetIndex, Alias = name } },
            };
            actions.AddRange(toDelete.Select(old => new IndexUpdateAliasesAction { RemoveIndex = new RemoveIndexAction { Index = old } }));
            var response = await client.Indices.UpdateAliasesAsync(new UpdateAliasesRequest { Actions = actions }, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess())
            {
                // Another call completed the rebuild after this one read the alias, and took the pending alias with it.
                var now = await AliasedIndexesAsync(client, name, cancellationToken).ConfigureAwait(false);
                if (now.IsSuccess && now.Value.Count == 1 && now.Value[0] == rebuild.TargetIndex)
                {
                    return Result.Ok<IReadOnlyList<string>>([]);
                }

                return Result.Fail<IReadOnlyList<string>>(RequestFailed(ElasticSearchFailures.Describe($"pointing alias {name} at rebuild index {rebuild.TargetIndex}", response), response.ApiCallDetails?.OriginalException));
            }

            return response.Acknowledged
                ? Result.Ok<IReadOnlyList<string>>(toDelete)
                : Result.Fail<IReadOnlyList<string>>($"Pointing alias {name} at rebuild index {rebuild.TargetIndex} was not acknowledged in time. It may still apply; call CompleteRebuildAsync again to confirm it.");
        }, cancellationToken);
    }

    public Task<Result<bool>> AbortRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default)
    {
        var checkedRebuild = CheckRebuild(rebuild, out var name);
        if (checkedRebuild.IsFailed)
        {
            return Task.FromResult(checkedRebuild.ToResult<bool>());
        }

        return clientFactory.TryWithClientAsync(rebuild.Index, async client =>
        {
            // Removing the pending alias first means a completion that has not yet run can no longer succeed, so the
            // index is not live when it is deleted. When the pending alias is already gone, either the rebuild is
            // complete or an earlier abort removed it.
            var pending = new IndexUpdateAliasesAction { Remove = new RemoveAction { Index = rebuild.TargetIndex, Alias = IndexNames.RebuildPendingAlias(rebuild.TargetIndex), MustExist = true } };
            var claimed = await client.Indices.UpdateAliasesAsync(new UpdateAliasesRequest { Actions = [pending] }, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!claimed.IsSuccess() || !claimed.Acknowledged)
            {
                var aliased = await AliasedIndexesAsync(client, name, cancellationToken).ConfigureAwait(false);
                if (aliased.IsFailed)
                {
                    return aliased.ToResult<bool>();
                }

                if (aliased.Value.Contains(rebuild.TargetIndex))
                {
                    return Result.Fail<bool>($"The rebuild of {rebuild.Index} is already complete: alias {name} points at {rebuild.TargetIndex}, so it was not deleted.");
                }

                if (claimed.ElasticsearchServerError?.Error?.Type is not ("aliases_not_found_exception" or "index_not_found_exception"))
                {
                    return Result.Fail<bool>(claimed.IsSuccess()
                        ? new Error($"Removing the pending alias of rebuild index {rebuild.TargetIndex} was not acknowledged in time, so it was not deleted; call AbortRebuildAsync again.")
                        : RequestFailed(ElasticSearchFailures.Describe($"removing the pending alias of rebuild index {rebuild.TargetIndex}", claimed), claimed.ApiCallDetails?.OriginalException));
                }
            }

            var response = await client.Indices.DeleteAsync(new DeleteIndexRequest(Indices.Index(rebuild.TargetIndex)), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return response.ApiCallDetails?.HttpStatusCode switch
            {
                _ when response.IsSuccess() => Result.Ok(true),
                404 => Result.Ok(false),
                _ => Result.Fail<bool>(RequestFailed(ElasticSearchFailures.Describe($"deleting rebuild index {rebuild.TargetIndex}", response), response.ApiCallDetails?.OriginalException)),
            };
        }, cancellationToken);
    }

    /// <summary>Fails when <paramref name="rebuild"/> is not one <see cref="StartRebuildAsync"/> could have returned, so no other index is touched.</summary>
    /// <param name="rebuild">The rebuild to check.</param>
    /// <param name="name">The name Elasticsearch knows the rebuild's index by, read once for the whole call.</param>
    private Result CheckRebuild(IndexRebuild? rebuild, out string name)
    {
        name = "";
        if (rebuild is null || string.IsNullOrWhiteSpace(rebuild.Index))
        {
            return Result.Fail("The rebuild is null or has no index.");
        }

        name = Name(rebuild.Index);
        return IndexNames.IsRebuildTargetOf(name, rebuild.TargetIndex)
            ? Result.Ok()
            : Result.Fail($"'{rebuild.TargetIndex}' is not a rebuild index of {rebuild.Index}; use the rebuild StartRebuildAsync returned.");
    }

    private static string RebuildIndexNotRefreshed(string target, long failed, long total) =>
        $"Refreshing rebuild index {target} failed on {failed} of {total} shards, so not every rebuilt document is searchable; the alias was not moved. Call CompleteRebuildAsync again.";

    /// <summary>
    /// The indexes the alias <paramref name="name"/> points at, or the index of that name when it is an index rather
    /// than an alias, or none when neither exists.
    /// </summary>
    private static async Task<Result<IReadOnlyList<string>>> PreviousIndexesAsync(ElasticsearchClient client, string name, CancellationToken cancellationToken)
    {
        var aliased = await AliasedIndexesAsync(client, name, cancellationToken).ConfigureAwait(false);
        if (aliased.IsFailed || aliased.Value.Count > 0)
        {
            return aliased;
        }

        var exists = await client.Indices.ExistsAsync(new ExistsRequest(Indices.Index(name)), cancellationToken).ConfigureAwait(false);
        return exists.ApiCallDetails?.HttpStatusCode switch
        {
            200 => Result.Ok<IReadOnlyList<string>>([name]),
            404 => Result.Ok<IReadOnlyList<string>>([]),
            _ => Result.Fail<IReadOnlyList<string>>(RequestFailed(ElasticSearchFailures.Describe("index exists check", exists), exists.ApiCallDetails?.OriginalException)),
        };
    }

    /// <summary>The indexes the alias <paramref name="name"/> points at; none when there is no such alias.</summary>
    private static async Task<Result<IReadOnlyList<string>>> AliasedIndexesAsync(ElasticsearchClient client, string name, CancellationToken cancellationToken)
    {
        var response = await client.Indices.GetAliasAsync(new GetAliasRequest(Names.Parse(name)), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (response.ApiCallDetails?.HttpStatusCode == 404)
        {
            return Result.Ok<IReadOnlyList<string>>([]);
        }

        return response.IsSuccess()
            ? Result.Ok<IReadOnlyList<string>>((response.Aliases?.Keys.Select(key => key.ToString()) ?? []).Order(StringComparer.Ordinal).ToList())
            : Result.Fail<IReadOnlyList<string>>(RequestFailed(ElasticSearchFailures.Describe($"reading alias {name}", response), response.ApiCallDetails?.OriginalException));
    }
}
