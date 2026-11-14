using System.Diagnostics;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.OpenTelemetry;
using Bielu.BetterSearch.Services;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;

namespace Bielu.BetterSearch.Core.Tests;

public class InstrumentationExtensionsTests
{
    [Fact]
    public async Task AddBetterSearchInstrumentation_TracerProvider_ExportsServiceSpans()
    {
        var index = $"index-{Guid.NewGuid():N}";
        var exported = new List<Activity>();
        using (Sdk.CreateTracerProviderBuilder()
                   .AddBetterSearchInstrumentation()
                   .AddProcessor(new CollectingProcessor(exported, index))
                   .Build())
        {
            using var service = new SearchServiceAsync(new FakeSearchProvider());
            await service.SearchAsync(new SearchQuery { Index = index }, TestContext.Current.CancellationToken);
        }

        exported.ShouldHaveSingleItem().OperationName.ShouldBe($"search {index}");
    }

    [Fact]
    public void AddBetterSearchInstrumentation_MeterProvider_Builds()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder().AddBetterSearchInstrumentation().Build();

        meterProvider.ShouldNotBeNull();
    }

    private sealed class CollectingProcessor(List<Activity> exported, string index) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data)
        {
            if (Equals(data.GetTagItem("db.collection.name"), index))
            {
                lock (exported)
                {
                    exported.Add(data);
                }
            }
        }
    }
}
