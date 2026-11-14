using OpenSearch.Net;

namespace Bielu.BetterSearch.OpenSearch.Aws.Tests;

/// <summary>A clock that always reads <paramref name="now"/>, so a signature is the same on every run.</summary>
internal sealed class FixedClock(DateTime now) : DateTimeProvider
{
    public override DateTime Now() => now;
}
