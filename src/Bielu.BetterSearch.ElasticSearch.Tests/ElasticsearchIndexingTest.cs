using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Testcontainers.Elasticsearch;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticsearchIndexingTest(IIndexingServiceAsync serviceAsync, IIndexingProviderAsync indexingProviderAsync)
    : IndexingTestBase(serviceAsync, indexingProviderAsync), IAsyncLifetime
{
    private readonly ElasticsearchContainer _elasticsearch
        = new ElasticsearchBuilder().WithPortBinding(9200, 9200)
            .WithPortBinding(9300, 9300)    .WithEnvironment("xpack.security.transport.ssl.enabled", "false")
            .WithEnvironment("xpack.security.http.ssl.enabled", "false").Build();

    public Task InitializeAsync()
        => _elasticsearch.StartAsync();

    public Task DisposeAsync()
        => _elasticsearch.DisposeAsync().AsTask();
}
