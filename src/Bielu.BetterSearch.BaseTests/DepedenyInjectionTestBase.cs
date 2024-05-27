using Bielu.BetterSearch.Abstractions.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.Tests;

public abstract class DepedenyInjectionTestBase<T>(IServiceProvider collection)
{
    [Fact]
    public void CanRetrieveIndexingProvider()
    {
        var service = collection.GetRequiredService<IIndexingProvider>();
        service.GetType().Should().Be(typeof(T));
    }
    
}
