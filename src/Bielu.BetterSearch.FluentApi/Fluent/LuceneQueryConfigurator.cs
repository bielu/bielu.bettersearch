using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class LuceneQueryConfigurator : SubQueryConfigurator<LuceneQueryConfigurator>
    {
        /// <summary>
        /// Sets the raw Lucene query string, stored in the query model unchanged. What runs depends on the provider:
        /// see the LuceneQuery row of the feature matrix.
        /// </summary>
        public LuceneQueryConfigurator WithQuery(string luceneQuery)
        {
            _query.LuceneQuery = luceneQuery;
            return this;
        }

        private LuceneSubQuery _query = new();
        public ISearchSubQuery Query => _query;
        protected override ISearchSubQuery SubQuery => _query;
    }
}
