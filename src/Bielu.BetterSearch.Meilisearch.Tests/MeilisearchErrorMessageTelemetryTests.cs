using System.Collections.Concurrent;
using System.Diagnostics;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Diagnostics;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>
/// A Lucene syntax error quotes the whole query, so its message must stay out of the span and the logs unless
/// <see cref="BetterSearchInstrumentationOptions.RecordErrorMessages"/> is on. The query fails to translate, so no
/// server is needed.
/// </summary>
public class MeilisearchErrorMessageTelemetryTests
{
    private const string Email = "alice@example.com";

    [Fact]
    public async Task SearchAsync_LuceneSyntaxErrorWithMessagesOff_RecordsNoQueryText()
    {
        var (activity, logs, result) = await SearchWithBrokenLuceneAsync(recordErrorMessages: null);

        result.Errors.ShouldHaveSingleItem().Message.ShouldContain(Email);
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBeNull();
        activity.Tags.ShouldNotContain(tag => tag.Value != null && tag.Value.Contains(Email));
        activity.GetTagItem("error.type").ShouldBe(typeof(FluentResults.Error).FullName);
        var log = logs.ShouldHaveSingleItem();
        log.Level.ShouldBe(LogLevel.Warning);
        log.Message.ShouldNotContain(Email);
        log.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task SearchAsync_LuceneSyntaxErrorWithMessagesOn_RecordsTheMessage()
    {
        var (activity, logs, _) = await SearchWithBrokenLuceneAsync(recordErrorMessages: "true");

        activity.StatusDescription.ShouldNotBeNull().ShouldContain(Email);
        logs.ShouldHaveSingleItem().Message.ShouldContain(Email);
    }

    private static async Task<(Activity Activity, List<LogEntry> Logs, FluentResults.Result<Abstractions.Models.ISearchResult> Result)>
        SearchWithBrokenLuceneAsync(string? recordErrorMessages)
    {
        var index = $"telemetry-{Guid.NewGuid():N}";
        var settings = new Dictionary<string, string?>
        {
            ["Bielu:BetterSearch:Meilisearch:SharedIndexSettings:ConnectionString"] = "http://localhost:7700",
        };
        if (recordErrorMessages is not null)
        {
            settings[$"{BetterSearchInstrumentationOptions.SectionKey}:RecordErrorMessages"] = recordErrorMessages;
        }

        var logs = new List<LogEntry>();
        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddProvider(new RecordingLoggerProvider(logs)))
            .AddBetterSearch(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), search => search.AddMeilisearch());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ISearchServiceAsync>();

        var activities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BetterSearchInstrumentation.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (Equals(activity.GetTagItem("db.collection.name"), index))
                {
                    activities.Enqueue(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        var query = new SearchQuery { Index = index };
        query.Add(Occurance.MUST, new LuceneSubQuery { LuceneQuery = $"email:\"{Email}" });
        var result = await service.SearchAsync(query, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        List<LogEntry> serviceLogs;
        lock (logs)
        {
            serviceLogs = logs.Where(log => log.Category.StartsWith("Bielu.BetterSearch.Services", StringComparison.Ordinal)).ToList();
        }

        return (activities.ShouldHaveSingleItem(), serviceLogs, result);
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

    private sealed class RecordingLoggerProvider(List<LogEntry> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, entries);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(string category, List<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (entries)
            {
                entries.Add(new LogEntry(category, logLevel, formatter(state, exception), exception));
            }
        }
    }
}
