# Fluent API

`Bielu.BetterSearch.FluentApi` builds an `ISearchQuery` without constructing the query model by hand. It builds
the model only: what each engine does with a query is in the [feature matrix](feature-matrix.md).

```csharp
using Bielu.BetterSearch.FluentApi;
using Bielu.BetterSearch.FluentApi.Fluent.Extensions;

var query = FluentApiSearchQueryCreator.Create()
    .WithSearchQuery(search => search
        .WithIndex("people")
        .WithPage(1)
        .WithPageSize(20)
        .SortByDescending("Born")
        .SortBy("Name")
        .WithProperties("Name", "City"))
    .CreateSearchQuery(search => search
        .CreateTermQuery(term => term.WithField("Type").WithValue("person"))
        .CreateBoolQuery(cities => cities
            .CreateTermQuery(term => term.WithField("City").WithValue("Warsaw").Should())
            .CreateTermQuery(term => term.WithField("City").WithValue("Krakow").Should()))
        .FilterByCulture("en-US"));
```

`new FluentApiSearchQueryCreator(query)` starts from a query you already have; `WithSearchQuery` and
`CreateSearchQuery` both add to it.

## Query settings

`WithSearchQuery` sets what applies to the whole request:

| Method | Sets |
|---|---|
| `WithIndex(name)` | `Index` |
| `WithPage(page)`, `WithPageSize(size)` | `Page`, `PageSize` |
| `SortBy(field, descending = false)`, `SortByDescending(field)` | adds to `SortOrder`, after any sort added before |
| `WithProperties(params fields)` | `Properties`, the fields returned on each hit, replacing any set before |

## Clauses

`CreateSearchQuery` adds clauses to the query, and `CreateBoolQuery` nests them. Every method below is available at
both levels:

| Method | Builds | Options |
|---|---|---|
| `CreateTermQuery` | `TermSubQuery` | `WithField`, `WithValue` |
| `CreateTermsQuery` | `TermsSubQuery` | `WithField`, `WithValue(params values)` |
| `CreatePrefixQuery` | `PrefixSubQuery` | `WithField`, `WithValue` |
| `CreatePrefixPhraseQuery` | `PrefixPhraseSubQuery` | `WithField`, `WithValue` |
| `CreateFuzzyQuery` | `FuzzyQuery` | `WithField`, `WithValue`, `WithFuzziness(maxEdits)` or `WithFuzziness(low, high)`, `WithPrefixLength`, `WithMaxExpansions`, `WithTranspositions` |
| `CreateNumericRangeQuery`, `CreateLongRangeQuery`, `CreateStringRangeQuery`, `CreateDateRangeQuery` | `NumericRange`, `LongRange`, `StringRange`, `DateRangeQuery` | `WithField`, `WithMinValue(value, includeEdge)`, `WithMaxValue(value, includeEdge)` |
| `CreateGoeDistanceQuery` | `SpatialSearchQuery` | `WithField`, `WithCenter(lat, lng)`, `WithDistance`, `WithUnitOfDistance` |
| `CreateLuceneQuery` | `LuceneSubQuery` | `WithQuery(luceneQuery)` |
| `CreateBoolQuery` | `BoolSearchSubQuery` | the methods in this table, `WithBoost` |

Each of these also has `WithBoost(boost)`, which sets the clause's `BoostValue`. The fuzzy options throw
`ArgumentOutOfRangeException` for a negative value.

### Occurrence

Each clause takes the occurrence of the query or bool it is created on, `MUST` unless you choose another. Call
`Must()`, `Should()`, `MustNot()` or `Filter()` on the clause itself to choose its own, which wins over its parent's.
The clauses of a bool default to `MUST`; call `Should()` and the others on the bool to change that default.

## Post filters

`CreatePostFilterQuery` adds a bool to `PostFilterQuery`, under the occurrence of the query (`MUST` unless you set
`Occurance` on the configurator). A post filter narrows the hits after facets are counted. Each call adds another bool.

```csharp
.CreateSearchQuery(search => search
    .CreateTermQuery(term => term.WithField("Type").WithValue("person"))
    .CreatePostFilterQuery(filter => filter
        .CreateTermQuery(term => term.WithField("City").WithValue("Warsaw"))))
```

## Highlighting

`CreateHighlighterQuery` adds one highlight request: `WithField(name)` for one field, or `ForAllFields()` for every
field. `WithQuery` highlights where its own bool query matches, rather than where the main query does.

```csharp
.CreateSearchQuery(search => search
    .CreateTermQuery(term => term.WithField("Title").WithValue("welcome"))
    .CreateHighlighterQuery(highlight => highlight.WithField("Title"))
    .CreateHighlighterQuery(highlight => highlight
        .WithField("Body")
        .WithQuery(own => own.CreateTermQuery(term => term.WithField("Body").WithValue("hello")))))
```

## Facets

`CreateAggregationQuery` adds facets: `CreateTermQuery` for a term facet, `CreateMultiTermQuery` for one over several
fields, and `CreateFilterQuery` for a filter facet.
