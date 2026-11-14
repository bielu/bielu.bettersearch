using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Diagnostics;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Core.Tests;

internal sealed class FakeSearchProvider : ISearchProviderAsync
{
    public Func<ISearchQuery, CancellationToken, Task<Result<ISearchResult>>> Search { get; set; } =
        (_, _) => Task.FromResult(Result.Ok<ISearchResult>(Results(0, 0)));

    public Func<string?, CancellationToken, Task<Result>> Ping { get; set; } = (_, _) => Task.FromResult(Result.Ok());

    public Result<SearchDocument?> GetDocumentResult { get; set; } = Result.Ok<SearchDocument?>(null);

    public Result<long> CountResult { get; set; } = Result.Ok(0L);

    public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
        Search(query, cancellationToken);

    public Task<Result> PingAsync(string? index = null, CancellationToken cancellationToken = default) => Ping(index, cancellationToken);

    public Task<Result<SearchDocument?>> GetDocumentAsync(string index, string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetDocumentResult);

    public Task<Result<long>> CountAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(CountResult);

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
    public Result<bool> UpdateResult { get; set; } = Result.Ok(true);

    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok());

    public Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default) =>
        Task.FromResult(BulkIndexResult);

    public Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) => Task.FromResult(RemoveResult);

    public Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult(RemoveDocumentsResult);

    public Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index, CancellationToken cancellationToken = default) =>
        Task.FromResult(RemoveAllResult);

    public Task<Result<bool>> UpdateDocumentAsync(string index, string id, IDictionary<string, IList<object>> fields,
        CancellationToken cancellationToken = default) => Task.FromResult(UpdateResult);

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
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (Entries)
        {
            Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}

/// <summary>An options monitor with fixed instrumentation options, or one whose options can't be read.</summary>
internal sealed class InstrumentationOptionsMonitor(bool recordErrorMessages, bool throws = false)
    : IOptionsMonitor<BetterSearchInstrumentationOptions>
{
    public BetterSearchInstrumentationOptions CurrentValue => throws
        ? throw new InvalidOperationException("Failed to convert configuration value at 'Bielu:BetterSearch:Instrumentation:RecordErrorMessages' to type 'System.Boolean'.")
        : new BetterSearchInstrumentationOptions { RecordErrorMessages = recordErrorMessages };

    public BetterSearchInstrumentationOptions Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<BetterSearchInstrumentationOptions, string?> listener) => null;
}

/// <summary>A second provider type, so a span's provider tag tells which provider a routed call reached.</summary>
internal sealed class OtherSearchProvider : ISearchProviderAsync
{
    public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Ok<ISearchResult>(FakeSearchProvider.Results(2, 2)));
}

/// <summary>A second indexing provider type, as a provider type can be added under one key only.</summary>
internal sealed class OtherIndexingProvider : FakeIndexingProvider;
