using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CCRSnap.Services;

public interface ITranslationService
{
    Task<string> TranslateAsync(string text, string? apiKey, string? apiSecret,
        string from = "auto", string to = "zh-CHS");
}

public class TranslationService : ITranslationService
{
    private readonly HttpClient _http = new();

    public async Task<string> TranslateAsync(string text, string? apiKey, string? apiSecret,
        string from = "auto", string to = "zh-CHS")
    {
        if (string.IsNullOrWhiteSpace(text)) return "（无文字可翻译）";
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
            return "请先在设置中配置有道云 API Key 和 Secret";

        var salt = Guid.NewGuid().ToString("N");
        var curtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signStr = apiKey + text + salt + curtime + apiSecret;
        var sign = ComputeHash(signStr, apiSecret);

        var form = new Dictionary<string, string>
        {
            ["q"] = text,
            ["from"] = from,
            ["to"] = to,
            ["appKey"] = apiKey,
            ["salt"] = salt,
            ["sign"] = sign,
            ["signType"] = "v3",
            ["curtime"] = curtime
        };

        var resp = await _http.PostAsync("https://openapi.youdao.com/api",
            new FormUrlEncodedContent(form));
        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var errorCode = doc.RootElement.GetProperty("errorCode").GetInt32();
        if (errorCode != 0)
            return $"翻译失败 (错误码: {errorCode})";
        var trans = doc.RootElement.GetProperty("translation")[0].GetString();
        return trans ?? "（翻译结果为空）";
    }

    private static string ComputeHash(string input, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}
