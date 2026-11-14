using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Bielu.BetterSearch.Tests;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticSearchInvalidSettingsSearchTests : InvalidSettingsSearchTestBase
{
    protected override string OptionsKey => ElasticSearchOptions.ElasticSearchOptionsKey;

    protected override void AddProvider(Configurator search) => search.AddElasticSearch();

    [Theory]
    [InlineData("PASSWORD", null, null, "PASSWORD authentication needs AuthenticationDetails.", "AuthenticationDetails")]
    [InlineData("PASSWORD", "Username", "elastic", "PASSWORD authentication needs AuthenticationDetails:Password.", "AuthenticationDetails:Password")]
    [InlineData("CLOUD", null, null, "CLOUD authentication needs AuthenticationDetails.", "AuthenticationDetails")]
    [InlineData("CLOUD", "Id", "name:abc", "CLOUD authentication needs AuthenticationDetails:Username and AuthenticationDetails:Password.", "AuthenticationDetails:Username")]
    [InlineData("CLOUDAPI", null, null, "CLOUDAPI authentication needs AuthenticationDetails.", "AuthenticationDetails")]
    [InlineData("CLOUDAPI", "Id", "name:abc", "CLOUDAPI authentication needs AuthenticationDetails:ApiKey.", "AuthenticationDetails:ApiKey")]
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
