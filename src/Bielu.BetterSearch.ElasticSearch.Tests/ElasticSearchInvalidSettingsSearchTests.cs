using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DependencyInjection;
using Bielu.BetterSearch.Tests;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticSearchInvalidSettingsSearchTests : InvalidSettingsSearchTestBase
{
    protected override string OptionsKey => ElasticSearchOptions.ElasticSearchOptionsKey;

    protected override void AddProvider(Configurator search) => search.AddElasticSearch();

    [Theory]
    [InlineData("Password", null, null, "Password authentication needs AuthenticationDetails.", "AuthenticationDetails")]
    [InlineData("Password", "Username", "elastic", "Password authentication needs AuthenticationDetails:Password.", "AuthenticationDetails:Password")]
    [InlineData("Cloud", null, null, "Cloud authentication needs AuthenticationDetails.", "AuthenticationDetails")]
    [InlineData("Cloud", "Id", "name:abc", "Cloud authentication needs AuthenticationDetails:Username and AuthenticationDetails:Password.", "AuthenticationDetails:Username")]
    [InlineData("CloudApi", null, null, "CloudApi authentication needs AuthenticationDetails.", "AuthenticationDetails")]
    [InlineData("CloudApi", "Id", "name:abc", "CloudApi authentication needs AuthenticationDetails:ApiKey.", "AuthenticationDetails:ApiKey")]
    [InlineData("ApiKey", null, null, "ApiKey authentication needs AuthenticationDetails.", "AuthenticationDetails")]
    [InlineData("ApiKey", "Id", "abc", "ApiKey authentication needs AuthenticationDetails:ApiKey.", "AuthenticationDetails:ApiKey")]
    [InlineData("Bearer", null, null, "Bearer authentication needs AuthenticationDetails.", "AuthenticationDetails")]
    [InlineData("Bearer", "ApiKey", "abc", "Bearer authentication needs AuthenticationDetails:Token.", "AuthenticationDetails:Token")]
    public async Task SearchAsync_AuthenticationWithoutCredentials_ReturnsFailedResultNamingTheSetting(
        string authenticationType, string? detail, string? value, string expected, string setting)
    {
        var settings = new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            ["SharedIndexSettings:AuthenticationType"] = authenticationType,
        };
        if (detail != null)
        {
            settings[$"SharedIndexSettings:AuthenticationDetails:{detail}"] = value;
        }

        var result = await SearchWithSettingsAsync(settings);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldStartWith(expected);
        result.Errors.Single().Message.ShouldContain($"{OptionsKey}:SharedIndexSettings:{setting}");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task SearchAsync_MaxResultWindowNotPositive_ReturnsFailedResultNamingTheSetting(string maxResultWindow)
    {
        var result = await SearchWithSettingsAsync(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "http://localhost:9200",
            ["MaxResultWindow"] = maxResultWindow,
        });

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe(
            $"{OptionsKey}:MaxResultWindow is {maxResultWindow}, but it must be greater than 0.");
    }
}
