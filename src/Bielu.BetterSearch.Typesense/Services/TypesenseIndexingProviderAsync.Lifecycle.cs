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
/// Rebuilds through a collection alias named like the index. Typesense has no atomic multi-step alias request, so a
/// rebuild is decided once by creating its decision collection (<see cref="DecisionCollection"/>), which only one
/// call can create: its one field records whether a complete or an abort created it, and every later complete or abort
/// of the rebuild reads it and follows it. Completing then points the alias at the rebuild collection and deletes the
/// collection it pointed at before, in separate requests.
/// </summary>
public sealed partial class TypesenseIndexingProviderAsync
{
    private const string CompleteDecision = "complete";
    private const string AbortDecision = "abort";

    /// <summary>How long a complete that lost the decision to another complete waits for it to move the alias before moving it itself.</summary>
    internal static TimeSpan CompletionWait { get; set; } = TimeSpan.FromSeconds(5);

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

            if (previous.Value.Collection == rebuild.TargetIndex)
            {
                // The alias points at the rebuild collection: an earlier call completed it.
                return Result.Ok<IReadOnlyList<string>>([]);
            }

            // Checked before deciding, so a rebuild whose collection is gone is never decided complete.
            var exists = await CollectionExistsAsync(client, rebuild.TargetIndex, cancellationToken).ConfigureAwait(false);
            if (exists.IsFailed || !exists.Value)
            {
                return exists.IsFailed ? exists.ToResult<IReadOnlyList<string>>() : Result.Fail<IReadOnlyList<string>>(RebuildIndexGone(rebuild, name));
            }

            var decision = await DecideAsync(client, rebuild.TargetIndex, CompleteDecision, cancellationToken).ConfigureAwait(false);
            if (decision.IsFailed)
            {
                return decision.ToResult<IReadOnlyList<string>>();
            }

            if (decision.Value.Decision != CompleteDecision)
            {
                return Result.Fail<IReadOnlyList<string>>($"The rebuild of {rebuild.Index} was aborted, so alias {name} was not pointed at rebuild index {rebuild.TargetIndex}.");
            }

            if (decision.Value.Won)
            {
                // An abort that finished after the check above deleted the rebuild collection and its decision.
                exists = await CollectionExistsAsync(client, rebuild.TargetIndex, cancellationToken).ConfigureAwait(false);
                if (exists.IsFailed)
                {
                    return exists.ToResult<IReadOnlyList<string>>();
                }

                if (!exists.Value)
                {
                    await DeleteDecisionAsync(client, rebuild.TargetIndex, cancellationToken).ConfigureAwait(false);
                    return Result.Fail<IReadOnlyList<string>>(RebuildIndexGone(rebuild, name));
                }
            }
            else
            {
                // Another complete decided first, or an earlier one stopped before moving the alias: wait for it, then
                // finish the move here.
                previous = await WaitForAliasAsync(client, name, rebuild.TargetIndex, cancellationToken).ConfigureAwait(false);
                if (previous.IsFailed || previous.Value.Collection == rebuild.TargetIndex)
                {
                    return previous.IsFailed ? previous.ToResult<IReadOnlyList<string>>() : Result.Ok<IReadOnlyList<string>>([]);
                }
            }

            return await MoveAliasAsync(client, name, rebuild.TargetIndex, previous.Value, cancellationToken).ConfigureAwait(false);
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
            var decision = await DecideAsync(client, rebuild.TargetIndex, AbortDecision, cancellationToken).ConfigureAwait(false);
            if (decision.IsFailed)
            {
                return decision.ToResult<bool>();
            }

            if (decision.Value.Decision != AbortDecision)
            {
                return Result.Fail<bool>($"The rebuild of {rebuild.Index} is already complete: alias {name} points, or is being pointed, at {rebuild.TargetIndex}, so it was not deleted.");
            }

            schema.Forget(client, rebuild.TargetIndex);
            var response = await client.SendAsync(HttpMethod.Delete, $"collections/{TypesenseHttpClientWrapper.Escape(rebuild.TargetIndex)}", null, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess && response.StatusCode != HttpStatusCode.NotFound)
            {
                return Result.Fail<bool>($"Deleting rebuild index {rebuild.TargetIndex} failed: {response.Describe()}");
            }

