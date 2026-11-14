using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Bielu.BetterSearch.Diagnostics;
using Shouldly;

namespace Bielu.BetterSearch.Core.Tests;

/// <summary>
/// Records the spans and measurements of the BetterSearch source and meter for one index, so tests running in
/// parallel don't see each other's telemetry.
/// </summary>
internal sealed class TelemetryCapture : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private readonly ConcurrentQueue<Activity> _activities = new();
    private readonly ConcurrentQueue<(string Instrument, double Value, Dictionary<string, object?> Tags)> _measurements = new();

    public TelemetryCapture(string index)
    {
        Index = index;
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BetterSearchInstrumentation.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (Equals(activity.GetTagItem(BetterSearchTelemetry.IndexTag), Index))
                {
                    _activities.Enqueue(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == BetterSearchInstrumentation.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _meterListener.Start();
    }

    public string Index { get; }

    public IReadOnlyList<Activity> Activities => _activities.ToList();

    public Activity SingleActivity() => Activities.ShouldHaveSingleItem();

    public IReadOnlyList<(double Value, Dictionary<string, object?> Tags)> Measurements(string instrument) =>
        _measurements.Where(m => m.Instrument == instrument).Select(m => (m.Value, m.Tags)).ToList();

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var dictionary = new Dictionary<string, object?>();
        foreach (var tag in tags)
        {
            dictionary[tag.Key] = tag.Value;
        }

        if (Equals(dictionary.GetValueOrDefault(BetterSearchTelemetry.IndexTag), Index))
        {
            _measurements.Enqueue((instrument.Name, value, dictionary));
        }
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
    }
}
