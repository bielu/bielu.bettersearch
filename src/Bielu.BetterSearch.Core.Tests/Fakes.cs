using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Bielu.BetterSearch.Core.Tests;

internal sealed class FakeSearchProvider : ISearchProviderAsync
{
    public Func<ISearchQuery, CancellationToken, Task<Result<ISearchResult>>> Search { get; set; } =
        (_, _) => Task.FromResult(Result.Ok<ISearchResult>(Results(0, 0)));

    public Func<string?, CancellationToken, Task<Result>> Ping { get; set; } = (_, _) => Task.FromResult(Result.Ok());

    public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
        Search(query, cancellationToken);

    public Task<Result> PingAsync(string? index = null, CancellationToken cancellationToken = default) => Ping(index, cancellationToken);

    public static BaseSearchResults Results(int hits, long totalCount) => new()
    {
        Items = Enumerable.Range(0, hits)
            .Select(i => (ISearchHit)new SearchHit(new BaseSearchModel { Id = i.ToString(System.Globalization.CultureInfo.InvariantCulture) }, 1))
            .ToList(),
        Pagination = new Pagination { TotalCount = totalCount },
    };
}

/// <summary>A provider that relies on the default <see cref="ISearchProviderAsync.PingAsync"/>.</summary>
internal sealed class SearchOnlyProvider : ISearchProviderAsync
{
    public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Ok<ISearchResult>(FakeSearchProvider.Results(0, 0)));
}

internal class FakeIndexingProvider : IIndexingProviderAsync
{
    public Result<int> BulkIndexResult { get; set; } = Result.Ok(0);
    public Result<int> RemoveDocumentsResult { get; set; } = Result.Ok(0);
    public Result<long?> RemoveAllResult { get; set; } = Result.Ok<long?>(null);
    public Result RemoveResult { get; set; } = Result.Ok();

    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok());

    public Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default) =>
        Task.FromResult(BulkIndexResult);

    public Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) => Task.FromResult(RemoveResult);

    public Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult(RemoveDocumentsResult);

    public Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index, CancellationToken cancellationToken = default) =>
        Task.FromResult(RemoveAllResult);

    public Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok(true));
    public Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok(true));
    public Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok(true));
    public Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok(true));
}

internal sealed class AcceptingValidator : IDocumentValidatorAsync
{
    public Task<Result<bool>> ValidateDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Ok(true));
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (Entries)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
