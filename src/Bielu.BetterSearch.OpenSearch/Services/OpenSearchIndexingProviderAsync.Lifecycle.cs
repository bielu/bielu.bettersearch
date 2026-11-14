using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>
/// Rebuilds through an alias named like the index: a rebuild fills a new index, then one aliases request points the
/// alias at it and deletes the indexes it pointed at before (<c>remove_index</c>), so the swap and the cleanup succeed
/// or fail together.
/// </summary>
internal sealed partial class OpenSearchIndexingProviderAsync
{
    public Task<Result<IndexRebuild>> StartRebuildAsync(string index, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(index, async client =>
        {
            var target = IndexNames.NewRebuildTarget(Name(index), DateTimeOffset.UtcNow);
            var created = await CreateIndexAsync(client, index, target, cancellationToken).ConfigureAwait(false);
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
        var checkedRebuild = CheckRebuild(rebuild);
        if (checkedRebuild.IsFailed)
        {
            return checkedRebuild.ToResult<int>();
        }

        var batch = documents?.ToList() ?? [];
        var checkedDocuments = RebuildDocuments.Check(rebuild, batch);
        if (checkedDocuments.IsFailed)
        {
            return checkedDocuments.ToResult<int>();
        }

        if (batch.Count == 0)
        {
            return Result.Ok(0);
        }

        return await clientFactory.TryWithClientAsync(rebuild.Index, async client =>
        {
            var response = await client.BulkAsync(b => b.Index(rebuild.TargetIndex).IndexMany(batch.Select(StoredFields.WithDatesAsText), (descriptor, document) => descriptor.Id(document.Id)).Refresh(WaitFor(rebuild.Index)), cancellationToken).ConfigureAwait(false);
            if (response.Items is null || response.Items.Count != batch.Count)
            {
                return Result.Fail<int>(RequestFailed(OpenSearchFailures.Describe($"indexing into rebuild index {rebuild.TargetIndex}", response), response.OriginalException));
            }

            var failed = response.Items.Where(item => item.Error is not null).ToList();
            return failed.Count == 0
                ? Result.Ok(batch.Count)
                : Result.Fail<int>($"{failed.Count} of {batch.Count} documents were not indexed into rebuild index {rebuild.TargetIndex} ({string.Join(", ", failed.Select(item => item.Id))}): {failed[0].Error!.Reason}");
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<IReadOnlyList<string>>> CompleteRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default)
    {
        var checkedRebuild = CheckRebuild(rebuild);
        if (checkedRebuild.IsFailed)
        {
            return Task.FromResult(checkedRebuild.ToResult<IReadOnlyList<string>>());
        }

        return clientFactory.TryWithClientAsync<IReadOnlyList<string>>(rebuild.Index, async client =>
        {
            var name = Name(rebuild.Index);
            // Refreshed first, so searches find every rebuilt document as soon as the alias points at it. This also
            // fails when the rebuild index is gone, before the alias is touched.
            var refresh = await client.Indices.RefreshAsync(rebuild.TargetIndex, ct: cancellationToken).ConfigureAwait(false);
            if (!IsSuccess(refresh))
            {
                return Result.Fail<IReadOnlyList<string>>(RequestFailed(OpenSearchFailures.Describe($"refreshing rebuild index {rebuild.TargetIndex}", refresh), refresh.OriginalException));
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

            var actions = new List<IAliasAction>
            {
                new AliasAddAction { Add = new AliasAddOperation { Index = rebuild.TargetIndex, Alias = name } },
            };
            actions.AddRange(toDelete.Select(old => new AliasRemoveIndexAction { RemoveIndex = new AliasRemoveIndexOperation { Index = old } }));
            var response = await client.Indices.BulkAliasAsync(new BulkAliasRequest { Actions = actions }, cancellationToken).ConfigureAwait(false);
            // A 200 with acknowledged false means the cluster took the change but did not confirm it in time. The client
            // calls that response invalid, so it is told apart before the generic failure.
            if (response.ApiCall is { Success: true } && response.OriginalException is null && response.ServerError is null && !response.Acknowledged)
            {
                return Result.Fail<IReadOnlyList<string>>($"Pointing alias {name} at rebuild index {rebuild.TargetIndex} was not acknowledged in time. It may still apply; call CompleteRebuildAsync again to confirm it.");
            }

            return IsSuccess(response)
                ? Result.Ok<IReadOnlyList<string>>(toDelete)
                : Result.Fail<IReadOnlyList<string>>(RequestFailed(OpenSearchFailures.Describe($"pointing alias {name} at rebuild index {rebuild.TargetIndex}", response), response.OriginalException));
        }, cancellationToken);
    }

    public Task<Result<bool>> AbortRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default)
    {
        var checkedRebuild = CheckRebuild(rebuild);
        if (checkedRebuild.IsFailed)
        {
            return Task.FromResult(checkedRebuild.ToResult<bool>());
        }

        return clientFactory.TryWithClientAsync(rebuild.Index, async client =>
        {
            var name = Name(rebuild.Index);
            var aliased = await AliasedIndexesAsync(client, name, cancellationToken).ConfigureAwait(false);
            if (aliased.IsFailed)
            {
                return aliased.ToResult<bool>();
            }

            if (aliased.Value.Contains(rebuild.TargetIndex))
            {
                return Result.Fail<bool>($"The rebuild of {rebuild.Index} is already complete: alias {name} points at {rebuild.TargetIndex}, so it was not deleted.");
            }

            var response = await client.Indices.DeleteAsync(rebuild.TargetIndex, ct: cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return response.ApiCall?.HttpStatusCode switch
            {
                _ when IsSuccess(response) => Result.Ok(true),
                404 => Result.Ok(false),
                _ => Result.Fail<bool>(RequestFailed(OpenSearchFailures.Describe($"deleting rebuild index {rebuild.TargetIndex}", response), response.OriginalException)),
            };
        }, cancellationToken);
    }

    /// <summary>Fails when <paramref name="rebuild"/> is not one <see cref="StartRebuildAsync"/> could have returned, so no other index is touched.</summary>
    private Result CheckRebuild(IndexRebuild? rebuild)
    {
        if (rebuild is null || string.IsNullOrWhiteSpace(rebuild.Index))
        {
            return Result.Fail("The rebuild is null or has no index.");
        }

        return IndexNames.IsRebuildTargetOf(Name(rebuild.Index), rebuild.TargetIndex)
            ? Result.Ok()
            : Result.Fail($"'{rebuild.TargetIndex}' is not a rebuild index of {rebuild.Index}; use the rebuild StartRebuildAsync returned.");
    }

    /// <summary>
    /// The indexes the alias <paramref name="name"/> points at, or the index of that name when it is an index rather
    /// than an alias, or none when neither exists.
    /// </summary>
    private static async Task<Result<IReadOnlyList<string>>> PreviousIndexesAsync(IOpenSearchClient client, string name, CancellationToken cancellationToken)
    {
        var aliased = await AliasedIndexesAsync(client, name, cancellationToken).ConfigureAwait(false);
        if (aliased.IsFailed || aliased.Value.Count > 0)
        {
            return aliased;
        }

        var exists = await client.Indices.ExistsAsync(name, ct: cancellationToken).ConfigureAwait(false);
        return exists.ApiCall?.HttpStatusCode switch
        {
            200 => Result.Ok<IReadOnlyList<string>>([name]),
            404 => Result.Ok<IReadOnlyList<string>>([]),
            _ => Result.Fail<IReadOnlyList<string>>(RequestFailed(OpenSearchFailures.Describe("index exists check", exists), exists.OriginalException)),
        };
    }

    /// <summary>The indexes the alias <paramref name="name"/> points at; none when there is no such alias.</summary>
    private static async Task<Result<IReadOnlyList<string>>> AliasedIndexesAsync(IOpenSearchClient client, string name, CancellationToken cancellationToken)
    {
        var response = await client.Indices.GetAliasAsync(new GetAliasRequest(Names.Parse(name)), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (response.ApiCall?.HttpStatusCode == 404)
        {
            return Result.Ok<IReadOnlyList<string>>([]);
        }

        return IsSuccess(response)
            ? Result.Ok<IReadOnlyList<string>>((response.Indices?.Keys.Select(key => key.Name) ?? []).Order(StringComparer.Ordinal).ToList())
            : Result.Fail<IReadOnlyList<string>>(RequestFailed(OpenSearchFailures.Describe($"reading alias {name}", response), response.OriginalException));
    }
}
