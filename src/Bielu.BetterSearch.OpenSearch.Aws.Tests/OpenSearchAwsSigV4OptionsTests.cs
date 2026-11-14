using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Aws.Tests;

public class OpenSearchAwsSigV4OptionsTests
{
    private static ServiceProvider Services(Dictionary<string, string?> settings) => new ServiceCollection()
        .AddBetterSearch(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), search => search
            .AddOpenSearch()
            .AddOpenSearchAwsSigV4())
        .BuildServiceProvider();

    [Fact]
    public void Options_NothingSet_SignForAmazonOpenSearchService()
    {
        using var services = Services([]);

        var options = services.GetRequiredService<IOptions<OpenSearchAwsSigV4Options>>().Value;

        options.Service.ShouldBe("es");
        options.Region.ShouldBeNull();
        options.Credentials.ShouldBeNull();
    }

    [Fact]
    public void Options_ServiceAndRegionInConfiguration_AreBound()
    {
        using var services = Services(new()
        {
            ["Bielu:BetterSearch:OpenSearch:AwsSigV4:Service"] = "aoss",
            ["Bielu:BetterSearch:OpenSearch:AwsSigV4:Region"] = "eu-central-1",
        });

        var options = services.GetRequiredService<IOptions<OpenSearchAwsSigV4Options>>().Value;

        options.Service.ShouldBe("aoss");
        options.Region.ShouldBe("eu-central-1");
    }

    [Theory]
    [InlineData("s3")]
    [InlineData("ES")]
    [InlineData("")]
    public void Options_UnknownService_FailValidationNamingTheSetting(string service)
    {
        using var services = Services(new() { ["Bielu:BetterSearch:OpenSearch:AwsSigV4:Service"] = service });

        var error = Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<OpenSearchAwsSigV4Options>>().Value);

        error.Message.ShouldContain($"Service is '{service}', but must be 'es' for Amazon OpenSearch Service or 'aoss' for Amazon OpenSearch Serverless. Set Bielu:BetterSearch:OpenSearch:AwsSigV4:Service.");
    }

    [Fact]
    public void Options_EmptyRegion_FailValidationNamingTheSetting()
    {
        using var services = new ServiceCollection()
            .AddBetterSearch(new ConfigurationBuilder().Build(), search => search
                .AddOpenSearch()
                .AddOpenSearchAwsSigV4(options => options.Region = " "))
            .BuildServiceProvider();

        var error = Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<OpenSearchAwsSigV4Options>>().Value);

        error.Message.ShouldContain("Region is empty. Set Bielu:BetterSearch:OpenSearch:AwsSigV4:Region");
    }

    [Fact]
    public void Create_InvalidService_ThrowsArgumentExceptionNamingTheSetting()
    {
        var error = Should.Throw<ArgumentException>(() => AwsSigV4Connections.Create(new OpenSearchAwsSigV4Options { Service = "s3", Region = "eu-west-1" }));

        error.Message.ShouldContain("Set Bielu:BetterSearch:OpenSearch:AwsSigV4:Service.");
    }
}
