using Bielu.BetterSearch.Typesense.Configuration;

namespace Bielu.BetterSearch.Typesense.Services;

internal interface ITypesenseClientSettingsManager
{
    TypesenseIndexSettings GetOrCreateClientSettings(string indexName);
}
