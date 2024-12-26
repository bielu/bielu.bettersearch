using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services;
using FluentResults;
using Lifti;
using Lifti.Querying;

namespace SImpl.SearchModule.ElasticSearch.Application.Services
{
    public class BaseLiftiQueryTranslatorService(
        ILiftiIndexManager liftiIndexManager,
        IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators) : IQueryTranslateServiceAsync<IQuery>
    {
        public async Task<Result<IQuery>> TranslateMainQuery(ISearchQuery<ISearchResult<ISearchModel>> query)
        {
            var indexResult = await liftiIndexManager.GetOrCreateIndexAsync(query.Index);
            if (indexResult.IsFailed)
            {
                return Result.Fail<IQuery>(indexResult.Errors.First().Message);
            }

            var index = indexResult.Value;
            var liftiQuery = index.Query();
            var subQueries = new Dictionary<Occurance, IQuery>();
            foreach (var subQuery in query.Query.GroupBy(x => x.Key))
            {
                foreach (var subqueryValue in subQuery.SelectMany(x => x.Value))
                {
                    var subQueryTranslator = subQueryTranslators.FirstOrDefault(t => t.CanTranslate(subqueryValue));
                    if (subQueryTranslator == null)
                    {
                        return Result.Fail<IQuery>($"No translator found for subquery {subQuery.GetType().Name}");
                    }

                    foreach (var queryValue in subQuery.SelectMany(x => x.Value))
                    {
                        var result = await subQueryTranslator.TranslateAsync(queryValue, subQueryTranslators);
                        if (result.IsFailed)
                        {
                            return Result.Fail<IQuery>(result.Errors.First().Message);
                        }

                        subQueries.Add(subQuery.Key, result.Value);
                    }
                }
            }

            var finalQuery = liftiQuery.InField("Index", f => f.ExactMatch(query.Index));
            foreach (var translatedQuery in subQueries)
            {
                finalQuery = translatedQuery.Key switch
                {
                    // Occurance.MUST => finalQuery.And.Query(translatedQuery.Value),
                    // Occurance.MUSTNOT => finalQuery.AndNot.Query(translatedQuery.Value),
                    // Occurance.SHOULD => finalQuery.Or.Query(translatedQuery.Value),
                    Occurance.FILTER => throw new NotSupportedException("Lifti not supports filter queries"),
                    _=> throw new NotSupportedException("Lifti provider not supports this type of query yet"),
                };
            }

            return Result.Ok(finalQuery.Build());
        }
    }
}
