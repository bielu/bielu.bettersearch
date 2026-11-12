using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class QueryValueTextTests : QueryValueTextTestBase
{
    protected override string From(object? value) => QueryValueText.From(value);
}
