using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Npgsql;

namespace Bielu.BetterSearch.PostgreSql.Tests;

public sealed class PostgreSqlIndexingTest(
    IIndexingServiceAsync serviceAsync,
    IIndexingProviderAsync indexingProviderAsync,
    IClientFactoryAsync<NpgsqlDataSource> dataSourceFactory)
    : IndexingTestBase(serviceAsync, indexingProviderAsync)
{
    // Every document of a write is stored in one transaction.
    protected override bool BulkIndexingIsAllOrNothing => true;

    // jsonb can't hold the null character, which a .NET string can.
    protected override SearchDocument? CreateDocumentRejectedByEngine(string index) => new()
    {
        Id = "2",
        Type = "TestDocument",
        Index = index,
        Fields = new Dictionary<string, IList<object>> { ["Count"] = ["null \0 character"] },
    };

    protected override async Task<long> CountDocumentsAsync(string index)
    {
        var dataSource = await dataSourceFactory.GetOrCreateClientAsync(index);
        await using var command = dataSource.CreateCommand($"SELECT count(*) FROM \"public\".\"{index}\"");
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
