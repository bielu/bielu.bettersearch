using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public sealed class OpenSearchInvalidSettingsSearchTests : InvalidSettingsSearchTestBase
{
    protected override string OptionsKey => OpenSearchOptions.OpenSearchOptionsKey;

    protected override void AddProvider(Configurator search) => search.AddOpenSearch();
}
