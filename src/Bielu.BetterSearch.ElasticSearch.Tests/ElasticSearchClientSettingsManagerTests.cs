using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticSearchClientSettingsManagerTests
{
    private static IElasticsearchClientSettings GetSettings(ElasticSearchOptions options, string index)
    {
        var manager = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(options));
        return manager.CreateClientSettings(manager.GetConnectionSettings(index));
    }

    private static ElasticSearchIndexSettings GetConnectionSettings(ElasticSearchOptions options, string index) =>
        new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(options)).GetConnectionSettings(index);

    private static ElasticSearchIndexSettings PasswordSettings(string connectionString, string password) => new()
    {
        ConnectionString = connectionString,
        AuthenticationType = AuthenticationType.PASSWORD,
        AuthenticationDetails = new AuthenticationDetails { Username = "elastic", Password = password },
    };

    [Fact]
    public void GetConnectionSettings_BoundIndexKeyCasingDiffers_UsesIndexSettings()
    {
        var options = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SharedIndexSettings:ConnectionString"] = "http://shared:9200",
                ["Indexes:People:ConnectionString"] = "http://people:9200",
            })
            .Build()
            .Get<ElasticSearchOptions>()!;

        GetSettings(options, "people").NodePool.Nodes.Single().Uri.ShouldBe(new Uri("http://people:9200"));
    }

    [Fact]
    public void GetConnectionSettings_AssignedIndexKeyCasingDiffers_UsesIndexSettings()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["People"] = new() { ConnectionString = "http://people:9200" },
            },
        };

        GetSettings(options, "people").NodePool.Nodes.Single().Uri.ShouldBe(new Uri("http://people:9200"));
    }

    [Fact]
    public void GetConnectionSettings_NoIndexSettings_UsesSharedConnectionString()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
        };

        GetSettings(options, "people").NodePool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
    }

    [Fact]
    public void GetConnectionSettings_IndexWithoutConnectionString_LeavesOptionsUnchanged()
    {
        var people = new ElasticSearchIndexSettings { IndexName = "people" };
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings> { ["people"] = people },
        };

        GetSettings(options, "people").NodePool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));

        people.ConnectionString.ShouldBeNull();
    }

    [Fact]
    public void GetConnectionSettings_IndexNamedDefault_OtherIndexesUseSharedConnectionString()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["default"] = new() { ConnectionString = "http://other:9200" },
            },
        };

        GetConnectionSettings(options, "people").ConnectionString.ShouldBe("http://shared:9200");
        GetConnectionSettings(options, "default").ConnectionString.ShouldBe("http://other:9200");
    }

    [Fact]
    public void GetConnectionSettings_IndexWithoutConnectionStringOrAuthentication_InheritsSharedAuthentication()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = PasswordSettings("https://shared:9200", "secret"),
            Indexes = new Dictionary<string, ElasticSearchIndexSettings> { ["people"] = new() { IndexName = "people" } },
        };

        var settings = GetConnectionSettings(options, "people");

        settings.ConnectionString.ShouldBe("https://shared:9200");
        settings.AuthenticationType.ShouldBe(AuthenticationType.PASSWORD);
        settings.AuthenticationDetails.ShouldNotBeNull().Password.ShouldBe("secret");
    }

    [Fact]
    public void GetConnectionSettings_BulkBatchSize_IndexValueWinsAndAnUnsetIndexTakesTheSharedOne()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new() { BulkBatchSize = 500 },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["people"] = new() { BulkBatchSize = 50 },
                ["places"] = new() { IndexName = "places" },
            },
        };

        GetConnectionSettings(options, "people").BulkBatchSize.ShouldBe(50);
        GetConnectionSettings(options, "places").BulkBatchSize.ShouldBe(500);
        GetConnectionSettings(options, "other").BulkBatchSize.ShouldBe(500);
    }

    [Fact]
    public void GetConnectionSettings_IndexWithOwnConnectionStringOnly_InheritsSharedAuthentication()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = PasswordSettings("https://shared:9200", "secret"),
            Indexes = new Dictionary<string, ElasticSearchIndexSettings> { ["people"] = new() { ConnectionString = "https://people:9200" } },
        };

        var settings = GetConnectionSettings(options, "people");

        settings.ConnectionString.ShouldBe("https://people:9200");
        settings.AuthenticationType.ShouldBe(AuthenticationType.PASSWORD);
        settings.AuthenticationDetails.ShouldNotBeNull().Password.ShouldBe("secret");
    }

    [Fact]
    public void GetConnectionSettings_IndexWithAuthenticationNone_TurnsSharedAuthenticationOff()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = PasswordSettings("https://shared:9200", "secret"),
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["people"] = new() { AuthenticationType = AuthenticationType.NONE },
            },
        };

        var settings = GetConnectionSettings(options, "people");

        settings.ConnectionString.ShouldBe("https://shared:9200");
        settings.AuthenticationType.ShouldBe(AuthenticationType.NONE);
        settings.AuthenticationDetails.ShouldBeNull();
    }

    [Fact]
    public void GetConnectionSettings_IndexWithOwnAuthentication_UsesItsOwn()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = PasswordSettings("https://shared:9200", "shared-secret"),
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["people"] = PasswordSettings("https://people:9200", "people-secret"),
            },
        };

        var settings = GetConnectionSettings(options, "people");

        settings.ConnectionString.ShouldBe("https://people:9200");
        settings.AuthenticationDetails.ShouldNotBeNull().Password.ShouldBe("people-secret");
    }

    [Fact]
    public void GetConnectionSettings_NoSharedSettingsAndIndexHasItsOwn_UsesIndexSettingsWithoutMappings()
    {
        var options = new ElasticSearchOptions
        {
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["people"] = new() { ConnectionString = "http://people:9200", AuthenticationType = AuthenticationType.NONE },
            },
        };

        var settings = GetConnectionSettings(options, "people");

        settings.ConnectionString.ShouldBe("http://people:9200");
        settings.Mappings.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, null, WriteVisibility.Default)]
    [InlineData(WriteVisibility.WaitUntilSearchable, null, WriteVisibility.WaitUntilSearchable)]
    [InlineData(WriteVisibility.WaitUntilSearchable, WriteVisibility.Default, WriteVisibility.Default)]
    [InlineData(null, WriteVisibility.WaitUntilSearchable, WriteVisibility.WaitUntilSearchable)]
    public void GetConnectionSettings_WriteVisibility_IndexSettingWinsAndUnsetInheritsShared(
        WriteVisibility? shared, WriteVisibility? own, WriteVisibility expected)
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new() { ConnectionString = "http://shared:9200", WriteVisibility = shared },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings> { ["people"] = new() { WriteVisibility = own } },
        };

        var manager = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(options));

        manager.GetConnectionSettings("people").WriteVisibility.ShouldBe(expected);
        manager.GetConnectionSettings("other").WriteVisibility.ShouldBe(shared ?? WriteVisibility.Default);
    }

    [Theory]
    [InlineData(null, "people", "people")]
    [InlineData("", "people", "people")]
    [InlineData("app-", "people", "app-people")]
    public void GetIndexName_Prefix_IsPutBeforeTheName(string? prefix, string index, string expected)
    {
        var manager = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(new ElasticSearchOptions { IndexPrefix = prefix }));

        manager.GetIndexName(index).ShouldBe(expected);
    }

    [Fact]
    public void GetIndexName_PrefixChanges_UsesTheCurrentPrefix()
    {
        var monitor = new TestOptionsMonitor<ElasticSearchOptions>(new ElasticSearchOptions { IndexPrefix = "blue-" });
        IElasticSearchClientSettingsManager manager = new ElasticSearchClientSettingsManager(monitor);

        monitor.CurrentValue = new ElasticSearchOptions { IndexPrefix = "green-" };

        manager.GetIndexName("people").ShouldBe("green-people");
    }

    [Fact]
    public void CreateClientSettings_ApiKey_SendsTheKeyToTheConnectionString()
    {
        var settings = GetSettings(new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings
            {
                ConnectionString = "https://shared:9200",
                AuthenticationType = AuthenticationType.APIKEY,
                AuthenticationDetails = new AuthenticationDetails { ApiKey = "a2V5" },
            },
        }, "people");

        settings.NodePool.Nodes.Single().Uri.ShouldBe(new Uri("https://shared:9200"));
        settings.Authentication.ShouldNotBeNull().AuthScheme.ShouldBe("ApiKey");
        settings.Authentication.TryGetAuthorizationParameters(out var parameter).ShouldBeTrue();
        parameter.ShouldBe("a2V5");
    }

    [Fact]
    public void CreateClientSettings_Bearer_SendsTheToken()
    {
        var settings = GetSettings(new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings
            {
                ConnectionString = "https://shared:9200",
                AuthenticationType = AuthenticationType.BEARER,
                AuthenticationDetails = new AuthenticationDetails { Token = "AAEAAWVsYXN0aWM" },
            },
        }, "people");

        settings.Authentication.ShouldNotBeNull().AuthScheme.ShouldBe("Bearer");
        settings.Authentication.TryGetAuthorizationParameters(out var parameter).ShouldBeTrue();
        parameter.ShouldBe("AAEAAWVsYXN0aWM");
    }

    [Fact]
    public void CreateClientSettings_ColonSeparatedFingerprint_SetsItWithoutColons()
    {
        var settings = GetSettings(new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings
            {
                ConnectionString = "https://shared:9200",
                CertificateFingerprint = string.Join(":", Enumerable.Repeat("ab", 32)),
            },
        }, "people");

        settings.CertificateFingerprint.ShouldBe(string.Concat(Enumerable.Repeat("AB", 32)));
        settings.ServerCertificateValidationCallback.ShouldBeNull();
    }

    [Fact]
    public void CreateClientSettings_SkippedCertificateValidation_AcceptsAnyCertificate()
    {
        var settings = GetSettings(new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings
            {
                ConnectionString = "https://shared:9200",
                DevelopmentOnlySkipCertificateValidation = true,
            },
        }, "people");

        settings.ServerCertificateValidationCallback.ShouldBe(CertificateValidations.AllowAll);
    }

    [Fact]
    public void CreateClientSettings_NoTlsSettings_KeepsTheSystemsValidation()
    {
        var settings = GetSettings(new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "https://shared:9200" },
        }, "people");

        settings.CertificateFingerprint.ShouldBeNull();
        settings.ServerCertificateValidationCallback.ShouldBeNull();
    }

    [Fact]
    public void GetConnectionSettings_TlsSettings_ComeWithTheConnectionString()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings
            {
                ConnectionString = "https://shared:9200",
                CertificateFingerprint = new string('A', 64),
            },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["own"] = new() { ConnectionString = "https://own:9200", DevelopmentOnlySkipCertificateValidation = true },
                ["inherits"] = new() { BulkBatchSize = 10, DevelopmentOnlySkipCertificateValidation = true },
            },
        };

        var own = GetConnectionSettings(options, "own");
        own.CertificateFingerprint.ShouldBeNull();
        own.DevelopmentOnlySkipCertificateValidation.ShouldBe(true);
        var inherits = GetConnectionSettings(options, "inherits");
        inherits.CertificateFingerprint.ShouldBe(new string('A', 64));
        inherits.DevelopmentOnlySkipCertificateValidation.ShouldBe(false);
    }

    [Theory]
    [InlineData(AuthenticationType.CLOUD)]
    [InlineData(AuthenticationType.CLOUDAPI)]
    public void GetConnectionSettings_IndexWithItsOwnCloudId_KeepsItsOwnTlsSettings(AuthenticationType authenticationType)
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings
            {
                ConnectionString = "https://shared:9200",
                DevelopmentOnlySkipCertificateValidation = true,
            },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["cloud"] = new()
                {
                    AuthenticationType = authenticationType,
                    AuthenticationDetails = new AuthenticationDetails { Id = "name:ZXhhbXBsZS5lcy5pbyRhYmMkZGVm", Username = "elastic", Password = "secret", ApiKey = "key" },
                },
            },
        };

        var cloud = GetConnectionSettings(options, "cloud");

        cloud.DevelopmentOnlySkipCertificateValidation.ShouldBe(false);
        GetSettings(options, "cloud").ServerCertificateValidationCallback.ShouldBeNull();
        GetConnectionSettings(options, "people").DevelopmentOnlySkipCertificateValidation.ShouldBe(true);
    }

    [Fact]
    public void ClientKey_ToString_LeavesOutTheTokenAndApiKey()
    {
        var key = ElasticsearchClientFactory.ClientKey.From(new ElasticSearchIndexSettings
        {
            ConnectionString = "https://shared:9200",
            AuthenticationType = AuthenticationType.BEARER,
            AuthenticationDetails = new AuthenticationDetails { Token = "s3cret-token", ApiKey = "s3cret-key" },
        });

        var text = key.ToString();

        text.ShouldContain("BEARER");
        text.ShouldNotContain("s3cret");
    }

    [Fact]
    public async Task GetOrCreateClientAsync_TokenChanges_UsesNewToken()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings
            {
                ConnectionString = "https://shared:9200",
                AuthenticationType = AuthenticationType.BEARER,
                AuthenticationDetails = new AuthenticationDetails { Token = "old" },
            },
        };
        var monitor = new TestOptionsMonitor<ElasticSearchOptions>(options);
        var factory = new ElasticsearchClientFactory(new ElasticSearchClientSettingsManager(monitor));
        var before = await factory.GetOrCreateClientAsync("people");

        options.SharedIndexSettings.AuthenticationDetails = new AuthenticationDetails { Token = "new" };
        var after = await factory.GetOrCreateClientAsync("people");

        after.ShouldNotBeSameAs(before);
        after.ElasticsearchClientSettings.Authentication!.TryGetAuthorizationParameters(out var parameter).ShouldBeTrue();
        parameter.ShouldBe("new");
    }
}
