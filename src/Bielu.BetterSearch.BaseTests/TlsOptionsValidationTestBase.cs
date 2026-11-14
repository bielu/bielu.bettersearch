using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// Checks of the <c>CertificateFingerprint</c> and <c>DevelopmentOnlySkipCertificateValidation</c> settings, for the
/// providers that have them.
/// </summary>
public abstract class TlsOptionsValidationTestBase : OptionsValidationTestBase
{
    private const string Fingerprint = "8F:3A:0C:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD";

    [Theory]
    [InlineData(Fingerprint)]
    [InlineData("8f3a0c112233445566778899aabbccddeeff00112233445566778899aabbccdd")]
    public void ValidateOnStart_Sha256CertificateFingerprint_Passes(string fingerprint)
    {
        ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "https://localhost:9200",
            ["SharedIndexSettings:CertificateFingerprint"] = fingerprint,
        }).ShouldBeNull();
    }

    [Theory]
    [InlineData("8F:3A:0C")]
    [InlineData("zz3a0c112233445566778899aabbccddeeff00112233445566778899aabbccdd")]
    [InlineData("8f3a0c112233445566778899aabbccddeeff00112233445566778899aabbccdd00")]
    public void ValidateOnStart_CertificateFingerprintNotSha256_FailsNamingTheIndexSetting(string fingerprint)
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "https://localhost:9200",
            ["Indexes:people:ConnectionString"] = "https://people:9200",
            ["Indexes:people:CertificateFingerprint"] = fingerprint,
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"CertificateFingerprint is not a SHA-256 fingerprint: it needs 64 hex digits, with or without colons. Set {OptionsKey}:Indexes:people:CertificateFingerprint."
        ]);
    }

    [Fact]
    public void ValidateOnStart_CertificateFingerprintAndSkippedValidation_FailsNamingBoth()
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "https://localhost:9200",
            ["SharedIndexSettings:CertificateFingerprint"] = Fingerprint,
            ["SharedIndexSettings:DevelopmentOnlySkipCertificateValidation"] = "true",
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"CertificateFingerprint and DevelopmentOnlySkipCertificateValidation can't both be set: a skipped validation never checks the fingerprint. Remove one. Set {OptionsKey}:SharedIndexSettings:DevelopmentOnlySkipCertificateValidation, or {OptionsKey}:Indexes:<index>:DevelopmentOnlySkipCertificateValidation for one index."
        ]);
    }

    [Fact]
    public void ValidateOnStart_IndexWithOwnConnectionStringAndSkippedValidation_DoesNotTakeTheSharedFingerprint()
    {
        // The TLS settings go with the connection string, so the shared fingerprint doesn't conflict with the index's switch.
        ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "https://localhost:9200",
            ["SharedIndexSettings:CertificateFingerprint"] = Fingerprint,
            ["Indexes:people:ConnectionString"] = "https://people:9200",
            ["Indexes:people:DevelopmentOnlySkipCertificateValidation"] = "true",
        }).ShouldBeNull();
    }

    [Theory]
    [InlineData("CertificateFingerprint", Fingerprint)]
    [InlineData("DevelopmentOnlySkipCertificateValidation", "false")]
    public void ValidateOnStart_IndexTlsSettingWithoutOwnConnectionString_FailsNamingTheSetting(string setting, string value)
    {
        var failure = ValidateOnStart(new Dictionary<string, string?>
        {
            ["SharedIndexSettings:ConnectionString"] = "https://localhost:9200",
            [$"Indexes:people:{setting}"] = value,
        });

        failure.ShouldNotBeNull().Failures.ShouldBe([
            $"{OptionsKey}:Indexes:people:{setting} only applies with the index's own ConnectionString; without one, the index uses the shared server and its TLS settings. Set {OptionsKey}:Indexes:people:ConnectionString, or set {setting} in {OptionsKey}:SharedIndexSettings."
        ]);
    }
}
