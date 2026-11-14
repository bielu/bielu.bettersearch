using Bielu.BetterSearch.Tests;
using Bielu.BetterSearch.Typesense.Configuration;
using Bielu.BetterSearch.Typesense.Services;
using Shouldly;

namespace Bielu.BetterSearch.Typesense.Tests;

public sealed class TypesenseClientTests(TypesenseResource typesense)
{
    [Fact]
    public void GetOrCreateClientSettings_IndexWithoutConnectionString_InheritsNodesAndKeyWithoutChangingTheOptions()
    {
        var options = new TypesenseOptions
        {
            SharedIndexSettings = new TypesenseIndexSettings { ConnectionString = "http://shared:8108", AdditionalNodes = ["http://other:8108"], ApiKey = "shared-key" },
            Indexes = new Dictionary<string, TypesenseIndexSettings> { ["People"] = new() { ApiKey = "people-key" } }
        };

        var settings = new TypesenseClientSettingsManager(new TestOptionsMonitor<TypesenseOptions>(options)).GetOrCreateClientSettings("people");

        settings.ConnectionString.ShouldBe("http://shared:8108");
        settings.AdditionalNodes.ShouldBe(["http://other:8108"]);
        settings.ApiKey.ShouldBe("people-key");
        options.Indexes!["People"].ConnectionString.ShouldBeEmpty();
    }

    [Fact]
    public void GetOrCreateClientSettings_IndexWithItsOwnConnectionString_UsesOnlyItsOwnSettings()
    {
        var options = new TypesenseOptions
        {
            SharedIndexSettings = new TypesenseIndexSettings { ConnectionString = "http://shared:8108", ApiKey = "shared-key" },
            Indexes = new Dictionary<string, TypesenseIndexSettings> { ["people"] = new() { ConnectionString = "http://people:8108" } }
        };

        var settings = new TypesenseClientSettingsManager(new TestOptionsMonitor<TypesenseOptions>(options)).GetOrCreateClientSettings("PEOPLE");

        settings.ConnectionString.ShouldBe("http://people:8108");
        settings.ApiKey.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrCreateClientAsync_InvalidAdditionalNode_ThrowsNamingTheSetting()
    {
        var options = new TypesenseOptions
        {
            SharedIndexSettings = new TypesenseIndexSettings { ConnectionString = "http://shared:8108", AdditionalNodes = ["shared:8108"] }
        };
        using var factory = new TypesenseClientFactory(new TypesenseClientSettingsManager(new TestOptionsMonitor<TypesenseOptions>(options)), new TestOptionsMonitor<TypesenseOptions>(options));

        var error = await Should.ThrowAsync<ArgumentException>(() => factory.GetOrCreateClientAsync("people"));

        error.Message.ShouldContain("ConnectionString is not an http or https URL");
    }

    [Fact]
    public async Task SendAsync_FirstNodeUnreachable_UsesTheNextNode()
    {
        var options = new TypesenseOptions
        {
            SharedIndexSettings = new TypesenseIndexSettings
            {
                // Nothing listens on port 1, so the connection is refused.
                ConnectionString = "http://127.0.0.1:1",
                AdditionalNodes = [typesense.Endpoint!.ToString()],
                ApiKey = typesense.ApiKey
            }
        };
        using var factory = new TypesenseClientFactory(new TypesenseClientSettingsManager(new TestOptionsMonitor<TypesenseOptions>(options)), new TestOptionsMonitor<TypesenseOptions>(options));
        var client = await factory.GetOrCreateClientAsync("people");

        var response = await client.SendAsync(HttpMethod.Get, "collections", null, TestContext.Current.CancellationToken);

        response.IsSuccess.ShouldBeTrue(response.Describe());
    }

    [Fact]
    public async Task SendAsync_OnlyNodeUnreachable_Throws()
    {
        var options = new TypesenseOptions { SharedIndexSettings = new TypesenseIndexSettings { ConnectionString = "http://127.0.0.1:1" } };
        using var factory = new TypesenseClientFactory(new TypesenseClientSettingsManager(new TestOptionsMonitor<TypesenseOptions>(options)), new TestOptionsMonitor<TypesenseOptions>(options));
        var client = await factory.GetOrCreateClientAsync("people");

        await Should.ThrowAsync<HttpRequestException>(() => client.SendAsync(HttpMethod.Get, "collections", null, TestContext.Current.CancellationToken));
    }
}
