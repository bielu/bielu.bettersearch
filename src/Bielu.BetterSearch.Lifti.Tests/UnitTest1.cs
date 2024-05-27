using Bielu.BetterSearch.Lifti.Services;
using Bielu.BetterSearch.Tests;
using Lifti;
using Lifti.Querying;

namespace Bielu.BetterSearch.Lifti.Tests;

public class UnitTest1(IServiceProvider collection) : DepedenyInjectionTestBase<LiftiIndexingProviderAsync,IFullTextIndex<string>,IQuery>(collection)
{
    [Fact]
    public void CanRetrieveLiftiSpecificServices()
    {
    }
}
