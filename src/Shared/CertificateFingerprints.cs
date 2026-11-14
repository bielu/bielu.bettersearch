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
    /// <paramref name="fingerprint"/> or has a certificate with it in its chain.
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
            return (certificate is not null && Matches(certificate, expected)) ||
                   (chain?.ChainElements.Any(element => Matches(element.Certificate, expected)) ?? false);
        };
    }

    private static bool Matches(X509Certificate certificate, string expected) =>
        string.Equals(certificate.GetCertHashString(HashAlgorithmName.SHA256), expected, StringComparison.OrdinalIgnoreCase);
}
