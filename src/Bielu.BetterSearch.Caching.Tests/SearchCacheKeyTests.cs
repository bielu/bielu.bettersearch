using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Tests;
using Shouldly;

namespace Bielu.BetterSearch.Caching.Tests;

public class SearchCacheKeyTests
{
    private static string Key(ISearchQuery query, string provider = "provider", string? keyPart = null) =>
        SearchCacheKey.Build(query, provider, keyPart);

    [Fact]
    public void Build_EqualQueries_SameKey()
    {
        Key(Queries.Term("products")).ShouldBe(Key(Queries.Term("products")));
    }

    public static TheoryData<string, SearchQuery> Changed => new()
    {
        { "page", Queries.Term("products", page: 2) },
        { "page size", With(q => q.PageSize = 20) },
        { "boost", Queries.Term("products", boost: 2) },
        { "sort direction", Queries.Term("products", desc: true) },
        { "sort field", With(q => q.SortOrder[0].FieldName = "Born") },
        { "value", Queries.Term("products", value: "john") },
        { "value type", With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = 1) },
        { "occurrence", With(q => q.Query[Occurrence.Must][0].Occurrence = Occurrence.Should) },
        { "occurrence key", With(q => (q.Query[Occurrence.Should], q.Query[Occurrence.Must]) = (q.Query[Occurrence.Must], [])) },
        { "index", Queries.Term("articles") },
        { "properties", With(q => q.Properties = ["Name"]) },
        { "post filter", With(q => q.PostFilterQuery[Occurrence.Filter] = [new BoolSearchSubQuery()]) },
        { "highlight", With(q => q.HighlightQueries.Add(new BaseHighlightQuery { Field = "Name" })) },
        { "nested", With(q => q.Add(Occurrence.Must, new BoolSearchSubQuery())) }
    };

    private static SearchQuery With(Action<SearchQuery> change)
    {
        var query = Queries.Term("products");
        change(query);
        return query;
    }

    [Theory]
    [MemberData(nameof(Changed))]
    public void Build_QueriesDifferInOneSetting_DifferentKeys(string setting, SearchQuery changed)
    {
        Key(changed).ShouldNotBe(Key(Queries.Term("products")), setting);
    }

    [Fact]
    public void Build_DifferentProviderOrKeyPart_DifferentKeys()
    {
        var query = Queries.Term("products");
        Key(query, provider: "other").ShouldNotBe(Key(query));
        Key(query, keyPart: "tenant-a").ShouldNotBe(Key(query));
        Key(query, keyPart: "tenant-a").ShouldNotBe(Key(query, keyPart: "tenant-b"));
    }

    [Fact]
    public void Build_StringAndNumberWithSameText_DifferentKeys()
    {
        var text = With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = "3");
        var number = With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = 3);
        Key(text).ShouldNotBe(Key(number));
    }

    [Fact]
    public void Build_CommaDecimalCulture_SameKeyAsInvariant()
    {
        var query = With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = 3.5);
        var invariant = SearchCacheKey.Canonical(query);

        using var culture = CultureScope.CommaDecimal();

        SearchCacheKey.Canonical(query).ShouldBe(invariant);
        invariant.ShouldContain("Double:3\"3.5\"");
    }

    [Fact]
    public void Build_DictionaryInsertionOrderDiffers_SameKey()
    {
        var first = Queries.Term("products");
        first.Add(Occurrence.Should, new TermSubQuery { Field = "City", Value = "Warsaw" });
        var second = new SearchQuery { Index = "products", Page = 1, PageSize = 10, SortOrder = first.SortOrder };
        second.Add(Occurrence.Should, new TermSubQuery { Field = "City", Value = "Warsaw" });
        second.Add(Occurrence.Must, new TermSubQuery { Field = "Name", Value = "jane", BoostValue = 1 });

        Key(second).ShouldBe(Key(first));
    }

    [Fact]
    public void Build_JsonElementValuesDiffer_DifferentKeys()
    {
        var jane = With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = JsonSerializer.SerializeToElement("jane"));
        var john = With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = JsonSerializer.SerializeToElement("john"));
        var janeAgain = With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = JsonSerializer.SerializeToElement("jane"));

        Key(jane).ShouldNotBe(Key(john));
        Key(jane).ShouldBe(Key(janeAgain));
        Key(jane).ShouldNotBe(Key(With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = "jane")));
    }

    [Fact]
    public void Build_JsonNodeValuesDiffer_DifferentKeys()
    {
        var jane = With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = JsonValue.Create("jane"));
        var john = With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = JsonValue.Create("john"));
        var array = With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = new JsonArray("jane"));
        var otherArray = With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = new JsonArray("john"));

        Key(jane).ShouldNotBe(Key(john));
        Key(array).ShouldNotBe(Key(otherArray));
    }

    [Fact]
    public void TryBuild_QueryHoldsTypeOutsideQueryModel_ReturnsNull()
    {
        var query = With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = new Uri("https://example.com/a"));
        SearchCacheKey.TryBuild(query, "provider", null).ShouldBeNull();
    }

    [Fact]
    public void Build_MaxEditsDiffers_DifferentKeys()
    {
        static SearchQuery Fuzzy(int? maxEdits) =>
            With(q => q.Query[Occurrence.Must] = [new FuzzySubQuery { Field = "Name", Value = "jane", MaxEdits = maxEdits }]);

        Key(Fuzzy(1)).ShouldNotBe(Key(Fuzzy(2)));
        Key(Fuzzy(1)).ShouldNotBe(Key(Fuzzy(null)));
        Key(Fuzzy(1)).ShouldBe(Key(Fuzzy(1)));
    }

    [Fact]
    public void Build_CustomSubQueryType_HasKeyFromItsProperties()
    {
        var jane = With(q => q.Query[Occurrence.Must] = [new CustomSubQuery { Text = "jane" }]);
        var john = With(q => q.Query[Occurrence.Must] = [new CustomSubQuery { Text = "john" }]);

        Key(jane).ShouldNotBe(Key(john));
    }

    private sealed class CustomSubQuery : ISearchSubQuery
    {
        public string Text { get; set; } = string.Empty;
        public Occurrence Occurrence { get; set; } = Occurrence.Must;
        public int BoostValue { get; set; } = 1;
    }

    [Fact]
    public void TryBuild_QueryHoldsDelegate_ReturnsNull()
    {
        var query = With(q => ((TermSubQuery)q.Query[Occurrence.Must][0]).Value = new Func<int>(() => 1));
        SearchCacheKey.TryBuild(query, "provider", null).ShouldBeNull();
    }
}
