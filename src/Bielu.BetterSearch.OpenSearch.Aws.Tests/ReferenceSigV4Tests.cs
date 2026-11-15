using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Aws.Tests;

/// <summary>Pins <see cref="ReferenceSigV4"/> to the examples AWS publishes, so a signature it agrees with is a correct one.</summary>
public class ReferenceSigV4Tests
{
    private const string ExampleSecretKey = "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY";

    [Fact]
    public void SigningKey_AwsDerivedKeyExample_MatchesThePublishedKey() =>
        ReferenceSigV4.Hex(ReferenceSigV4.SigningKey(ExampleSecretKey, "20120215", "us-east-1", "iam"))
            .ShouldBe("f4780e2d9f65fa895f9c67b32ce1baf0b0d8a43505a000a1a9e090d414db404d");

    [Fact]
    public void Signature_AwsGetVanillaExample_MatchesThePublishedSignature() =>
        ReferenceSigV4.Signature(ExampleSecretKey, "20150830", "us-east-1", "service", "20150830T123600Z",
                "GET", "/", "",
                [new("Host", "example.amazonaws.com"), new("X-Amz-Date", "20150830T123600Z")],
                "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")
            .ShouldBe("5fa00fa31553b73ebf1942676e86291e8372ff2a2260956d9b8aae1d763fbf31");
}
