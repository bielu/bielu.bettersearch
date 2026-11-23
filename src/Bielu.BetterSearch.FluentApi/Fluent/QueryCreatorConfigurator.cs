using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.FluentApi.Fluent
{
    /// <summary>Configures the paging, index, sorting and returned properties of a search query.</summary>
    /// <param name="query">The search query to configure.</param>
    public class QueryCreatorConfigurator(ISearchQuery query)
    {
        /// <summary>The search query this configurator changes.</summary>
        public ISearchQuery Query { get; set; } = query;

        /// <summary>Sets the <see cref="ISearchQuery.PageSize"/>, the number of hits per page.</summary>
        /// <param name="pageSize">The number of hits per page.</param>
        /// <returns>This configurator, for chaining.</returns>
        public QueryCreatorConfigurator WithPageSize(int pageSize)
        {
            Query.PageSize = pageSize;
            return this;
        }
        /// <summary>Sets the <see cref="ISearchQuery.Page"/> to return.</summary>
        /// <param name="page">The page to return.</param>
        /// <returns>This configurator, for chaining.</returns>
        public QueryCreatorConfigurator WithPage(int page)
        {
            Query.Page = page;
            return this;
        }
        /// <summary>Sets the <see cref="ISearchQuery.Index"/> to search.</summary>
        /// <param name="indexName">The name of the index.</param>
        /// <returns>This configurator, for chaining.</returns>
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
            Query.SortOrder.Add(new SortOrderField { FieldName = fieldName, Desc = descending, Order = order });
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
