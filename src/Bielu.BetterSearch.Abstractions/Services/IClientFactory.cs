namespace Bielu.BetterSearch.Abstractions.Services;

public interface IClientFactory<TClient>
{
    TClient GetOrCreateClient();
}
