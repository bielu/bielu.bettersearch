using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public class MeilisearchClientSettingsManagerTests
{
    private static MeilisearchIndexSettings GetSettings(MeilisearchOptions options, string index = "products") =>
        new MeilisearchClientSettingsManager(new TestOptionsMonitor<MeilisearchOptions>(options)).GetOrCreateClientSettings(index);

    [Fact]
    public void GetOrCreateClientSettings_NoIndexSettings_UsesSharedSettings()
    {
        var settings = GetSettings(new MeilisearchOptions
        {
            SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = "http://shared:7700", ApiKey = "shared-key" },
        });

        settings.ConnectionString.ShouldBe("http://shared:7700");
        settings.ApiKey.ShouldBe("shared-key");
    }

    [Fact]
    public void GetOrCreateClientSettings_IndexSettingsWithConnectionString_UsesIndexSettings()
    {
        var settings = GetSettings(new MeilisearchOptions
        {
            SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = "http://shared:7700", ApiKey = "shared-key" },
            Indexes = new Dictionary<string, MeilisearchIndexSettings>
            {
                ["products"] = new() { ConnectionString = "http://products:7700" },
            },
        });

        settings.ConnectionString.ShouldBe("http://products:7700");
        settings.ApiKey.ShouldBeNull();
    }

    [Fact]
    public void GetOrCreateClientSettings_IndexSettingsWithoutConnectionString_UsesSharedServerAndKey()
    {
        var settings = GetSettings(new MeilisearchOptions
        {
            SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = "http://shared:7700", ApiKey = "shared-key" },
            Indexes = new Dictionary<string, MeilisearchIndexSettings> { ["products"] = new() },
        });

        settings.ConnectionString.ShouldBe("http://shared:7700");
        settings.ApiKey.ShouldBe("shared-key");
    }

    [Fact]
    public void GetOrCreateClientSettings_IndexSettingsWithoutConnectionString_DoesNotChangeConfiguredOptions()
    {
        var indexSettings = new MeilisearchIndexSettings();

        GetSettings(new MeilisearchOptions
        {
            SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = "http://shared:7700", ApiKey = "shared-key" },
            Indexes = new Dictionary<string, MeilisearchIndexSettings> { ["products"] = indexSettings },
        });

        indexSettings.ConnectionString.ShouldBeEmpty();
        indexSettings.ApiKey.ShouldBeNull();
    }

    [Fact]
    public void GetOrCreateClientSettings_OtherIndexConfigured_UsesSharedSettings()
    {
        var settings = GetSettings(new MeilisearchOptions
        {
            SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = "http://shared:7700" },
            Indexes = new Dictionary<string, MeilisearchIndexSettings>
            {
                ["orders"] = new() { ConnectionString = "http://orders:7700" },
            },
        });

        settings.ConnectionString.ShouldBe("http://shared:7700");
    }

    [Fact]
    public void GetOrCreateClientSettings_MissingConnectionString_Throws() =>
        Should.Throw<ArgumentException>(() => GetSettings(new MeilisearchOptions()));

    [Fact]
    public async Task GetOrCreateClientAsync_SameServerAndKey_ReusesClient()
    {
        var factory = new MeilisearchClientFactory(new MeilisearchClientSettingsManager(new TestOptionsMonitor<MeilisearchOptions>(new MeilisearchOptions
        {
            SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = "http://shared:7700" },
            Indexes = new Dictionary<string, MeilisearchIndexSettings>
            {
                ["orders"] = new() { ConnectionString = "http://orders:7700" },
            },
        })));

        var first = await factory.GetOrCreateClientAsync("products");
        var second = await factory.GetOrCreateClientAsync("people");
        var orders = await factory.GetOrCreateClientAsync("orders");

        second.ShouldBeSameAs(first);
        orders.ShouldNotBeSameAs(first);
    }

    [Fact]
    public void GetOrCreateClientSettings_BoundIndexKeyCasingDiffers_UsesIndexSettings()
    {
        var options = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SharedIndexSettings:ConnectionString"] = "http://shared:7700",
                ["Indexes:People:ConnectionString"] = "http://people:7700",
            })
            .Build()
            .Get<MeilisearchOptions>()!;

        GetSettings(options, "people").ConnectionString.ShouldBe("http://people:7700");
    }

    [Fact]
    public void GetOrCreateClientSettings_AssignedIndexKeyCasingDiffers_UsesIndexSettings()
    {
        var options = new MeilisearchOptions
        {
            SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = "http://shared:7700", ApiKey = "shared-key" },
            Indexes = new Dictionary<string, MeilisearchIndexSettings>
            {
                ["People"] = new() { ConnectionString = "http://people:7700", ApiKey = "people-key" },
            },
        };

        var settings = GetSettings(options, "people");

        settings.ConnectionString.ShouldBe("http://people:7700");
        settings.ApiKey.ShouldBe("people-key");
    }
}
