namespace Bielu.BetterSearch.Abstractions.Configuration;

/// <summary>
/// How a client spreads its requests over the nodes of a cluster: the connection string and the additional nodes.
/// Whichever is chosen, a request that cannot reach a node is sent to the next one, and the node is left out for a
/// while before it is tried again.
/// </summary>
public enum NodePoolType
{
    /// <summary>The nodes take turns, starting from a random one, so the load is spread over all of them.</summary>
    Static,

    /// <summary>Every request goes to the first node that is up, in the order the nodes are listed.</summary>
    Sticky,

    /// <summary>
    /// As <see cref="Static"/>, but the client asks the cluster for its nodes when it starts and after a node fails,
    /// and from then on uses the addresses the nodes publish, so the client must be able to reach those addresses.
    /// </summary>
    Sniffing
}
