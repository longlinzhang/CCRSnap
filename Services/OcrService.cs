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
            return "请先在设置中配置 DeepSeek API Key";

        // 缩小图片到 max 1200px 宽，降低 JPEG 质量到 40%
        using var resized = ResizeBitmap(bitmap, 1200);
        using var ms = new MemoryStream();
        resized.Save(ms, ImageFormat.Jpeg);
        var b64 = Convert.ToBase64String(ms.ToArray());

        var payload = new
        {
            model = "deepseek-chat",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = "Extract ALL text from this image. Output ONLY the recognized text exactly as it appears, preserving line breaks. No explanations:\n\n" + b64
                }
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

    private static Bitmap ResizeBitmap(Bitmap src, int maxWidth)
    {
        if (src.Width <= maxWidth) return new Bitmap(src);
        float ratio = (float)maxWidth / src.Width;
        int w = maxWidth;
        int h = (int)(src.Height * ratio);
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(src, 0, 0, w, h);
        return bmp;
    }
}
