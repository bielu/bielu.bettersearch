using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.Configuration;
using OpenSearch.Net;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class OpenSearchClientSettingsManagerTests
{
    private static OpenSearchClientSettingsManager CreateManager(OpenSearchOptions options) =>
        new(new TestOptionsMonitor<OpenSearchOptions>(options));

    private static IConnectionConfigurationValues GetSettings(OpenSearchOptions options, string index = "products")
    {
        var manager = CreateManager(options);
        return manager.CreateClientSettings(manager.GetConnectionSettings(index));
    }

    [Fact]
    public void GetConnectionSettings_BulkBatchSize_IndexValueWinsAndAnUnsetIndexTakesTheSharedOne()
    {
        var manager = CreateManager(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { BulkBatchSize = 500 },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["people"] = new() { BulkBatchSize = 50 },
                ["places"] = new() { IndexName = "places" },
            },
        });

        manager.GetConnectionSettings("people").BulkBatchSize.ShouldBe(50);
        manager.GetConnectionSettings("places").BulkBatchSize.ShouldBe(500);
        manager.GetConnectionSettings("other").BulkBatchSize.ShouldBe(500);
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
        var monitor = new TestOptionsMonitor<OpenSearchOptions>(options);
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
        var monitor = new TestOptionsMonitor<OpenSearchOptions>(options);
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
        var monitor = new TestOptionsMonitor<OpenSearchOptions>(options);
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
        var monitor = new TestOptionsMonitor<OpenSearchOptions>(options);
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
    public void GetConnectionSettings_BoundIndexKeyCasingDiffers_UsesIndexSettings()
    {
        var options = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SharedIndexSettings:ConnectionString"] = "http://shared:9200",
                ["Indexes:People:ConnectionString"] = "http://people:9200",
            })
            .Build()
            .Get<OpenSearchOptions>()!;

        CreateManager(options).GetConnectionSettings("people").ConnectionString.ShouldBe("http://people:9200");
    }

    [Fact]
    public void GetConnectionSettings_AssignedIndexKeyCasingDiffers_UsesIndexSettings()
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["People"] = new() { ConnectionString = "http://people:9200" },
            },
        };

        CreateManager(options).GetConnectionSettings("people").ConnectionString.ShouldBe("http://people:9200");
    }

    [Fact]
    public void CreateClientSettings_MissingConnectionString_Throws() =>
        Should.Throw<ArgumentException>(() => GetSettings(new OpenSearchOptions()));

    [Fact]
    public void GetConnectionSettings_NoSharedSettingsAndIndexHasItsOwn_UsesIndexSettingsWithoutMappings()
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = null!,
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new() { ConnectionString = "http://products:9200", AuthenticationType = OpenSearchAuthenticationType.NONE },
            },
        };

        var settings = CreateManager(options).GetConnectionSettings("products");

        settings.ConnectionString.ShouldBe("http://products:9200");
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
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new() { ConnectionString = "http://shared:9200", WriteVisibility = shared },
            Indexes = new Dictionary<string, OpenSearchIndexSettings> { ["people"] = new() { WriteVisibility = own } },
        };

        var manager = CreateManager(options);

        manager.GetConnectionSettings("people").WriteVisibility.ShouldBe(expected);
        manager.GetConnectionSettings("other").WriteVisibility.ShouldBe(shared ?? WriteVisibility.Default);
    }

    [Theory]
    [InlineData(null, "people", "people")]
    [InlineData("", "people", "people")]
    [InlineData("app-", "people", "app-people")]
    public void GetIndexName_Prefix_IsPutBeforeTheName(string? prefix, string index, string expected) =>
        CreateManager(new OpenSearchOptions { IndexPrefix = prefix }).GetIndexName(index).ShouldBe(expected);

    [Fact]
    public void CreateClientSettings_SkippedCertificateValidation_AcceptsAnyCertificate()
    {
        var settings = GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "https://shared:9200",
                DevelopmentOnlySkipCertificateValidation = true,
            },
        });

        settings.ServerCertificateValidationCallback.ShouldBe(CertificateValidations.AllowAll);
    }

    [Fact]
    public void CreateClientSettings_NoTlsSettings_KeepsTheSystemsValidation()
    {
        GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "https://shared:9200" },
        }).ServerCertificateValidationCallback.ShouldBeNull();
    }

    [Fact]
    public void CreateClientSettings_CertificateFingerprint_AcceptsOnlyAChainWithThatCertificate()
    {
        using var ca = SelfSigned("CN=Test CA");
        using var other = SelfSigned("CN=Other CA");
        using var leaf = SelfSigned("CN=node");
        using var chain = new X509Chain();
        chain.ChainPolicy.ExtraStore.Add(ca);
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        using var leafSignedByCa = SignedBy(ca, "CN=node");
        chain.Build(leafSignedByCa);
        string Fingerprint(X509Certificate2 certificate) =>
            string.Join(":", certificate.GetCertHashString(HashAlgorithmName.SHA256).ToLowerInvariant().Chunk(2).Select(pair => new string(pair)));
        Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> Callback(string fingerprint) =>
            GetSettings(new OpenSearchOptions
            {
                SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "https://shared:9200", CertificateFingerprint = fingerprint },
            }).ServerCertificateValidationCallback.ShouldNotBeNull();
        using var emptyChain = new X509Chain();

        // The CA's fingerprint accepts a certificate it signed, through the chain; the leaf's accepts the leaf itself.
        Callback(Fingerprint(ca))(this, leafSignedByCa, chain, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeTrue();
        Callback(Fingerprint(leaf))(this, leaf, emptyChain, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch).ShouldBeTrue();
        Callback(Fingerprint(other))(this, leafSignedByCa, chain, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeFalse();
        Callback(Fingerprint(other))(this, leaf, emptyChain, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeFalse();
        // A certificate the system trusts is still accepted.
        Callback(Fingerprint(other))(this, leaf, emptyChain, SslPolicyErrors.None).ShouldBeTrue();
    }

    [Fact]
    public void GetConnectionSettings_TlsSettings_ComeWithTheConnectionString()
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "https://shared:9200",
                CertificateFingerprint = new string('A', 64),
            },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["own"] = new() { ConnectionString = "https://own:9200", DevelopmentOnlySkipCertificateValidation = true },
                ["inherits"] = new() { BulkBatchSize = 10, DevelopmentOnlySkipCertificateValidation = true },
            },
        };
        var manager = CreateManager(options);

        var own = manager.GetConnectionSettings("own");
        own.CertificateFingerprint.ShouldBeNull();
        own.DevelopmentOnlySkipCertificateValidation.ShouldBe(true);
        var inherits = manager.GetConnectionSettings("inherits");
        inherits.CertificateFingerprint.ShouldBe(new string('A', 64));
        inherits.DevelopmentOnlySkipCertificateValidation.ShouldBe(false);
    }

    private static X509Certificate2 SelfSigned(string subject)
    {
        // The certificate keeps using the key, as an issuer signs with it.
        var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        return request.CreateSelfSigned(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2125, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    private static X509Certificate2 SignedBy(X509Certificate2 issuer, string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.Create(issuer, new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2124, 1, 1, 0, 0, 0, TimeSpan.Zero), [1, 2, 3, 4]);
    }
}
