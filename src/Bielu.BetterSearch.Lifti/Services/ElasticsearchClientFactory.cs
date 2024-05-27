using Bielu.BetterSearch.Abstractions.Services;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public class ElasticsearchClientFactoryAsync : IClientFactoryAsync<IFullTextIndex<string>>
{
    public Task<IFullTextIndex<string>> GetOrCreateClientAsync() => throw new NotImplementedException();
}
