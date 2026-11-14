using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.PostgreSql.Tests;

public class QueryValueTextTests : QueryValueTextTestBase
{
    protected override string From(object? value) => QueryValueText.From(value);
}
