using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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
        if (string.IsNullOrEmpty(apiKey))
            return "请先在设置中配置 DeepSeek API Key（获取: https://platform.deepseek.com/）";

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Jpeg);
        var b64 = Convert.ToBase64String(ms.ToArray());

        var payload = new
        {
            model = "deepseek-chat",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = "请识别图片中的所有文字，直接返回识别结果，不要附加任何说明" },
                        new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{b64}" } }
                    }
                }
            },
            max_tokens = 2048
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
