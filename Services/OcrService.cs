using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CCRSnap.Services;

public interface IOcrService
{
    Task<string> RecognizeTextAsync(Bitmap bitmap, string? secretId = null, string? secretKey = null);
}

public class OcrService : IOcrService
{
    private readonly HttpClient _http = new();

    public async Task<string> RecognizeTextAsync(Bitmap bitmap, string? secretId = null, string? secretKey = null)
    {
        if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
            return "请先在设置中配置腾讯云 SecretId 和 SecretKey（获取: https://console.cloud.tencent.com/cam/capi）";

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Jpeg);
        var b64 = Convert.ToBase64String(ms.ToArray());

        var payload = JsonSerializer.Serialize(new { ImageBase64 = b64 });
        var (auth, ts) = Tc3Helper.Sign(secretId, secretKey, "ocr", "RecognizeGeneralOCR", "ap-guangzhou", payload);

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://ocr.tencentcloudapi.com");
        req.Headers.TryAddWithoutValidation("Authorization", auth);
        req.Headers.TryAddWithoutValidation("X-TC-Action", "RecognizeGeneralOCR");
        req.Headers.TryAddWithoutValidation("X-TC-Timestamp", ts.ToString());
        req.Headers.TryAddWithoutValidation("X-TC-Version", "2018-11-19");
        req.Headers.TryAddWithoutValidation("X-TC-Region", "ap-guangzhou");
        req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("Response", out var r))
        {
            if (r.TryGetProperty("Error", out var e))
                return $"API Error: {e.GetProperty("Message").GetString()}";
            if (r.TryGetProperty("TextDetections", out var dets))
            {
                var sb = new System.Text.StringBuilder();
                foreach (var item in dets.EnumerateArray())
                    sb.AppendLine(item.GetProperty("DetectedText").GetString());
                return sb.ToString().TrimEnd();
            }
        }
        return "未识别到文字";
    }
}
