using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Caching.Tests;

/// <summary>A search provider that counts its calls and returns one hit naming the index and the call.</summary>
internal class CountingSearchProvider : ISearchProviderAsync
{
    private int _calls;

    public int Calls => _calls;

    /// <summary>When set, every search fails with this message.</summary>
    public string? FailWith { get; set; }

    /// <summary>When set, runs inside each search before it returns.</summary>
    public Func<CancellationToken, Task>? During { get; set; }

    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        var call = Interlocked.Increment(ref _calls);
        if (During is not null)
        {
            await During(cancellationToken);
        }

        if (FailWith is not null)
        {
            return Result.Fail<ISearchResult>(FailWith);
        }

        var document = new BaseSearchModel
        {
            Id = $"{query.Index}-{call}",
            ContentType = "page",
            CustomProperties = new Dictionary<string, List<object>> { ["call"] = [(long)call] }
        };
        ISearchResult result = new BaseSearchResults
        {
            Items = [new SearchHit(document, 1.5)],
            Pagination = new Pagination { TotalCount = 1, Page = query.Page, PageSize = query.PageSize, TotalPages = 1 }
        };
        return Result.Ok(result);
    }

    /// <summary>Counts as a call, and returns a document with the id asked for.</summary>
    public Task<Result<SearchDocument?>> GetDocumentAsync(string index, string id, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(Result.Ok<SearchDocument?>(new SearchDocument { Id = id, Index = index }));
    }

    /// <summary>Counts as a call, and returns the number of calls so far.</summary>
    public Task<Result<long>> CountAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Ok((long)Interlocked.Increment(ref _calls)));
}

/// <summary>
/// An indexing provider whose writes all succeed, or all fail when <see cref="Fail"/> is set, or fail for the document
/// <see cref="FailId"/> only, and for a batch that holds it.
/// </summary>
internal class FakeIndexingProvider : IIndexingProviderAsync
{
    public bool Fail { get; set; }

    public string? FailId { get; set; }

    private Result Outcome() => Fail ? Result.Fail("write failed") : Result.Ok();

    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
        Task.FromResult(document.Id == FailId ? Result.Fail("write failed") : Outcome());

    public Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default) =>
        Task.FromResult(documents.Any(document => document.Id == FailId) ? Result.Fail<int>("write failed") : Outcome().ToResult(documents.Count()));

    public Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) =>
        Task.FromResult(Outcome());

    public Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult(Outcome().ToResult(ids.Count()));

    public Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index, CancellationToken cancellationToken = default) =>
        Task.FromResult(Outcome().ToResult<long?>(0));

    public Task<Result<bool>> UpdateDocumentAsync(string index, string id, IDictionary<string, IList<object>> fields,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Outcome().ToResult(true));

    public Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok(true));
    public Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok(true));
    public Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok(true));
    public Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok(true));
}

/// <summary>A second provider type, for registering two providers under two keys.</summary>
internal sealed class SecondCountingSearchProvider : CountingSearchProvider;

internal sealed class SecondFakeIndexingProvider : FakeIndexingProvider;

internal sealed class SortField : ISortOrderField
{
    public string FieldName { get; set; } = string.Empty;
    public bool Desc { get; set; }
    public int Order { get; set; }
}

internal static class Queries
{
    public static SearchQuery Term(string index, string value = "jane", int page = 1, int boost = 1, bool desc = false)
    {
        var query = new SearchQuery { Index = index, Page = page, PageSize = 10 };
        query.Add(Occurance.MUST, new TermSubQuery { Field = "Name", Value = value, BoostValue = boost });
        query.SortOrder.Add(new SortField { FieldName = "Name", Desc = desc });
        return query;
    }
}
