using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CCRSnap.Models;

namespace CCRSnap.Services;

public interface IOcrService
{
    Task<string> RecognizeTextAsync(Bitmap bitmap, string? secretId = null, string? secretKey = null, OcrApiType apiType = OcrApiType.GeneralBasic);
}

public class OcrService : IOcrService
{
    private readonly HttpClient _http = new();
    private static readonly string[] ApiNames = {
        "GeneralBasicOCR", "GeneralAccurateOCR", "GeneralHandwritingOCR",
        "RecognizeTableOCR", "RecognizeTableV2", "QrcodeOCR", "ImageRectify"
    };

    public async Task<string> RecognizeTextAsync(Bitmap bitmap, string? secretId = null, string? secretKey = null, OcrApiType apiType = OcrApiType.GeneralBasic)
    {
        if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
            return "请先在设置中配置腾讯云 SecretId 和 SecretKey";

        using var ms = new MemoryStream();
        int idx = (int)apiType;
        // For QR/barcode need different image processing
        if (apiType == OcrApiType.Qrcode) bitmap.Save(ms, ImageFormat.Png);
        else bitmap.Save(ms, ImageFormat.Jpeg);
        var b64 = Convert.ToBase64String(ms.ToArray());

        var action = ApiNames[idx];
        var payload = JsonSerializer.Serialize(new { ImageBase64 = b64 });
        var (auth, ts) = Tc3Helper.Sign(secretId, secretKey, "ocr", action, "ap-guangzhou", payload);

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://ocr.tencentcloudapi.com");
        req.Headers.TryAddWithoutValidation("Authorization", auth);
        req.Headers.TryAddWithoutValidation("X-TC-Action", action);
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
            {
                var code = e.GetProperty("Code").GetString();
                var msg = e.GetProperty("Message").GetString();
                if (code == "FailedOperation.UnOpenError")
                    return "OCR 服务未开通，请前往 https://console.cloud.tencent.com/ocr/overview 开通后使用";
                return $"API Error ({code}): {msg}";
            }
            // Try common response formats
            if (r.TryGetProperty("TextDetections", out var dets) && dets.ValueKind == System.Text.Json.JsonValueKind.Array) // GeneralBasic/Accurate/Handwriting
                return JoinText(dets.EnumerateArray(), "DetectedText");
            if (r.TryGetProperty("TableDetections", out var tbl) && tbl.ValueKind == System.Text.Json.JsonValueKind.Array) // Table
                return JoinText(tbl.EnumerateArray(), "Text");
            if (r.TryGetProperty("CodeResults", out var codes) && codes.ValueKind == System.Text.Json.JsonValueKind.Array) // QR/Barcode
                return JoinText(codes.EnumerateArray(), "Text");
            if (apiType == OcrApiType.ImageRectify)
                return "图像切边矫正完成（返回的是矫正后的图像数据，非文字）";
            // Fallback: show raw response for debugging
            return $"响应格式未知，原始数据: {r.ToString()[..200]}";
        }
        return "未识别到内容";
    }

    private static string JoinText(JsonElement.ArrayEnumerator arr, string prop)
    {
        var sb = new StringBuilder();
        foreach (var item in arr)
            sb.AppendLine(item.GetProperty(prop).GetString());
        return sb.ToString().TrimEnd();
    }
}
