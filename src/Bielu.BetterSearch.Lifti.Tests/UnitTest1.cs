using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

public class UnitTest1(IServiceProvider collection) : DepedenyInjectionTestBase<LiftiIndexingProvider>(collection)
{
    [Fact]
    public void CanRetrieveLiftiSpecificServices()
    {
    }
}
