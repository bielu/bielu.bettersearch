using Bielu.BetterSearch.Abstractions.Services;
using Lifti;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticsearchClientFactory : IClientFactory<IFullTextIndex<string>>
{
    public IFullTextIndex<string> GetOrCreateClient() => throw new NotImplementedException();
}
