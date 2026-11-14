using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class LuceneQueryConfigurator : SubQueryConfigurator<LuceneQueryConfigurator>
    {
        /// <summary>Sets the raw Lucene query string, passed to the engine as written.</summary>
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
