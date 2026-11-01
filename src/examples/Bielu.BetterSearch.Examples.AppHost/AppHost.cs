var builder = DistributedApplication.CreateBuilder(args);

// The integration's default image is older than the Elasticsearch client this repository uses, so pin
// the server to the client version in Directory.Packages.props.
builder.AddElasticsearch("elasticsearch")
    .WithImageTag("9.5.3")
    .WithEnvironment("cluster.routing.allocation.disk.threshold_enabled", "false")
    .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m");

await builder.Build().RunAsync().ConfigureAwait(false);
