using System.Security.Cryptography;
using System.Text;

namespace CCRSnap.Services;

internal static class Tc3Helper
{
    public static (string authHeader, long timestamp) Sign(string secretId, string secretKey,
        string service, string action, string region, string payload)
    {
        var now = DateTimeOffset.UtcNow;
        long ts = now.ToUnixTimeSeconds();
        string date = now.ToString("yyyy-MM-dd");

        string payloadHash = Hex(HashSHA256(payload));
        string ch = $"content-type:application/json; charset=utf-8\nhost:{service}.tencentcloudapi.com\nx-tc-action:{action.ToLower()}\n";
        string signedHeaders = "content-type;host;x-tc-action";
        string canonicalRequest = $"POST\n/\n\n{ch}\n{signedHeaders}\n{payloadHash}";

        string credentialScope = $"{date}/{service}/tc3_request";
        string stringToSign = $"TC3-HMAC-SHA256\n{ts}\n{credentialScope}\n{Hex(HashSHA256(canonicalRequest))}";

        byte[] kd = HMACSHA256(Encoding.UTF8.GetBytes($"TC3{secretKey}"), Encoding.UTF8.GetBytes(date));
        byte[] ks = HMACSHA256(kd, Encoding.UTF8.GetBytes(service));
        byte[] kt = HMACSHA256(ks, Encoding.UTF8.GetBytes("tc3_request"));
        byte[] sigBytes = HMACSHA256(kt, Encoding.UTF8.GetBytes(stringToSign));

        string auth = $"TC3-HMAC-SHA256 Credential={secretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={Hex(sigBytes)}";
        return (auth, ts);
    }

    private static byte[] HashSHA256(string s) { using var sha = SHA256.Create(); return sha.ComputeHash(Encoding.UTF8.GetBytes(s)); }
    private static string Hex(byte[] b) => BitConverter.ToString(b).Replace("-", "").ToLower();
    private static byte[] HMACSHA256(byte[] key, byte[] data) { using var hmac = new System.Security.Cryptography.HMACSHA256(key); return hmac.ComputeHash(data); }
}
