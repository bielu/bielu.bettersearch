using Bielu.BetterSearch.Abstractions.Services.NullImplementations;
using Bielu.BetterSearch.Services;

namespace Bielu.BetterSearch.Configuration;

public class SearchApplicationConfiguration
{
    public Type DocumentValidatorType { get; set; } = typeof(DummyDocumentValidatorAsync);
    public Type IndexingServiceType { get; set; } = typeof(IndexingServiceAsync);

    public Type SearchServiceType { get; set; } = typeof(SearchServiceAsync);
    public Type IndexingProviderType { get; set; } = typeof(DummyIndexingProviderAsync);
    public Type SearchProviderType { get; set; } = typeof(DummySearchProviderAsync);
}
