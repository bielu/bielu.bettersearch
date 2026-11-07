using System.Globalization;
using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.FluentApi.Fluent.RangeQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent.Extensions
{
    public static class CreateQueryHelper
    {

        public static IQueryConfigurator CreateTermQuery(this IQueryConfigurator configurator,
            Action<TermQueryConfigurator> query)
        {
            var booleanQuery = new TermQueryConfigurator();
            query.Invoke(booleanQuery);
            booleanQuery.Query.Occurance = configurator.Occurance;
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }
        public static IQueryConfigurator CreateTermsQuery(this IQueryConfigurator configurator,
            Action<TermsQueryConfigurator> query)
        {
            var booleanQuery = new TermsQueryConfigurator();
            query.Invoke(booleanQuery);
            booleanQuery.Query.Occurance = configurator.Occurance;
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }

        public static IQueryConfigurator CreateFuzzyQuery(this IQueryConfigurator configurator, Action<FuzzyQueryConfigurator> query)
        {
            var booleanQuery = new FuzzyQueryConfigurator();
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }
        public static IQueryConfigurator CreatePrefixQuery(this IQueryConfigurator configurator, Action<PrefixQueryConfigurator> query)
        {
            var booleanQuery = new PrefixQueryConfigurator();
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }
        public static IQueryConfigurator CreatePrefixPhraseQuery(this IQueryConfigurator configurator, Action<PrefixPhraseQueryConfigurator> query)
        {
            var booleanQuery = new PrefixPhraseQueryConfigurator();
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }
        public static IQueryConfigurator CreateNumericRangeQuery(this IQueryConfigurator configurator,
            Action<NumericRangeQueryConfigurator> query)
        {
            var booleanQuery = new NumericRangeQueryConfigurator();
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }
        public static IQueryConfigurator CreateLongRangeQuery(this IQueryConfigurator configurator,
            Action<LongRangeQueryConfigurator> query)
        {
            var booleanQuery = new LongRangeQueryConfigurator();
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }
        public static IQueryConfigurator CreateStringRangeQuery(this IQueryConfigurator configurator,
            Action<StringRangeQueryConfigurator> query)
        {
            var booleanQuery = new StringRangeQueryConfigurator();
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }
        public static IQueryConfigurator CreateDateRangeQuery(this IQueryConfigurator configurator,
            Action<DateRangeQueryConfigurator> query)
        {
            var booleanQuery = new DateRangeQueryConfigurator();
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }
        public static IQueryConfigurator CreateBoolQuery(this IQueryConfigurator configurator,
            Action<BooleanQueryConfigurator> query)
        {
            var booleanQuery = new BooleanQueryConfigurator();
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }
        public static IQueryConfigurator CreateGoeDistanceQuery(this IQueryConfigurator configurator,
            Action<SpatialQueryConfigurator> query)
        {
            var booleanQuery = new SpatialQueryConfigurator();
            query.Invoke(booleanQuery);
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }
        public static IBaseQueryConfigurator CreateTermQuery(this IBaseQueryConfigurator configurator,
            Action<TermQueryConfigurator> query)
        {
            var booleanQuery = new TermQueryConfigurator();
            query.Invoke(booleanQuery);
            booleanQuery.Occurance = configurator.Occurance;
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }
        public static IBaseQueryConfigurator CreateTermsQuery(this IBaseQueryConfigurator configurator,
            Action<TermsQueryConfigurator> query)
        {
            var booleanQuery = new TermsQueryConfigurator();
            query.Invoke(booleanQuery);
            booleanQuery.Occurance = configurator.Occurance;
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
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
            configurator.Query.Add(configurator.Occurance, booleanQuery.Query);
            return configurator;
        }
    }
}
