using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// Checks of the certificate validation a <c>CertificateFingerprint</c> sets up, for the providers that have the setting.
/// </summary>
public abstract class CertificateFingerprintTestBase
{
    /// <summary>
    /// The server certificate validation callback of a client whose shared settings have
    /// <paramref name="fingerprint"/>.
    /// </summary>
    protected abstract Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> FingerprintCallback(string fingerprint);

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
        using var emptyChain = new X509Chain();

        // The CA's fingerprint accepts a certificate it signed, through the chain; the leaf's accepts the leaf itself.
        FingerprintCallback(Fingerprint(ca))(this, leafSignedByCa, chain, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeTrue();
        FingerprintCallback(Fingerprint(leaf))(this, leaf, emptyChain, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch).ShouldBeTrue();
        FingerprintCallback(Fingerprint(other))(this, leafSignedByCa, chain, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeFalse();
        FingerprintCallback(Fingerprint(other))(this, leaf, emptyChain, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeFalse();
        // A certificate the system trusts is still accepted.
        FingerprintCallback(Fingerprint(other))(this, leaf, emptyChain, SslPolicyErrors.None).ShouldBeTrue();
    }

    [Fact]
    public void CreateClientSettings_CaFingerprint_RejectsALeafTheCaDidNotSign()
    {
        using var ca = SelfSigned("CN=Test CA");
        // Another key under the pinned CA's name signs the leaf, so the chain names the pinned CA without its signature.
        using var forger = SelfSigned("CN=Test CA");
        using var forged = SignedBy(forger, "CN=node");
        using var chain = new X509Chain();
        chain.ChainPolicy.ExtraStore.Add(ca);
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        chain.Build(forged).ShouldBeFalse();
        chain.ChainElements.Select(element => element.Certificate.Thumbprint).ShouldContain(ca.Thumbprint);
        var callback = FingerprintCallback(ca.GetCertHashString(HashAlgorithmName.SHA256));

        callback(this, forged, chain, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeFalse();
    }

    [Fact]
    public void CreateClientSettings_IntermediateCaFingerprint_AcceptsALeafItSigned()
    {
        using var root = SelfSigned("CN=Root CA");
        using var intermediate = SignedBy(root, "CN=Intermediate CA", isCa: true);
        using var leaf = SignedBy(intermediate, "CN=node");
        using var chain = new X509Chain();
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.ChainPolicy.ExtraStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(leaf);
        var callback = FingerprintCallback(intermediate.GetCertHashString(HashAlgorithmName.SHA256));

        callback(this, leaf, chain, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeTrue();
    }

    protected static X509Certificate2 SelfSigned(string subject)
    {
        // The certificate keeps using the key, as an issuer signs with it.
        var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        return request.CreateSelfSigned(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2125, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    protected static X509Certificate2 SignedBy(X509Certificate2 issuer, string subject, bool isCa = false)
    {
        var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (isCa)
        {
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        }

        // A CA keeps its private key, as it signs with it.
        using var certificate = request.Create(issuer, new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2124, 1, 1, 0, 0, 0, TimeSpan.Zero), [1, 2, 3, 4]);
        return certificate.CopyWithPrivateKey(key);
    }
}
