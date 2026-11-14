using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Tests;

/// <summary>An <see cref="IOptionsMonitor{TOptions}"/> that returns the options a test gives it.</summary>
public sealed class TestOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
{
    // Settable so a test can change the options, as a configuration reload would.
    public TOptions CurrentValue { get; set; } = value;

    /// <summary>Options returned by <see cref="Get"/> for a name; any other name gets <see cref="CurrentValue"/>.</summary>
    public Dictionary<string, TOptions> Named { get; } = new();

    public TOptions Get(string? name) =>
        name is not null && Named.TryGetValue(name, out var named) ? named : CurrentValue;

    // No test reloads options through a change notification, so there is nothing to register.
    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
