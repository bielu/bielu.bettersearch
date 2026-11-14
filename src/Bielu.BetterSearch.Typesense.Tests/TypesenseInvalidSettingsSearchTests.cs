using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Tests;
using Bielu.BetterSearch.Typesense.Configuration;
using Bielu.BetterSearch.Typesense.DepedencyInjection;

namespace Bielu.BetterSearch.Typesense.Tests;

public sealed class TypesenseInvalidSettingsSearchTests : InvalidSettingsSearchTestBase
{
    protected override string OptionsKey => TypesenseOptions.TypesenseOptionsKey;

    protected override void AddProvider(Configurator search) => search.AddTypesense();
}
