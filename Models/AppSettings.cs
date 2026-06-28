using System.Text.Json.Serialization;

namespace CCRSnap.Models;

public enum ScheduleMode
{
    Now,
    Hourly,
    HalfHourly,
    SpecificTime
}

public enum CaptureMode
{
    Separate,
    Combined
}

public enum ImageFormatType
{
    Jpeg,
    Png,
    Gif,
    Tiff,
    Bmp
}

public class AppSettings
{
    // Save path
    public string SavePath { get; set; } = "";

    // Schedule
    public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Now;
    public int IntervalSeconds { get; set; } = 30;
    public string? SpecificTime { get; set; }

    // Capture
    public CaptureMode CaptureMode { get; set; } = CaptureMode.Separate;
    public int ScreenIndex { get; set; } = 0;
    public bool ScreenshotEnabled { get; set; } = true;

    // File
    public ImageFormatType FileFormat { get; set; } = ImageFormatType.Jpeg;
    public int JpegQuality { get; set; } = 80;
    public string FilePrefix { get; set; } = "";
    public string CustomExtension { get; set; } = "jpg";

    // Auto-delete
    public bool AutoDelete { get; set; } = true;
    public int KeepDays { get; set; } = 30;

    // Behavior
    public bool AutoStart { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool StartAtLogin { get; set; } = false;
    public bool HideSaveFolder { get; set; } = false;

    // Diff detection
    public bool DetectChange { get; set; } = false;
    public int Tolerance { get; set; } = 10;
    public double DiffRatio { get; set; } = 10.0;

    // WeCom push
    public bool PushToWeCom { get; set; } = false;
    public string WebhookUrl { get; set; } = "";

    [JsonIgnore]
    public string LastFileName { get; set; } = "";

    // Language
    public string Language { get; set; } = "zh-CN";
    public string? TencentSecretId { get; set; }
    public string? TencentSecretKey { get; set; }
}
