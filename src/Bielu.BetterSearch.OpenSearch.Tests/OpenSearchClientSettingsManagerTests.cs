using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
using Microsoft.Extensions.Options;
using OpenSearch.Net;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class OpenSearchClientSettingsManagerTests
{
    private static OpenSearchClientSettingsManager CreateManager(OpenSearchOptions options) =>
        new(new StaticOptionsMonitor(options));

    private static IConnectionConfigurationValues GetSettings(OpenSearchOptions options, string index = "products") =>
        CreateManager(options).GetOrCreateClientSettings(index);

    [Fact]
    public void GetOrCreateClientSettings_NoIndexSettings_UsesSharedConnectionString()
    {
        var settings = GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://shared:9200" },
        });

        settings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
        settings.BasicAuthenticationCredentials.ShouldBeNull();
        settings.ApiKeyAuthenticationCredentials.ShouldBeNull();
    }

    [Fact]
    public void GetOrCreateClientSettings_IndexSettingsWithConnectionString_UsesIndexConnectionString()
    {
        var settings = GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new() { ConnectionString = "http://products:9200" },
            },
        });

        settings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://products:9200"));
    }

    [Fact]
    public void GetOrCreateClientSettings_IndexSettingsWithoutConnectionString_FallsBackToShared()
    {
        var settings = GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new()
                {
                    AuthenticationType = OpenSearchAuthenticationType.PASSWORD,
                    AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "secret" },
                },
            },
        });

        settings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
        settings.BasicAuthenticationCredentials.Username.ShouldBe("admin");
    }

    [Fact]
    public void GetOrCreateClientSettings_ApiKeyAuthentication_SetsApiKeyCredentials()
    {
        var settings = GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = OpenSearchAuthenticationType.APIKEY,
                AuthenticationDetails = new OpenSearchAuthenticationDetails { ApiKey = "os_key" },
            },
        });

        settings.ApiKeyAuthenticationCredentials.ShouldNotBeNull();
        // Sent as "Authorization: ApiKey os_key", the form the OpenSearch security plugin accepts.
        settings.ApiKeyAuthenticationCredentials.Base64EncodedApiKey.CreateString().ShouldBe("os_key");
        settings.BasicAuthenticationCredentials.ShouldBeNull();
    }

    [Fact]
    public void GetOrCreateClientSettings_IndexSettingsWithoutAuthentication_UsesSharedAuthentication()
    {
        var settings = GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = OpenSearchAuthenticationType.PASSWORD,
                AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "secret" },
            },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new() { ConnectionString = "http://products:9200" },
            },
        });

        settings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://products:9200"));
        settings.BasicAuthenticationCredentials.ShouldNotBeNull();
        settings.BasicAuthenticationCredentials.Username.ShouldBe("admin");
    }

    [Fact]
    public void GetOrCreateClientSettings_IndexSettings_DoesNotChangeTheOptions()
    {
        var products = new OpenSearchIndexSettings();
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = OpenSearchAuthenticationType.PASSWORD,
                AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "secret" },
            },
            Indexes = new Dictionary<string, OpenSearchIndexSettings> { ["products"] = products },
        };

        GetSettings(options);

        products.ConnectionString.ShouldBeEmpty();
        products.AuthenticationType.ShouldBe(OpenSearchAuthenticationType.NONE);
        products.AuthenticationDetails.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrCreateClientAsync_SameSettings_ReusesOneClient()
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new() { ConnectionString = "http://products:9200" },
            },
        };
        var monitor = new StaticOptionsMonitor(options);
        var factory = new OpenSearchClientFactory(new OpenSearchClientSettingsManager(monitor), monitor);

        var people = await factory.GetOrCreateClientAsync("people");
        var orders = await factory.GetOrCreateClientAsync("orders");
        var products = await factory.GetOrCreateClientAsync("products");

        orders.ShouldBeSameAs(people);
        (await factory.GetOrCreateClientAsync("products")).ShouldBeSameAs(products);
        products.ShouldNotBeSameAs(people);
        products.ConnectionSettings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://products:9200"));
    }

    [Theory]
    [InlineData(OpenSearchAuthenticationType.PASSWORD)]
    [InlineData(OpenSearchAuthenticationType.APIKEY)]
    public void GetOrCreateClientSettings_AuthenticationWithoutDetails_Throws(OpenSearchAuthenticationType authenticationType) =>
        Should.Throw<ArgumentException>(() => GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = authenticationType,
            },
        }));

    [Fact]
    public void GetOrCreateClientSettings_MissingConnectionString_Throws() =>
        Should.Throw<ArgumentException>(() => GetSettings(new OpenSearchOptions()));

    private sealed class StaticOptionsMonitor(OpenSearchOptions options) : IOptionsMonitor<OpenSearchOptions>
    {
        public OpenSearchOptions CurrentValue => options;

        public OpenSearchOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<OpenSearchOptions, string?> listener) => null;
    }
}
