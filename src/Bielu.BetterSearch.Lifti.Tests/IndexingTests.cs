using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Lifti.Tests;

public class IndexingTests(IIndexingServiceAsync serviceAsync, IIndexingProviderAsync providerAsync) : IndexingTestBase(serviceAsync,providerAsync)
{

}
