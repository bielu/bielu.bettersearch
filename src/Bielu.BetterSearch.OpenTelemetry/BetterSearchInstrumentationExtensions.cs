using Bielu.BetterSearch.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Bielu.BetterSearch.OpenTelemetry;

public static class BetterSearchInstrumentationExtensions
{
    /// <summary>Collects the spans of the BetterSearch services (<see cref="BetterSearchInstrumentation.ActivitySourceName"/>).</summary>
    public static TracerProviderBuilder AddBetterSearchInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(BetterSearchInstrumentation.ActivitySourceName);
    }

    /// <summary>Collects the metrics of the BetterSearch services (<see cref="BetterSearchInstrumentation.MeterName"/>).</summary>
    public static MeterProviderBuilder AddBetterSearchInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddMeter(BetterSearchInstrumentation.MeterName);
    }
}
