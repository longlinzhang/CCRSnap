using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
namespace CCRSnap.Services;
public interface IWeComPushService
{
    Task<bool> PushImageAsync(Bitmap image, string webhookUrl);
}
public class WeComPushService : IWeComPushService
{
    private readonly HttpClient _httpClient;
    public WeComPushService()
    {
        _httpClient = new HttpClient();
    }
    public async Task<bool> PushImageAsync(Bitmap image, string webhookUrl)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl) || image == null)
            return false;
        try
        {
            // Image -> bytes
            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Png);
                imageBytes = ms.ToArray();
            }
            // Base64
            string base64 = Convert.ToBase64String(imageBytes);
            // MD5
            string md5;
            using (var md5Hash = MD5.Create())
            {
                byte[] hashBytes = md5Hash.ComputeHash(imageBytes);
                md5 = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
            var messageBody = new
            {
                msgtype = "image",
                image = new
                {
                    base64,
                    md5
                }
            };
            var response = await _httpClient.PostAsJsonAsync(webhookUrl, messageBody);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"WeCom push failed: {ex.Message}");
            return false;
        }
    }
}