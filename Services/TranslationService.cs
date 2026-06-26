using System.Net.Http;
using System.Net.Http.Headers;
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
        if (string.IsNullOrEmpty(apiKey))
            return "请先配置 DeepSeek API Key";
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var payload = new
        {
            model = "deepseek-chat",
            messages = new[]
            {
                new { role = "user", content = $"请将以下文本翻译成中文，直接返回翻译结果，不要附加任何说明：\n\n{text}" }
            },
            max_tokens = 4096
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req);
        var respJson = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(respJson);

        if (doc.RootElement.TryGetProperty("error", out var err))
            return $"API Error: {err.GetProperty("message").GetString()}";

        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}
