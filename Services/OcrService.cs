using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CCRSnap.Services;

public interface IOcrService
{
    Task<string> RecognizeTextAsync(Bitmap bitmap, string? apiKey = null, string? apiSecret = null);
}

public class OcrService : IOcrService
{
    private readonly HttpClient _http = new();

    public async Task<string> RecognizeTextAsync(Bitmap bitmap, string? apiKey = null, string? apiSecret = null)
    {
        if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
        {
            try { return await YoudaoOcrAsync(bitmap, apiKey, apiSecret); }
            catch { }
        }
        return "请先在设置中配置有道云 API Key 和 Secret（注册: https://ai.youdao.com）";
    }

    private async Task<string> YoudaoOcrAsync(Bitmap bitmap, string appKey, string appSecret)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Jpeg);
        var imgBase64 = Convert.ToBase64String(ms.ToArray());
        var salt = Guid.NewGuid().ToString("N");
        var curtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signStr = appKey + salt + curtime + appSecret;
        var sign = ComputeHash(signStr, appSecret);
        var form = new Dictionary<string, string>
        {
            ["img"] = imgBase64, ["langType"] = "zh-CHS",
            ["detectType"] = "10012", ["imageType"] = "1",
            ["appKey"] = appKey, ["salt"] = salt,
            ["sign"] = sign, ["signType"] = "v3", ["curtime"] = curtime
        };
        var resp = await _http.PostAsync("https://openapi.youdao.com/ocrapi", new FormUrlEncodedContent(form));
        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("Result").GetProperty("regions")[0].GetProperty("lines")[0].GetProperty("text").GetString() ?? "";
    }

    private static string ComputeHash(string input, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}
