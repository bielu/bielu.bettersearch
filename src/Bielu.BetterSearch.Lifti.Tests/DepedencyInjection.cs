using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services;
using Bielu.BetterSearch.Tests;
using FluentAssertions;
using Lifti;
using Lifti.Querying;

namespace Bielu.BetterSearch.Lifti.Tests;

public class DepedencyInjection(IServiceProvider collection) : DepedenyInjectionTestBase<LiftiIndexingProviderAsync,IFullTextIndex<string>,IQuery>(collection)
{
    [Fact]
    public void LiftiIndexManagerShouldBeRegistered() => ServiceShouldBeRegistered(typeof(ILiftiIndexManager));

    [Fact]
    public void SearchProviderShouldBeTypeOfLiftiSearchProviderAsync() => ServiceShouldBeTypeOf(typeof(ISearchProviderAsync), typeof(LiftiSearchProviderAsync));

    [Fact]
    public void IndexingProviderShouldBeTypeOfLiftiIndexingProviderAsync() => ServiceShouldBeTypeOf(typeof(IIndexingProviderAsync), typeof(LiftiIndexingProviderAsync));
}
