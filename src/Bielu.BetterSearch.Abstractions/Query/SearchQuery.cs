﻿using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;
using Bielu.BetterSearch.Abstractions.Results;

namespace Bielu.BetterSearch.Abstractions.Query
{
    public class SearchQuery : ISearchQuery<IQueryResult>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<ISortOrderField> SortOrder { get; set; } = new();
        public List<IAggregationQuery> FacetQueries { get; set; } = new List<IAggregationQuery>();
        public List<IHighlightQuery> HighlightQueries { get; set; } = new List<IHighlightQuery>();

        public IDictionary<Occurance, ISearchSubQuery> PostFilterQuery { get; set; } =
            new Dictionary<Occurance, ISearchSubQuery>();

        public IDictionary<Occurance, ISearchSubQuery> Query { get; set; }
        public string Index { get; set; }
        public DateTime? PreviewAt { get; set; }

        void ISearchQuery<IQueryResult>.Add(Occurance queryOccurance, ISearchSubQuery booleanQueryQuery)
        {
            Query.Add(queryOccurance, booleanQueryQuery);
        }

        public IEnumerable<string> Properties { get; set; }
        void ICreatableSearchQuery<Occurance, ISearchSubQuery>.Add(Occurance key, ISearchSubQuery value) =>  Query.Add(key, value);
    }
}
