using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Bielu.BetterSearch.OpenSearch.Aws.Tests;

/// <summary>
/// An independent AWS SigV4 signer, written from the AWS documentation and pinned by AWS's published examples in
/// <see cref="ReferenceSigV4Tests"/>, that recomputes the signature of a request the connection sent.
/// </summary>
internal static class ReferenceSigV4
{
    public static string Signature(string secretKey, string date, string region, string service, string amzDate,
        string method, string path, string query, IReadOnlyList<KeyValuePair<string, string>> signedHeaders, string payloadHash)
    {
        var canonicalHeaders = string.Concat(signedHeaders.Select(header => $"{header.Key.ToLowerInvariant()}:{header.Value.Trim()}\n"));
        var signedHeaderNames = string.Join(';', signedHeaders.Select(header => header.Key.ToLowerInvariant()));
        var canonicalRequest = $"{method}\n{path}\n{query}\n{canonicalHeaders}\n{signedHeaderNames}\n{payloadHash}";
        var scope = $"{date}/{region}/{service}/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{scope}\n{Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";
        return Hex(HMACSHA256.HashData(SigningKey(secretKey, date, region, service), Encoding.UTF8.GetBytes(stringToSign)));
    }

    public static byte[] SigningKey(string secretKey, string date, string region, string service)
    {
        var key = Hmac(Encoding.UTF8.GetBytes("AWS4" + secretKey), date);
        key = Hmac(key, region);
        key = Hmac(key, service);
        return Hmac(key, "aws4_request");
    }

    public static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLower(CultureInfo.InvariantCulture);

    private static byte[] Hmac(byte[] key, string data) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));
}
