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

// The CommunityToolkit integration pins the image and generates the master key. Keep the image in step
// with the MeiliSearch client in Directory.Packages.props. Its HTTP endpoint also serves Meilisearch's
// built-in search preview.
var meilisearch = builder.AddMeilisearch("meilisearch")
    .WithUrlForEndpoint("http", url => url.DisplayText = "Search preview");

// A UI for browsing indexes, documents and settings and for trying queries. The browser talks to
// Meilisearch directly, so the UI is given the host URL rather than the container network one.
builder.AddContainer("meilisearch-ui", "eyeix/meilisearch-ui", "v0.15.3")
    .WithHttpEndpoint(targetPort: 24900, name: "http")
    .WithEnvironment("SINGLETON_MODE", "true")
    .WithEnvironment("SINGLETON_HOST",
        new EndpointReference(meilisearch.Resource, "http", KnownNetworkIdentifiers.LocalhostNetwork))
    .WithEnvironment("SINGLETON_API_KEY", meilisearch.Resource.MasterKeyParameter)
    .WithUrlForEndpoint("http", url => url.DisplayText = "Meilisearch UI")
    .WaitFor(meilisearch)
    .WithParentRelationship(meilisearch);

// OpenSearch.Client 2.x, pinned in Directory.Packages.props, supports OpenSearch 2.x and 3.x servers
// (see opensearch-net's COMPATIBILITY.md); keep the image within what the client supports.
builder.AddContainer("opensearch", "opensearchproject/opensearch", "3.3.2")
    .WithHttpEndpoint(targetPort: 9200, name: "http")
    .WithEnvironment("discovery.type", "single-node")
    .WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
    .WithEnvironment("DISABLE_INSTALL_DEMO_CONFIG", "true")
    .WithEnvironment("cluster.routing.allocation.disk.threshold_enabled", "false")
    .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms512m -Xmx512m")
    .WithHttpHealthCheck("/_cluster/health", endpointName: "http");

await builder.Build().RunAsync().ConfigureAwait(false);
