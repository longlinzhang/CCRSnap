using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using CCRSnap.Models;
namespace CCRSnap.Services;
public interface IFileStorageService
{
    string SaveImage(Bitmap image, AppSettings settings, string suffix);
    void DeleteOldFolders(string basePath, int keepDays);
    string GetTimestampFileName();
}
public class FileStorageService : IFileStorageService
{
    public string SaveImage(Bitmap image, AppSettings settings, string suffix)
    {
        string basePath = settings.SavePath;
        var now = DateTime.Now;
        string year = now.Year.ToString();
        string month = now.Month.ToString("D2");
        string day = now.Day.ToString("D2");
        string dir = Path.Combine(basePath, year, $"{year}-{month}", $"{year}-{month}-{day}");
        Directory.CreateDirectory(dir);
        if (settings.HideSaveFolder)
        {
            try { File.SetAttributes(basePath, FileAttributes.Hidden); }
            catch { }
        }
        string timestamp = $"{year}-{month}-{day}___{now.Hour:D2}.{now.Minute:D2}.{now.Second:D2}";
        string prefix = settings.FilePrefix;
        string ext = settings.CustomExtension;
        string fileName = $"{prefix}{suffix}{timestamp}.{ext}";
        string fullPath = Path.Combine(dir, fileName);
        if (settings.FileFormat == ImageFormatType.Jpeg)
        {
            var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(c => c.MimeType == "image/jpeg");
            if (jpegEncoder != null)
            {
                var ep = new EncoderParameters(1);
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,
                    Math.Clamp(settings.JpegQuality, 1, 100));
                image.Save(fullPath, jpegEncoder, ep);
                ep.Param[0]?.Dispose();
                return fullPath;
            }
        }
        var format = settings.FileFormat switch
        {
            ImageFormatType.Png => ImageFormat.Png,
            ImageFormatType.Gif => ImageFormat.Gif,
            ImageFormatType.Tiff => ImageFormat.Tiff,
            ImageFormatType.Bmp => ImageFormat.Bmp,
            _ => ImageFormat.Jpeg
        };
        image.Save(fullPath, format);
        return fullPath;
    }
    public void DeleteOldFolders(string basePath, int keepDays)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-keepDays);
            string yearDir = Path.Combine(basePath, cutoff.Year.ToString());
            if (!Directory.Exists(yearDir)) return;
            string monthDir = Path.Combine(yearDir, $"{cutoff.Year}-{cutoff.Month:D2}");
            if (!Directory.Exists(monthDir)) return;
            string dayDir = Path.Combine(monthDir,
                $"{cutoff.Year}-{cutoff.Month:D2}-{cutoff.Day:D2}");
            if (Directory.Exists(dayDir))
                Directory.Delete(dayDir, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Delete old files error: {ex.Message}");
        }
    }
    public string GetTimestampFileName()
    {
        var now = DateTime.Now;
        return $"{now.Year}-{now.Month:D2}-{now.Day:D2}___{now.Hour:D2}.{now.Minute:D2}.{now.Second:D2}";
    }
}