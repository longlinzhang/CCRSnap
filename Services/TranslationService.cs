using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CCRSnap.Services;

public interface ITranslationService
{
    Task<string> TranslateAsync(string text, string? secretId, string? secretKey,
        string from = "auto", string to = "zh");
}

public class TranslationService : ITranslationService
{
    private readonly HttpClient _http = new();

    public async Task<string> TranslateAsync(string text, string? secretId, string? secretKey,
        string from = "auto", string to = "zh")
    {
        if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
            return "请先配置腾讯云 SecretId 和 SecretKey";
        if (string.IsNullOrWhiteSpace(text)) return "";

        var body = JsonSerializer.Serialize(new { SourceText = text, Source = from, Target = to, ProjectId = 0 });
        var (auth, ts) = Tc3Helper.Sign(secretId, secretKey, "tmt", "TextTranslate", "ap-guangzhou", body);

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://tmt.tencentcloudapi.com");
        req.Headers.TryAddWithoutValidation("Authorization", auth);
        req.Headers.TryAddWithoutValidation("X-TC-Action", "TextTranslate");
        req.Headers.TryAddWithoutValidation("X-TC-Timestamp", ts.ToString());
        req.Headers.TryAddWithoutValidation("X-TC-Version", "2018-03-21");
        req.Headers.TryAddWithoutValidation("X-TC-Region", "ap-guangzhou");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("Response", out var r))
        {
            if (r.TryGetProperty("Error", out var e))
                return $"API Error: {e.GetProperty("Message").GetString()}";
            return r.GetProperty("TargetText").GetString() ?? "";
        }
        return "翻译失败";
    }
}
