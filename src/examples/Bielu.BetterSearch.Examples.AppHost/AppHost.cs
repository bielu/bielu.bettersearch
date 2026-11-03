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

// Keep the image's major version in step with what OpenSearch.Client in Directory.Packages.props supports.
builder.AddContainer("opensearch", "opensearchproject/opensearch", "3.3.2")
    .WithHttpEndpoint(targetPort: 9200, name: "http")
    .WithEnvironment("discovery.type", "single-node")
    .WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
    .WithEnvironment("DISABLE_INSTALL_DEMO_CONFIG", "true")
    .WithEnvironment("cluster.routing.allocation.disk.threshold_enabled", "false")
    .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms512m -Xmx512m")
    .WithHttpHealthCheck("/_cluster/health", endpointName: "http");

await builder.Build().RunAsync().ConfigureAwait(false);
