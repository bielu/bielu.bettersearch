using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Lifti.Services;
using Bielu.BetterSearch.Tests;
using Lifti;
using Lifti.Querying;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

public class DepedencyInjection(IServiceProvider collection) : DepedenyInjectionTestBase<LiftiIndexingProviderAsync, IFullTextIndex<string>, IQuery>(collection)
{
    protected override void AddProvider(Configurator search) => search.AddLiftiSearch();

    protected override void AddProvider(Configurator search, string key) => search.AddLiftiSearch(key);

    [Fact]
    public void LiftiIndexManagerShouldBeRegistered() => ServiceShouldBeRegistered(typeof(ILiftiIndexManager));

    [Fact]
    public void SearchProviderShouldBeTypeOfLiftiSearchProviderAsync() => ServiceShouldBeTypeOf(typeof(ISearchProviderAsync), typeof(LiftiSearchProviderAsync));

    [Fact]
    public void IndexingProviderShouldBeTypeOfLiftiIndexingProviderAsync() => ServiceShouldBeTypeOf(typeof(IIndexingProviderAsync), typeof(LiftiIndexingProviderAsync));
}
