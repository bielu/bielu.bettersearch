using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>Meilisearch waits for each write's task, so the default services already return only once a write is searchable.</summary>
public sealed class MeilisearchSearchableWritesTests(IServiceProvider services) : SearchableWritesTestBase(services);
