using System.Globalization;
using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.FluentApi.Fluent.RangeQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.Extensions
{
    /// <summary>Extension methods that add sub-queries to a query configurator.</summary>
    public static class CreateQueryHelper
    {
        /// <summary>
        /// Adds <paramref name="subQuery"/> with its own occurrence when one was chosen, else with the parent's.
        /// </summary>
        private static void Add(this ICreatableSearchQuery target, Occurrence parent,
            Occurrence? own, ISearchSubQuery subQuery)
        {
            target.Add(own ?? parent, subQuery);
        }

        /// <summary>
        /// Builds the sub-query <paramref name="query"/> configures and adds it to <paramref name="target"/>, the one
        /// implementation behind the top-level and the nested <c>Create…Query</c> methods.
        /// </summary>
        private static TConfigurator Create<TConfigurator, TSubQuery>(this TConfigurator configurator,
            ICreatableSearchQuery target, Occurrence parent, Action<TSubQuery> query)
            where TSubQuery : SubQueryConfigurator<TSubQuery>, new()
        {
            var subQuery = new TSubQuery();
            query.Invoke(subQuery);
            target.Add(parent, subQuery.Occurrence, subQuery.Built);
            return configurator;
        }

        /// <summary>Adds a term query (<see cref="TermSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IQueryConfigurator CreateTermQuery(this IQueryConfigurator configurator,
            Action<TermQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a terms query (<see cref="TermsSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IQueryConfigurator CreateTermsQuery(this IQueryConfigurator configurator,
            Action<TermsQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a fuzzy query (<see cref="FuzzySubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IQueryConfigurator CreateFuzzyQuery(this IQueryConfigurator configurator,
            Action<FuzzyQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a prefix query (<see cref="PrefixSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IQueryConfigurator CreatePrefixQuery(this IQueryConfigurator configurator,
            Action<PrefixQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a prefix phrase query (<see cref="PrefixPhraseSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IQueryConfigurator CreatePrefixPhraseQuery(this IQueryConfigurator configurator,
            Action<PrefixPhraseQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a numeric range query (<see cref="NumericRangeSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IQueryConfigurator CreateNumericRangeQuery(this IQueryConfigurator configurator,
            Action<NumericRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a long range query (<see cref="LongRangeSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IQueryConfigurator CreateLongRangeQuery(this IQueryConfigurator configurator,
            Action<LongRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a string range query (<see cref="StringRangeSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IQueryConfigurator CreateStringRangeQuery(this IQueryConfigurator configurator,
            Action<StringRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a date range query (<see cref="DateRangeSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IQueryConfigurator CreateDateRangeQuery(this IQueryConfigurator configurator,
            Action<DateRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a geo distance query (<see cref="SpatialSearchSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IQueryConfigurator CreateGeoDistanceQuery(this IQueryConfigurator configurator,
            Action<SpatialQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a raw Lucene query (<see cref="LuceneSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IQueryConfigurator CreateLuceneQuery(this IQueryConfigurator configurator,
            Action<LuceneQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);

        /// <summary>Adds a term query (<see cref="TermSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreateTermQuery(this IBaseQueryConfigurator configurator,
            Action<TermQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a terms query (<see cref="TermsSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreateTermsQuery(this IBaseQueryConfigurator configurator,
            Action<TermsQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a fuzzy query (<see cref="FuzzySubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreateFuzzyQuery(this IBaseQueryConfigurator configurator,
            Action<FuzzyQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a prefix query (<see cref="PrefixSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreatePrefixQuery(this IBaseQueryConfigurator configurator,
            Action<PrefixQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a prefix phrase query (<see cref="PrefixPhraseSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreatePrefixPhraseQuery(this IBaseQueryConfigurator configurator,
            Action<PrefixPhraseQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a numeric range query (<see cref="NumericRangeSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreateNumericRangeQuery(this IBaseQueryConfigurator configurator,
            Action<NumericRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a long range query (<see cref="LongRangeSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreateLongRangeQuery(this IBaseQueryConfigurator configurator,
            Action<LongRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a string range query (<see cref="StringRangeSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreateStringRangeQuery(this IBaseQueryConfigurator configurator,
            Action<StringRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a date range query (<see cref="DateRangeSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreateDateRangeQuery(this IBaseQueryConfigurator configurator,
            Action<DateRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a geo distance query (<see cref="SpatialSearchSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreateGeoDistanceQuery(this IBaseQueryConfigurator configurator,
            Action<SpatialQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);
        /// <summary>Adds a raw Lucene query (<see cref="LuceneSubQuery"/>) that <paramref name="query"/> configures, with the occurrence it chooses or else the configurator's.</summary>
        /// <param name="configurator">The configurator whose query gets the sub-query.</param>
        /// <param name="query">Configures the sub-query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreateLuceneQuery(this IBaseQueryConfigurator configurator,
            Action<LuceneQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurrence, query);

        /// <summary>Adds a nested bool query that <paramref name="query"/> configures, under the configurator's occurrence.</summary>
        /// <param name="configurator">The configurator whose query gets the bool query.</param>
        /// <param name="query">Configures the bool query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IQueryConfigurator CreateBoolQuery(this IQueryConfigurator configurator,
            Action<BooleanQueryConfigurator> query)
        {
            var booleanQuery = new BooleanQueryConfigurator();
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurrence, null, booleanQuery.Query);
            return configurator;
        }
        /// <summary>
        /// Keeps only documents whose <see cref="CultureField.Name"/> field holds <paramref name="culture"/>,
        /// as a <c>Filter</c> clause. Index the culture with <see cref="CultureField.Value"/>.
        /// </summary>
        public static IBaseQueryConfigurator FilterByCulture(this IBaseQueryConfigurator configurator, string culture)
        {
            configurator.Query.Add(Occurrence.Filter, new TermSubQuery
            {
                Occurrence = Occurrence.Filter,
                Field = CultureField.Name,
                Value = CultureField.Value(culture)
            });
            return configurator;
        }

        /// <inheritdoc cref="FilterByCulture(IBaseQueryConfigurator, string)"/>
        public static IBaseQueryConfigurator FilterByCulture(this IBaseQueryConfigurator configurator, CultureInfo culture) =>
            configurator.FilterByCulture(culture.Name);

        /// <summary>Adds a bool query that <paramref name="query"/> configures, under the configurator's occurrence, which its own sub-queries also default to.</summary>
        /// <param name="configurator">The configurator whose query gets the bool query.</param>
        /// <param name="query">Configures the bool query.</param>
        /// <returns><paramref name="configurator"/>, for chaining.</returns>
        public static IBaseQueryConfigurator CreateBoolQuery(this IBaseQueryConfigurator configurator,
            Action<BooleanQueryConfigurator> query)
        {
            var booleanQuery = new BooleanQueryConfigurator();
            booleanQuery.Occurrence = configurator.Occurrence;
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurrence, null, booleanQuery.Query);
            return configurator;
        }

        /// <summary>
        /// Adds a bool query to <see cref="ISearchQuery.PostFilterQuery"/>, under the configurator's occurrence. A post
        /// filter narrows the hits after facets are counted, so facets still count the documents it leaves out.
        /// </summary>
        public static IBaseQueryConfigurator CreatePostFilterQuery(this IBaseQueryConfigurator configurator,
            Action<BooleanQueryConfigurator> query)
        {
            var booleanQuery = new BooleanQueryConfigurator();
            booleanQuery.Occurrence = configurator.Occurrence;
            query.Invoke(booleanQuery);
            booleanQuery.Query.Occurrence = configurator.Occurrence;
            configurator.Query.PostFilterQuery ??= [];
            configurator.Query.PostFilterQuery.Add(booleanQuery.Query);
            return configurator;
        }
    }
}
