namespace Bielu.BetterSearch.Diagnostics;

/// <summary>
/// The names the Core services emit telemetry under. Subscribe to them with
/// <c>AddSource(BetterSearchInstrumentation.ActivitySourceName)</c> and <c>AddMeter(BetterSearchInstrumentation.MeterName)</c>,
/// or with <c>AddBetterSearchInstrumentation()</c> from the <c>Bielu.BetterSearch.OpenTelemetry</c> package.
/// </summary>
public static class BetterSearchInstrumentation
{
    /// <summary>The <see cref="System.Diagnostics.ActivitySource"/> name: one span per search and indexing service call.</summary>
    public const string ActivitySourceName = "Bielu.BetterSearch";

    /// <summary>The <see cref="System.Diagnostics.Metrics.Meter"/> name: operation duration and count, and documents indexed and removed.</summary>
    public const string MeterName = "Bielu.BetterSearch";
}
