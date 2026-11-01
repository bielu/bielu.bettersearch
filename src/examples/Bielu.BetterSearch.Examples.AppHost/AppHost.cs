var builder = DistributedApplication.CreateBuilder(args);

// A plain container rather than Aspire.Hosting.Elasticsearch: that package is frozen at 13.3.0 and is
// built against Elastic.Clients.Elasticsearch 8.x, while this repository uses the 9.x client. Keep the
// image tag in step with the client version in Directory.Packages.props.
builder.AddContainer("elasticsearch", "elasticsearch", "9.5.3")
    .WithHttpEndpoint(targetPort: 9200, name: "http")
    .WithEnvironment("discovery.type", "single-node")
    .WithEnvironment("xpack.security.enabled", "false")
    .WithEnvironment("cluster.routing.allocation.disk.threshold_enabled", "false")
    .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
    .WithHttpHealthCheck("/_cluster/health", endpointName: "http");

await builder.Build().RunAsync().ConfigureAwait(false);
