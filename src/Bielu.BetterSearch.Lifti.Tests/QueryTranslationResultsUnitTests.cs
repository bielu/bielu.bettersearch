using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

public class QueryTranslationResultsUnitTests(ISearchServiceAsync serviceAsync, IIndexingServiceAsync indexingServiceAsync) : QueryResultBaseTests(serviceAsync, indexingServiceAsync)
{

}
