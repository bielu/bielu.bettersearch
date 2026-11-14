using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.PostgreSql.Tests;

/// <summary>Each write commits before it returns, so the default services already return only once a write is searchable.</summary>
public sealed class PostgreSqlSearchableWritesTests(IServiceProvider services) : SearchableWritesTestBase(services);
