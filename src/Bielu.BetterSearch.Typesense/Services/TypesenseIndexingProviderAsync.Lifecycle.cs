using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Services;
using FluentResults;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>
/// Rebuilds through a collection alias named like the index. Typesense has no atomic or conditional alias update, so
/// completing points the alias at the rebuild collection and deletes the collections it replaced in separate requests.
/// </summary>
/// <remarks>
/// Rebuilds have a single-writer contract: at most one rebuild of an index is in progress at a time, and its start,
/// fill, complete and abort do not run at the same time as another rebuild call for the same index. Running them at
/// the same time is not supported and can leave a collection behind or the alias pointing at a deleted collection.
/// </remarks>
public sealed partial class TypesenseIndexingProviderAsync
{
    public async Task<Result<IndexRebuild>> StartRebuildAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            var target = IndexNames.NewRebuildTarget(Name(index), DateTimeOffset.UtcNow);
            var created = await CreateCollectionAsync(client, target, cancellationToken).ConfigureAwait(false);
            if (created.IsFailed)
            {
                return created.ToResult<IndexRebuild>();
            }

            return created.Value
                ? Result.Ok(new IndexRebuild(index, target))
                : Result.Fail<IndexRebuild>($"The rebuild index {target} already exists; start the rebuild again.");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<IndexRebuild>(new Error(e.Message).CausedBy(e));
        }
    }

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

        // A missing rebuild collection was aborted or deleted, so it is not created again.
        var imported = await ImportAsync(rebuild.Index, rebuild.TargetIndex, $"rebuild index {rebuild.TargetIndex}", false, batch, cancellationToken).ConfigureAwait(false);
        return imported.IsSuccess ? Result.Ok(batch.Count) : imported.ToResult<int>();
    }

    public async Task<Result<IReadOnlyList<string>>> CompleteRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default)
    {
        // The name is read once, so the alias checked here is the one moved even if the prefix reloads.
        var checkedRebuild = CheckRebuild(rebuild, out var name);
        if (checkedRebuild.IsFailed)
        {
            return checkedRebuild.ToResult<IReadOnlyList<string>>();
        }

        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(rebuild.Index).ConfigureAwait(false);
            var previous = await PreviousCollectionAsync(client, name, cancellationToken).ConfigureAwait(false);
            if (previous.IsFailed)
            {
                return previous.ToResult<IReadOnlyList<string>>();
            }

            if (previous.Value.Collection != rebuild.TargetIndex)
            {
                var exists = await CollectionExistsAsync(client, rebuild.TargetIndex, cancellationToken).ConfigureAwait(false);
                if (exists.IsFailed || !exists.Value)
                {
                    return exists.IsFailed ? exists.ToResult<IReadOnlyList<string>>() : Result.Fail<IReadOnlyList<string>>(RebuildIndexGone(rebuild, name));
                }

                var moved = await MoveAliasAsync(client, name, rebuild.TargetIndex, previous.Value, cancellationToken).ConfigureAwait(false);
                if (moved.IsFailed)
                {
                    return moved;
                }

                return await DeleteLeftoverRebuildsAsync(client, name, rebuild.TargetIndex, moved.Value, cancellationToken).ConfigureAwait(false);
            }

            // An earlier call moved the alias, and may have stopped before deleting the collection it replaced.
            return await DeleteLeftoverRebuildsAsync(client, name, rebuild.TargetIndex, [], cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<IReadOnlyList<string>>(new Error(e.Message).CausedBy(e));
        }
    }

    public async Task<Result<bool>> AbortRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default)
    {
        var checkedRebuild = CheckRebuild(rebuild, out var name);
        if (checkedRebuild.IsFailed)
        {
            return checkedRebuild.ToResult<bool>();
        }

        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(rebuild.Index).ConfigureAwait(false);
            var live = await AliasTargetAsync(client, name, cancellationToken).ConfigureAwait(false);
            if (live.IsFailed)
            {
                return live.ToResult<bool>();
            }

            if (live.Value == rebuild.TargetIndex)
            {
                return Result.Fail<bool>($"The rebuild of {rebuild.Index} is already complete: alias {name} points at {rebuild.TargetIndex}, so it was not deleted.");
            }

            schema.Forget(client, rebuild.TargetIndex);
            var response = await client.SendAsync(HttpMethod.Delete, $"collections/{TypesenseHttpClientWrapper.Escape(rebuild.TargetIndex)}", null, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess && response.StatusCode != HttpStatusCode.NotFound)
            {
                return Result.Fail<bool>($"Deleting rebuild index {rebuild.TargetIndex} failed: {response.Describe()}");
            }

            return Result.Ok(response.IsSuccess);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<bool>(new Error(e.Message).CausedBy(e));
        }
    }

    /// <summary>
    /// Points the alias <paramref name="name"/> at <paramref name="target"/> and deletes the collection it pointed at
    /// before, returning the deleted collection's name.
    /// </summary>
    private async Task<Result<IReadOnlyList<string>>> MoveAliasAsync(TypesenseHttpClientWrapper client, string name, string target,
        (string? Collection, bool IsAlias) previous, CancellationToken cancellationToken)
    {
        var aliasBody = new JsonObject { ["collection_name"] = target }.ToJsonString();
        IReadOnlyList<string> deleted = [];
        if (previous is { Collection: not null, IsAlias: false })
        {
            // Typesense refuses an alias named like a collection, so the first rebuild deletes the plain collection
            // and then creates the alias; searches in between fail. A write in between creates the collection again,
            // so it is deleted again, and that write is lost, as any write made during a rebuild is.
            for (var attempt = 0; ; attempt++)
            {
                var dropped = await client.SendAsync(HttpMethod.Delete, $"collections/{TypesenseHttpClientWrapper.Escape(name)}", null, cancellationToken).ConfigureAwait(false);
                if (!dropped.IsSuccess && dropped.StatusCode != HttpStatusCode.NotFound)
                {
                    return Result.Fail<IReadOnlyList<string>>($"Deleting collection {name} to replace it with an alias failed: {dropped.Describe()}");
                }

                if (dropped.IsSuccess)
                {
                    deleted = [name];
                }

                var aliased = await client.SendAsync(HttpMethod.Put, $"aliases/{TypesenseHttpClientWrapper.Escape(name)}",
                    () => new StringContent(aliasBody, Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
                if (aliased.IsSuccess)
                {
                    break;
                }

                if (attempt == 2 || !aliased.Message.Contains("conflicts with an existing collection", StringComparison.Ordinal))
                {
                    return Result.Fail<IReadOnlyList<string>>($"Creating alias {name} for rebuild index {target} failed after collection {name} was deleted: {aliased.Describe()}. Call CompleteRebuildAsync again.");
                }
            }
        }
        else
        {
            var aliased = await client.SendAsync(HttpMethod.Put, $"aliases/{TypesenseHttpClientWrapper.Escape(name)}",
                () => new StringContent(aliasBody, Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
            if (!aliased.IsSuccess)
            {
                return Result.Fail<IReadOnlyList<string>>($"Pointing alias {name} at rebuild index {target} failed: {aliased.Describe()}");
            }

            if (previous.Collection is { } old)
            {
                schema.Forget(client, old);
                var dropped = await client.SendAsync(HttpMethod.Delete, $"collections/{TypesenseHttpClientWrapper.Escape(old)}", null, cancellationToken).ConfigureAwait(false);
                if (!dropped.IsSuccess && dropped.StatusCode != HttpStatusCode.NotFound)
                {
                    return Result.Fail<IReadOnlyList<string>>($"Alias {name} now points at rebuild index {target}, but deleting the collection it pointed at before, {old}, failed: {dropped.Describe()}. Call CompleteRebuildAsync again or delete {old} yourself.");
                }

                deleted = dropped.IsSuccess ? [old] : [];
            }
        }

        // Searches through the alias read the rebuild collection's fields from now on.
        schema.Forget(client, name);
        return Result.Ok(deleted);
    }

    /// <summary>
    /// Deletes every rebuild collection of <paramref name="name"/> other than the live <paramref name="live"/>: one a
    /// complete that stopped after moving the alias did not delete, or one never completed or aborted. Returns
    /// <paramref name="deleted"/> and their names.
    /// </summary>
    private async Task<Result<IReadOnlyList<string>>> DeleteLeftoverRebuildsAsync(TypesenseHttpClientWrapper client, string name, string live,
        IReadOnlyList<string> deleted, CancellationToken cancellationToken)
    {
        var listed = await client.SendAsync(HttpMethod.Get, "collections", null, cancellationToken).ConfigureAwait(false);
        if (!listed.IsSuccess)
        {
            return Result.Fail<IReadOnlyList<string>>($"Alias {name} points at rebuild index {live}, but listing collections to delete earlier rebuild collections of it failed: {listed.Describe()}. Call CompleteRebuildAsync again.");
        }

        List<string> leftovers;
        using (var json = JsonDocument.Parse(listed.Body))
        {
            // Only names NewRebuildTarget gives this index, so an index whose name starts with the same text is not touched.
            leftovers = json.RootElement.EnumerateArray()
                .Select(collection => collection.GetProperty("name").GetString())
                .Where(collection => collection != live && IndexNames.IsRebuildTargetOf(name, collection))
                .Select(collection => collection!)
                .Order(StringComparer.Ordinal)
                .ToList();
        }

        var all = deleted.ToList();
        foreach (var leftover in leftovers)
        {
            schema.Forget(client, leftover);
            var dropped = await client.SendAsync(HttpMethod.Delete, $"collections/{TypesenseHttpClientWrapper.Escape(leftover)}", null, cancellationToken).ConfigureAwait(false);
            if (!dropped.IsSuccess && dropped.StatusCode != HttpStatusCode.NotFound)
            {
                return Result.Fail<IReadOnlyList<string>>($"Alias {name} points at rebuild index {live}, but deleting {leftover}, an earlier rebuild collection of it, failed: {dropped.Describe()}. Call CompleteRebuildAsync again or delete {leftover} yourself.");
            }

            if (dropped.IsSuccess)
            {
                all.Add(leftover);
            }
        }

        return Result.Ok<IReadOnlyList<string>>(all);
    }

    /// <summary>
    /// The collection the alias <paramref name="name"/> points at, or the collection of that name when there is no such
    /// alias, or none when neither exists.
    /// </summary>
    private static async Task<Result<(string? Collection, bool IsAlias)>> PreviousCollectionAsync(TypesenseHttpClientWrapper client, string name,
        CancellationToken cancellationToken)
    {
        var aliased = await AliasTargetAsync(client, name, cancellationToken).ConfigureAwait(false);
        if (aliased.IsFailed || aliased.Value is not null)
        {
            return aliased.IsFailed ? aliased.ToResult<(string?, bool)>() : Result.Ok<(string?, bool)>((aliased.Value, true));
        }

        var exists = await CollectionExistsAsync(client, name, cancellationToken).ConfigureAwait(false);
        return exists.IsFailed ? exists.ToResult<(string?, bool)>() : Result.Ok<(string?, bool)>((exists.Value ? name : null, false));
    }

    /// <summary>The collection the alias <paramref name="name"/> points at; <c>null</c> when there is no such alias.</summary>
    private static async Task<Result<string?>> AliasTargetAsync(TypesenseHttpClientWrapper client, string name, CancellationToken cancellationToken)
    {
        var response = await client.SendAsync(HttpMethod.Get, $"aliases/{TypesenseHttpClientWrapper.Escape(name)}", null, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Result.Ok<string?>(null);
        }

        if (!response.IsSuccess)
        {
            return Result.Fail<string?>($"Reading alias {name} failed: {response.Describe()}");
        }

        using var json = JsonDocument.Parse(response.Body);
        return Result.Ok(json.RootElement.GetProperty("collection_name").GetString());
    }

    /// <summary>Whether <paramref name="collection"/>, or the collection an alias of that name points at, exists.</summary>
    private static async Task<Result<bool>> CollectionExistsAsync(TypesenseHttpClientWrapper client, string collection, CancellationToken cancellationToken)
    {
        var response = await client.SendAsync(HttpMethod.Get, $"collections/{TypesenseHttpClientWrapper.Escape(collection)}", null, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccess)
        {
            return Result.Ok(true);
        }

        return response.StatusCode == HttpStatusCode.NotFound
            ? Result.Ok(false)
            : Result.Fail<bool>($"Checking collection {collection} failed: {response.Describe()}");
    }

    /// <summary>
    /// Deletes the collection the alias <paramref name="name"/> points at and the alias:
    /// deleting by the alias's name would delete the collection and leave the alias pointing at nothing.
    /// </summary>
    private async Task<Result<bool>> DeleteAliasedAsync(TypesenseHttpClientWrapper client, string index, string name, string live, CancellationToken cancellationToken)
    {
        schema.Forget(client, live);
        var dropped = await client.SendAsync(HttpMethod.Delete, $"collections/{TypesenseHttpClientWrapper.Escape(live)}", null, cancellationToken).ConfigureAwait(false);
        if (!dropped.IsSuccess && dropped.StatusCode != HttpStatusCode.NotFound)
        {
            return Result.Fail<bool>($"Deleting index {index} failed: {dropped.Describe()}");
        }

        var unaliased = await client.SendAsync(HttpMethod.Delete, $"aliases/{TypesenseHttpClientWrapper.Escape(name)}", null, cancellationToken).ConfigureAwait(false);
        return unaliased.IsSuccess || unaliased.StatusCode == HttpStatusCode.NotFound
            ? Result.Ok(true)
            : Result.Fail<bool>($"Deleting index {index} deleted collection {live} but not alias {name}: {unaliased.Describe()}");
    }

    /// <summary>Fails when <paramref name="rebuild"/> is not one <see cref="StartRebuildAsync"/> could have returned, so no other collection is touched.</summary>
    /// <param name="rebuild">The rebuild to check.</param>
    /// <param name="name">The name Typesense knows the rebuild's index by, read once for the whole call.</param>
    private Result CheckRebuild(IndexRebuild? rebuild, out string name)
    {
        name = "";
        if (rebuild is null || string.IsNullOrWhiteSpace(rebuild.Index))
        {
            return Result.Fail("The rebuild is null or has no index.");
        }

        try
        {
            name = Name(rebuild.Index);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(new Error(e.Message).CausedBy(e));
        }

        return IndexNames.IsRebuildTargetOf(name, rebuild.TargetIndex)
            ? Result.Ok()
            : Result.Fail($"'{rebuild.TargetIndex}' is not a rebuild index of {rebuild.Index}; use the rebuild StartRebuildAsync returned.");
    }

    private static string RebuildIndexGone(IndexRebuild rebuild, string name) =>
        $"Rebuild index {rebuild.TargetIndex} does not exist: the rebuild was aborted or the collection deleted, so alias {name} was not moved.";
}
