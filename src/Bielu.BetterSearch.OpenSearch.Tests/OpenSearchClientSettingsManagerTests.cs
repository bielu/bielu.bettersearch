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

    private static IConnectionConfigurationValues GetSettings(OpenSearchOptions options, string index = "products")
    {
        var manager = CreateManager(options);
        return manager.CreateClientSettings(manager.GetConnectionSettings(index));
    }

    [Fact]
    public void CreateClientSettings_NoIndexSettings_UsesSharedConnectionString()
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
    public void CreateClientSettings_IndexSettingsWithConnectionString_UsesIndexConnectionString()
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
    public void CreateClientSettings_IndexSettingsWithoutConnectionString_FallsBackToShared()
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
    public void CreateClientSettings_ApiKeyAuthentication_SetsApiKeyCredentials()
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
    public void CreateClientSettings_IndexSettingsWithoutAuthentication_UsesSharedAuthentication()
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
    public void CreateClientSettings_IndexSettings_DoesNotChangeTheOptions()
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
        products.AuthenticationType.ShouldBeNull();
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
        var factory = new OpenSearchClientFactory(new OpenSearchClientSettingsManager(monitor));

        var people = await factory.GetOrCreateClientAsync("people");
        var orders = await factory.GetOrCreateClientAsync("orders");
        var products = await factory.GetOrCreateClientAsync("products");

        orders.ShouldBeSameAs(people);
        (await factory.GetOrCreateClientAsync("products")).ShouldBeSameAs(products);
        products.ShouldNotBeSameAs(people);
        products.ConnectionSettings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://products:9200"));
    }

    [Fact]
    public void CreateClientSettings_IndexSettingsWithAuthenticationNone_TurnsSharedAuthenticationOff()
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
                ["products"] = new() { ConnectionString = "http://products:9200", AuthenticationType = OpenSearchAuthenticationType.NONE },
            },
        });

        settings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://products:9200"));
        settings.BasicAuthenticationCredentials.ShouldBeNull();
        settings.ApiKeyAuthenticationCredentials.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrCreateClientAsync_IndexEntriesWithoutConnectionSettings_ShareTheSharedClient()
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new() { IndexingEnabled = false },
                ["default"] = new() { ConnectionString = "http://other:9200" },
            },
        };
        var monitor = new StaticOptionsMonitor(options);
        var factory = new OpenSearchClientFactory(new OpenSearchClientSettingsManager(monitor));

        var people = await factory.GetOrCreateClientAsync("people");
        var products = await factory.GetOrCreateClientAsync("products");
        var defaultIndex = await factory.GetOrCreateClientAsync("default");

        products.ShouldBeSameAs(people);
        defaultIndex.ShouldNotBeSameAs(people);
        people.ConnectionSettings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
        defaultIndex.ConnectionSettings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://other:9200"));
    }

    [Fact]
    public async Task GetOrCreateClientAsync_ChangedSettings_CreatesANewClient()
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = OpenSearchAuthenticationType.PASSWORD,
                AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "old" },
            },
        };
        var monitor = new StaticOptionsMonitor(options);
        var factory = new OpenSearchClientFactory(new OpenSearchClientSettingsManager(monitor));
        var before = await factory.GetOrCreateClientAsync("people");

        options.SharedIndexSettings.AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "new" };
        var after = await factory.GetOrCreateClientAsync("people");

        after.ShouldNotBeSameAs(before);
        after.ConnectionSettings.BasicAuthenticationCredentials.Password.CreateString().ShouldBe("new");
    }

    [Theory]
    [InlineData(OpenSearchAuthenticationType.PASSWORD)]
    [InlineData(OpenSearchAuthenticationType.APIKEY)]
    public void ClientKey_ToString_LeavesOutCredentials(OpenSearchAuthenticationType authenticationType)
    {
        var key = OpenSearchClientFactory.ClientKey.From(new OpenSearchIndexSettings
        {
            ConnectionString = "http://shared:9200",
            AuthenticationType = authenticationType,
            AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "s3cret-password", ApiKey = "s3cret-key" },
        });

        var text = key.ToString();

        text.ShouldContain("http://shared:9200");
        text.ShouldContain(authenticationType.ToString());
        text.ShouldNotContain("s3cret");
    }

    [Fact]
    public async Task GetOrCreateClientAsync_InvalidSettingsFixed_CreatesTheClient()
    {
        var options = new OpenSearchOptions();
        var monitor = new StaticOptionsMonitor(options);
        var factory = new OpenSearchClientFactory(new OpenSearchClientSettingsManager(monitor));
        await Should.ThrowAsync<ArgumentException>(() => factory.GetOrCreateClientAsync("people"));

        options.SharedIndexSettings.ConnectionString = "http://shared:9200";
        var client = await factory.GetOrCreateClientAsync("people");

        client.ConnectionSettings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
    }

    [Theory]
    [InlineData("localhost:9200")]
    [InlineData("not a url")]
    public void CreateClientSettings_ConnectionStringNotAnHttpUrl_Throws(string connectionString) =>
        Should.Throw<ArgumentException>(() => GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = connectionString },
        }));

    [Theory]
    [InlineData(OpenSearchAuthenticationType.PASSWORD)]
    [InlineData(OpenSearchAuthenticationType.APIKEY)]
    public void CreateClientSettings_AuthenticationWithoutDetails_Throws(OpenSearchAuthenticationType authenticationType) =>
        Should.Throw<ArgumentException>(() => GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = authenticationType,
            },
        }));

    [Theory]
    [InlineData(OpenSearchAuthenticationType.PASSWORD, "", "s3cret", "")]
    [InlineData(OpenSearchAuthenticationType.PASSWORD, "admin", " ", "")]
    [InlineData(OpenSearchAuthenticationType.APIKEY, "", "", "")]
    [InlineData(OpenSearchAuthenticationType.APIKEY, "", "", "  ")]
    public void CreateClientSettings_AuthenticationWithEmptyCredentials_Throws(
        OpenSearchAuthenticationType authenticationType, string username, string password, string apiKey) =>
        Should.Throw<ArgumentException>(() => GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = authenticationType,
                AuthenticationDetails = new OpenSearchAuthenticationDetails
                {
                    Username = username,
                    Password = password,
                    ApiKey = apiKey,
                },
            },
        }));

    [Fact]
    public void CreateClientSettings_MissingConnectionString_Throws() =>
        Should.Throw<ArgumentException>(() => GetSettings(new OpenSearchOptions()));

    private sealed class StaticOptionsMonitor(OpenSearchOptions options) : IOptionsMonitor<OpenSearchOptions>
    {
        public OpenSearchOptions CurrentValue => options;

        public OpenSearchOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<OpenSearchOptions, string?> listener) => null;
    }
}
