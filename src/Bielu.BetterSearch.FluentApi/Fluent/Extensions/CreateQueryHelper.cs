using System.Globalization;
using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.FluentApi.Fluent.RangeQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.Extensions
{
    public static class CreateQueryHelper
    {
        /// <summary>
        /// Adds <paramref name="subQuery"/> with its own occurrence when one was chosen, else with the parent's.
        /// </summary>
        private static void Add(this ICreatableSearchQuery<Occurance, ISearchSubQuery> target, Occurance parent,
            Occurance? own, ISearchSubQuery subQuery)
        {
            var occurance = own ?? parent;
            subQuery.Occurance = occurance;
            target.Add(occurance, subQuery);
        }

        /// <summary>
        /// Builds the sub-query <paramref name="query"/> configures and adds it to <paramref name="target"/>, the one
        /// implementation behind the top-level and the nested <c>Create…Query</c> methods.
        /// </summary>
        private static TConfigurator Create<TConfigurator, TSubQuery>(this TConfigurator configurator,
            ICreatableSearchQuery<Occurance, ISearchSubQuery> target, Occurance parent, Action<TSubQuery> query)
            where TSubQuery : SubQueryConfigurator<TSubQuery>, new()
        {
            var subQuery = new TSubQuery();
            query.Invoke(subQuery);
            target.Add(parent, subQuery.Occurance, subQuery.Built);
            return configurator;
        }

        public static IQueryConfigurator CreateTermQuery(this IQueryConfigurator configurator,
            Action<TermQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IQueryConfigurator CreateTermsQuery(this IQueryConfigurator configurator,
            Action<TermsQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IQueryConfigurator CreateFuzzyQuery(this IQueryConfigurator configurator,
            Action<FuzzyQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IQueryConfigurator CreatePrefixQuery(this IQueryConfigurator configurator,
            Action<PrefixQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IQueryConfigurator CreatePrefixPhraseQuery(this IQueryConfigurator configurator,
            Action<PrefixPhraseQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IQueryConfigurator CreateNumericRangeQuery(this IQueryConfigurator configurator,
            Action<NumericRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IQueryConfigurator CreateLongRangeQuery(this IQueryConfigurator configurator,
            Action<LongRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IQueryConfigurator CreateStringRangeQuery(this IQueryConfigurator configurator,
            Action<StringRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IQueryConfigurator CreateDateRangeQuery(this IQueryConfigurator configurator,
            Action<DateRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IQueryConfigurator CreateGoeDistanceQuery(this IQueryConfigurator configurator,
            Action<SpatialQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IQueryConfigurator CreateLuceneQuery(this IQueryConfigurator configurator,
            Action<LuceneQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);

        public static IBaseQueryConfigurator CreateTermQuery(this IBaseQueryConfigurator configurator,
            Action<TermQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IBaseQueryConfigurator CreateTermsQuery(this IBaseQueryConfigurator configurator,
            Action<TermsQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IBaseQueryConfigurator CreateFuzzyQuery(this IBaseQueryConfigurator configurator,
            Action<FuzzyQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IBaseQueryConfigurator CreatePrefixQuery(this IBaseQueryConfigurator configurator,
            Action<PrefixQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IBaseQueryConfigurator CreatePrefixPhraseQuery(this IBaseQueryConfigurator configurator,
            Action<PrefixPhraseQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IBaseQueryConfigurator CreateNumericRangeQuery(this IBaseQueryConfigurator configurator,
            Action<NumericRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IBaseQueryConfigurator CreateLongRangeQuery(this IBaseQueryConfigurator configurator,
            Action<LongRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IBaseQueryConfigurator CreateStringRangeQuery(this IBaseQueryConfigurator configurator,
            Action<StringRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IBaseQueryConfigurator CreateDateRangeQuery(this IBaseQueryConfigurator configurator,
            Action<DateRangeQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IBaseQueryConfigurator CreateGoeDistanceQuery(this IBaseQueryConfigurator configurator,
            Action<SpatialQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);
        public static IBaseQueryConfigurator CreateLuceneQuery(this IBaseQueryConfigurator configurator,
            Action<LuceneQueryConfigurator> query) =>
            configurator.Create(configurator.Query, configurator.Occurance, query);

        public static IQueryConfigurator CreateBoolQuery(this IQueryConfigurator configurator,
            Action<BooleanQueryConfigurator> query)
        {
            var booleanQuery = new BooleanQueryConfigurator();
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurance, null, booleanQuery.Query);
            return configurator;
        }
        /// <summary>
        /// Keeps only documents whose <see cref="CultureField.Name"/> field holds <paramref name="culture"/>,
        /// as a <c>FILTER</c> clause. Index the culture with <see cref="CultureField.Value"/>.
        /// </summary>
        public static IBaseQueryConfigurator FilterByCulture(this IBaseQueryConfigurator configurator, string culture)
        {
            configurator.Query.Add(Occurance.FILTER, new TermSubQuery
            {
                Occurance = Occurance.FILTER,
                Field = CultureField.Name,
                Value = CultureField.Value(culture)
            });
            return configurator;
        }

        /// <inheritdoc cref="FilterByCulture(IBaseQueryConfigurator, string)"/>
        public static IBaseQueryConfigurator FilterByCulture(this IBaseQueryConfigurator configurator, CultureInfo culture) =>
            configurator.FilterByCulture(culture.Name);

        public static IBaseQueryConfigurator CreateBoolQuery(this IBaseQueryConfigurator configurator,
            Action<BooleanQueryConfigurator> query)
        {
            var booleanQuery = new BooleanQueryConfigurator();
            booleanQuery.Occurance = configurator.Occurance;
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurance, null, booleanQuery.Query);
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
            booleanQuery.Occurance = configurator.Occurance;
            query.Invoke(booleanQuery);
            booleanQuery.Query.Occurance = configurator.Occurance;
            configurator.Query.PostFilterQuery ??= new Dictionary<Occurance, List<INestableQuery>>();
            if (!configurator.Query.PostFilterQuery.TryGetValue(configurator.Occurance, out var clauses))
            {
                clauses = [];
                configurator.Query.PostFilterQuery.Add(configurator.Occurance, clauses);
            }

            clauses.Add(booleanQuery.Query);
            return configurator;
        }
    }
}
