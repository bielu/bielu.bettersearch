using Bielu.BetterSearch.Typesense.Configuration;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Typesense.Tests;

internal sealed class StaticOptionsMonitor(TypesenseOptions options) : IOptionsMonitor<TypesenseOptions>
{
    public TypesenseOptions CurrentValue => options;

    public TypesenseOptions Get(string? name) => options;

    public IDisposable? OnChange(Action<TypesenseOptions, string?> listener) => null;
}
