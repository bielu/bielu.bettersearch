using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Typesense.Tests;

/// <summary>Typesense applies each write before it answers, so the default services already return only once a write is searchable.</summary>
public sealed class TypesenseSearchableWritesTests(IServiceProvider services) : SearchableWritesTestBase(services);
