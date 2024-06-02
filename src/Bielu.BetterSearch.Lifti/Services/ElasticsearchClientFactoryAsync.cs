using Bielu.BetterSearch.Abstractions.Services;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiClientFactoryAsync(ILiftiIndexManager indexManager)
    : IClientFactoryAsync<IFullTextIndex<string>>
{
    public async Task<IFullTextIndex<string>> GetOrCreateClientAsync(string indexName)
    {
        var index = await indexManager.GetOrCreateIndexAsync(indexName);
        if(index.IsSuccess)
        {
            return index.Value;
        }
        throw new InvalidOperationException(index.Errors[0].Message);
    }
}
