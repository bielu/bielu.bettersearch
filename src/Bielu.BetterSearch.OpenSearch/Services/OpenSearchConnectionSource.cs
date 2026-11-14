using OpenSearch.Net;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>The <c>createConnection</c> callback given to <c>UseOpenSearchConnection</c>.</summary>
internal sealed class OpenSearchConnectionSource(Func<IServiceProvider, IConnection> createConnection)
{
    /// <summary>The callback, bound to <paramref name="services"/>.</summary>
    public Func<IConnection> Bind(IServiceProvider services) => () => createConnection(services);
}
