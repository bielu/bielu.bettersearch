using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public sealed class MeilisearchInvalidSettingsSearchTests : InvalidSettingsSearchTestBase
{
    protected override string OptionsKey => MeilisearchOptions.MeilisearchOptionsKey;

    protected override void AddProvider(Configurator search) => search.AddMeilisearch();
}
