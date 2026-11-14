using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    public class QueryCreatorConfigurator(ISearchQuery query)
    {
        public ISearchQuery Query { get; set; } = query;

        public QueryCreatorConfigurator WithPageSize(int pageSize)
        {
            Query.PageSize = pageSize;
            return this;
        }
        public QueryCreatorConfigurator WithPage(int page)
        {
            Query.Page = page;
            return this;
        }
        public QueryCreatorConfigurator WithIndex(string indexName)
        {
            Query.Index = indexName;
            return this;
        }

        /// <summary>
        /// Sorts the hits by <paramref name="fieldName"/>, after any sort added before it.
        /// </summary>
        public QueryCreatorConfigurator SortBy(string fieldName, bool descending = false)
        {
            Query.SortOrder ??= [];
            var order = Query.SortOrder.Count == 0 ? 0 : Query.SortOrder.Max(sort => sort.Order) + 1;
            Query.SortOrder.Add(new FluentSortOrderField { FieldName = fieldName, Desc = descending, Order = order });
            return this;
        }

        /// <inheritdoc cref="SortBy"/>
        public QueryCreatorConfigurator SortByDescending(string fieldName) => SortBy(fieldName, descending: true);

        /// <summary>Returns only <paramref name="properties"/> on each hit, replacing any set before.</summary>
        public QueryCreatorConfigurator WithProperties(params string[] properties)
        {
            Query.Properties = properties.ToList();
            return this;
        }
    }
}
