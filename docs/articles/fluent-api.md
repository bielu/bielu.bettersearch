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
| `CreateLuceneQuery` | `LuceneSubQuery` | `WithQuery(luceneQuery)`, stored unchanged; what each engine does with it is in the [feature matrix](feature-matrix.md) |
| `CreateBoolQuery` | `BoolSearchSubQuery` | the methods in this table, `WithBoost` |

Each of these also has `WithBoost(boost)`, which sets the clause's `BoostValue` and throws
`ArgumentOutOfRangeException` for a boost of 0 or less. The fuzzy options throw `ArgumentOutOfRangeException` for a
negative value.

### Occurrence

Each clause other than a bool takes the occurrence of the query or bool it is created on, `MUST` unless you choose
another. Call `Must()`, `Should()`, `MustNot()` or `Filter()` on the clause itself to choose its own, which wins over
its parent's.

A bool works differently. `CreateBoolQuery` always adds the bool under its parent's occurrence, and `Must()`,
`Should()`, `MustNot()` and `Filter()` on the bool set the default for the bool's own clauses, not where the bool
goes. The clauses of a bool nested in another bool default to `MUST`; the clauses of a bool created directly on
`CreateSearchQuery` default to the query's occurrence. So `b.Should()` does not make a bool optional: under a `MUST`
parent, the bool below is still required, and it requires a document to match `Warsaw`, because its only clause is a
`SHOULD`.

```csharp
.CreateSearchQuery(search => search
    .CreateBoolQuery(b => b.Should()
        .CreateTermQuery(term => term.WithField("City").WithValue("Warsaw"))))
```

To make a bool optional, create it on a parent whose occurrence is `SHOULD`.

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