            // The rebuild collection is gone, so a later complete fails on that before it decides anything.
            await DeleteDecisionAsync(client, rebuild.TargetIndex, cancellationToken).ConfigureAwait(false);
            return Result.Ok(response.IsSuccess);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<bool>(new Error(e.Message).CausedBy(e));
        }
    }

    /// <summary>
    /// The collection whose creation decides the rebuild of <paramref name="target"/>: the first complete or abort
    /// creates it, with one field named after what it decided, and every later one reads that field.
    /// </summary>
    internal static string DecisionCollection(string target) => target + "-decision";

    /// <summary>
    /// Decides the rebuild of <paramref name="target"/> as <paramref name="decision"/> unless another call decided it
    /// first: what was decided, and whether this call decided it.
    /// </summary>
    private static async Task<Result<(string Decision, bool Won)>> DecideAsync(TypesenseHttpClientWrapper client, string target, string decision,
        CancellationToken cancellationToken)
    {
        var name = DecisionCollection(target);
        var body = new JsonObject
        {
            ["name"] = name,
            ["fields"] = new JsonArray(new JsonObject { ["name"] = decision, ["type"] = "string", ["optional"] = true }),
        }.ToJsonString();
        // A decision collection can be deleted between a failed create and the read, by an abort that finished.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var created = await client.SendAsync(HttpMethod.Post, "collections", () => new StringContent(body, Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
            if (created.IsSuccess)
            {
                return Result.Ok((decision, true));
            }

            if (created.StatusCode != HttpStatusCode.Conflict)
            {
                return Result.Fail<(string, bool)>($"Recording the decision for rebuild index {target} failed: {created.Describe()}");
            }

            var existing = await client.SendAsync(HttpMethod.Get, $"collections/{TypesenseHttpClientWrapper.Escape(name)}", null, cancellationToken).ConfigureAwait(false);
            if (existing.StatusCode == HttpStatusCode.NotFound)
            {
                continue;
            }

            if (!existing.IsSuccess)
            {
                return Result.Fail<(string, bool)>($"Reading the decision for rebuild index {target} failed: {existing.Describe()}");
            }

            using var json = JsonDocument.Parse(existing.Body);
            var decided = json.RootElement.GetProperty("fields").EnumerateArray()
                .Select(field => field.GetProperty("name").GetString())
                .FirstOrDefault(field => field is CompleteDecision or AbortDecision);
            return decided is null
                ? Result.Fail<(string, bool)>($"Collection {name} exists but does not record a rebuild decision; delete it and call again.")
                // A replayed create can find the collection this call created.
                : Result.Ok((decided, false));
        }

        return Result.Fail<(string, bool)>($"The decision for rebuild index {target} kept changing; call again.");
    }

    private static async Task DeleteDecisionAsync(TypesenseHttpClientWrapper client, string target, CancellationToken cancellationToken)
    {
        // Only an empty marker is left behind when this fails, so the failure is not reported.
        await client.SendAsync(HttpMethod.Delete, $"collections/{TypesenseHttpClientWrapper.Escape(DecisionCollection(target))}", null, cancellationToken).ConfigureAwait(false);
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
                // Read again, as another complete may have replaced the collection with the alias, and deleting the
                // collection by the alias's name would delete the collection behind it.
                var current = await PreviousCollectionAsync(client, name, cancellationToken).ConfigureAwait(false);
                if (current.IsFailed)
                {
                    return current.ToResult<IReadOnlyList<string>>();
                }

                if (current.Value.IsAlias)
                {
                    return current.Value.Collection == target
                        ? Result.Ok(deleted)
                        : await MoveAliasAsync(client, name, target, current.Value, cancellationToken).ConfigureAwait(false);
                }

                if (current.Value.Collection is not null)
                {
                    var dropped = await client.SendAsync(HttpMethod.Delete, $"collections/{TypesenseHttpClientWrapper.Escape(name)}", null, cancellationToken).ConfigureAwait(false);
                    if (!dropped.IsSuccess && dropped.StatusCode != HttpStatusCode.NotFound)
                    {
                        return Result.Fail<IReadOnlyList<string>>($"Deleting collection {name} to replace it with an alias failed: {dropped.Describe()}");
                    }

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
                    return Result.Fail<IReadOnlyList<string>>($"Alias {name} now points at rebuild index {target}, but deleting the collection it pointed at before, {old}, failed: {dropped.Describe()}. Delete {old} yourself.");
                }

                await DeleteDecisionAsync(client, old, cancellationToken).ConfigureAwait(false);
                deleted = dropped.IsSuccess ? [old] : [];
            }
        }

        // Searches through the alias read the rebuild collection's fields from now on.
        schema.Forget(client, name);
        return Result.Ok(deleted);
    }

    /// <summary>
    /// Waits up to <see cref="CompletionWait"/> for the alias <paramref name="name"/> to point at <paramref name="target"/>,
    /// and returns what it points at then.
    /// </summary>
    private static async Task<Result<(string? Collection, bool IsAlias)>> WaitForAliasAsync(TypesenseHttpClientWrapper client, string name, string target,
        CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow + CompletionWait;
        while (true)
        {
            var current = await PreviousCollectionAsync(client, name, cancellationToken).ConfigureAwait(false);
            if (current.IsFailed || current.Value.Collection == target || DateTimeOffset.UtcNow >= until)
            {
                return current;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
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
    /// Deletes the collection the alias <paramref name="name"/> points at, its decision collection and the alias:
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

        await DeleteDecisionAsync(client, live, cancellationToken).ConfigureAwait(false);
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
