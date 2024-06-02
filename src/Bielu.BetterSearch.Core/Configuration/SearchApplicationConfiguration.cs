using Bielu.BetterSearch.Abstractions.Services.NullImplementations;
using Bielu.BetterSearch.Services;

namespace Bielu.BetterSearch.Configuration;

public class SearchApplicationConfiguration
{
    private static SearchApplicationConfiguration _currentInstance;
    public static SearchApplicationConfiguration CurrentInstance
    {
        get
        {
            if (_currentInstance == null)
            {
                _currentInstance = new SearchApplicationConfiguration();
            }
            return _currentInstance;
        }
    }

    public Type DocumentValidatorType { get; set; } = typeof(DummyDocumentValidatorAsync);
    public Type IndexingServiceType { get; set; } = typeof(IndexingServiceAsync);

    public Type  SearchServiceType{ get; set; } = typeof(SearchServiceAsync);
    public Type IndexingProviderType { get; set; } = typeof(DummyIndexingProviderAsync);
    public Type SearchProviderType { get; set; } = typeof(DummySearchProviderAsync);
}
