using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// Trusts a server certificate by the SHA-256 fingerprint of the certificate or of one in its chain, such as the
/// self-signed CA an Elasticsearch or OpenSearch install generates. For clients with no fingerprint setting of their own.
/// </summary>
internal static class CertificateFingerprints
{
    /// <summary>
    /// A certificate validation callback that accepts a certificate the system trusts, and otherwise only one that has
    /// <paramref name="fingerprint"/> or chains to a certificate with it through valid signatures.
    /// </summary>
    public static Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> Validator(string fingerprint)
    {
        var expected = ConnectionSettingsChecks.NormalizeFingerprint(fingerprint)
                       ?? throw new ArgumentException("CertificateFingerprint is not a SHA-256 fingerprint: it needs 64 hex digits, with or without colons.");
        return (_, certificate, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None)
            {
                return true;
            }

            // The fingerprint replaces the system's checks, the host name included, as a self-signed certificate seldom
            // names the host the application connects through.
            if (certificate is null)
            {
                return false;
            }

            if (Matches(certificate, expected))
            {
                return true;
            }

            var issuer = chain?.ChainElements.Select(element => element.Certificate).FirstOrDefault(element => Matches(element, expected));
            return issuer is not null && SignedThrough(certificate, issuer, chain!);
        };
    }

    /// <summary>
    /// Whether <paramref name="certificate"/> chains to <paramref name="issuer"/> with every signature valid. A failed
    /// chain can still list a certificate that only has the issuer's name, so being in it is not enough.
    /// </summary>
    private static bool SignedThrough(X509Certificate certificate, X509Certificate2 issuer, X509Chain chain)
    {
        using var leaf = new X509Certificate2(certificate);
        using var pinned = new X509Chain();
        pinned.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        pinned.ChainPolicy.CustomTrustStore.Add(issuer);
        pinned.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        // An intermediate CA is not a root, so the chain ends at it unfinished; every other error, a bad signature
        // included, still fails the build.
        pinned.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        foreach (var element in chain.ChainElements)
        {
            pinned.ChainPolicy.ExtraStore.Add(element.Certificate);
        }

        var expected = issuer.GetCertHashString(HashAlgorithmName.SHA256);
        return pinned.Build(leaf) && pinned.ChainElements.Skip(1).Any(element => Matches(element.Certificate, expected));
    }

    private static bool Matches(X509Certificate certificate, string expected) =>
        string.Equals(certificate.GetCertHashString(HashAlgorithmName.SHA256), expected, StringComparison.OrdinalIgnoreCase);
}
