using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Lifti.Querying;

namespace SImpl.SearchModule.ElasticSearch.Application.Services
{
    public class BaseLiftiQueryTranslatorService : IQueryTranslateServiceAsync<IQuery>
    {
        public async Task<Result<IQuery>> TranslateMainQuery(ISearchQuery<ISearchResult<ISearchModel>> query) =>
            throw new NotImplementedException();
    }
}
