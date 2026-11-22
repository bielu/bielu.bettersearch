using System.Reflection;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;

namespace Bielu.BetterSearch.Core.Tests;

/// <summary>
/// The members added during the beta as default interface members are required at 1.0, so an implementation or a
/// decorator that leaves one out fails to compile instead of failing when it is called. <c>PingAsync</c> stays an
/// optional capability with a default.
/// </summary>
public class InterfaceContractTests
{
    public static TheoryData<Type, string> RequiredMembers => new()
    {
        { typeof(ISearchProviderAsync), nameof(ISearchProviderAsync.GetDocumentAsync) },
        { typeof(ISearchProviderAsync), nameof(ISearchProviderAsync.CountAsync) },
        { typeof(ISearchServiceAsync), nameof(ISearchServiceAsync.GetDocumentAsync) },
        { typeof(ISearchServiceAsync), nameof(ISearchServiceAsync.CountAsync) },
        { typeof(IIndexingProviderAsync), nameof(IIndexingProviderAsync.UpdateDocumentAsync) },
        { typeof(IIndexingServiceAsync), nameof(IIndexingServiceAsync.UpdateDocumentAsync) },
        { typeof(ISearchResult), "get_" + nameof(ISearchResult.Aggregations) },
        { typeof(ISearchHit), "get_" + nameof(ISearchHit.Highlights) },
        { typeof(IBucket), "get_" + nameof(IBucket.Aggregations) },
    };

    [Theory]
    [MemberData(nameof(RequiredMembers))]
    public void Member_RequiredAtOnePointZero_IsAbstract(Type type, string name)
    {
        Method(type, name).IsAbstract.ShouldBeTrue($"{type.Name}.{name} has a default implementation.");
    }

    [Fact]
    public void PingAsync_OptionalCapability_HasDefaultImplementation()
    {
        Method(typeof(ISearchProviderAsync), nameof(ISearchProviderAsync.PingAsync)).IsAbstract.ShouldBeFalse();
    }

    private static MethodInfo Method(Type type, string name) =>
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(method => method.Name == name).ShouldHaveSingleItem();
}
