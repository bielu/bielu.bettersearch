using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>Lifti indexes in memory, so the default services already return only once a write is searchable.</summary>
public sealed class LiftiSearchableWritesTests(IServiceProvider services) : SearchableWritesTestBase(services);
