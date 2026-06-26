using System.Windows;
using CCRSnap.Models;
using CCRSnap.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CCRSnap.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ISchedulingService _schedulingService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IScreenCaptureService _captureService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IImageDiffService _diffService;
    private readonly IWeComPushService? _weComPushService;

    public AppSettings Settings => _settingsService.Settings;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private int _captureCount;
    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private string _copyrightText = "CCRSnap WPF";
    [ObservableProperty] private bool _isDetectChecked;

    public MainViewModel(ISettingsService settingsService,
        ISchedulingService schedulingService,
        IHotkeyService hotkeyService,
        IScreenCaptureService captureService,
        IFileStorageService fileStorageService,
        IImageDiffService diffService,
        IWeComPushService? weComPushService = null)
    {
        _settingsService = settingsService;
        _schedulingService = schedulingService;
        _hotkeyService = hotkeyService;
        _captureService = captureService;
        _fileStorageService = fileStorageService;
        _diffService = diffService;
        _weComPushService = weComPushService;
        _schedulingService.CaptureTriggered += OnCaptureTriggered;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _isDetectChecked = Settings.DetectChange;
    }

    private void OnCaptureTriggered() =>
        global::System.Windows.Application.Current.Dispatcher.Invoke(() => DoScheduledCapture());

    private void OnHotkeyPressed(int id) =>
        global::System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            switch (id)
            {
                case 100: DoManualCapture(); break;
                case 101: ToggleWindowVisibility(); break;
                case 102: DoQuickSave(); break;
            }
        });

    public void DoManualCapture()
    {
        var captureWin = new Views.CaptureWindow(_captureService);
        captureWin.WindowStartupLocation = WindowStartupLocation.Manual;
        captureWin.ShowInTaskbar = false;
        bool? result = captureWin.ShowDialog();
        if (result == true && captureWin.CapturedImage != null)
        {
            var editor = new Views.EditorWindow(
                captureWin.CapturedImage, _settingsService,
                _fileStorageService, _weComPushService);
            editor.ShowDialog();
        }
    }

    public void StartScheduling()
    {
        var settings = _settingsService.Settings;
        if (settings.ScheduleMode == ScheduleMode.SpecificTime &&
            string.IsNullOrWhiteSpace(settings.SpecificTime))
        {
            global::System.Windows.MessageBox.Show("请先设置指定时间", "提示");
            return;
        }
        if (string.IsNullOrWhiteSpace(settings.SavePath))
        {
            global::System.Windows.MessageBox.Show("请先设置保存路径", "提示");
            return;
        }
        _schedulingService.Start(settings);
        IsRunning = true;
        StatusText = "定时截图运行中...";
    }

    public void StopScheduling()
    {
        _schedulingService.Stop();
        IsRunning = false;
        StatusText = "已停止";
    }

    public void DoScheduledCapture()
    {
        try
        {
            var settings = _settingsService.Settings;
            if (!settings.ScreenshotEnabled) return;
            if (settings.AutoDelete)
                _fileStorageService.DeleteOldFolders(settings.SavePath, settings.KeepDays);
            if (settings.CaptureMode == CaptureMode.Separate)
            {
                if (settings.DetectChange)
                {
                    using var img = _captureService.CaptureScreen(settings.ScreenIndex);
                    string suffix = GetScreenSuffix(settings.ScreenIndex);
                    bool shouldSave = true;
                    if (!string.IsNullOrEmpty(settings.LastFileName))
                    {
                        double diff = _diffService.Compare(settings.LastFileName, img, settings.Tolerance);
                        shouldSave = diff > settings.DiffRatio;
                    }
                    if (shouldSave)
                    {
                        string path = _fileStorageService.SaveImage(img, settings, suffix);
                        settings.LastFileName = path;
                        _settingsService.Save();
                        CaptureCount++;
                        if (settings.PushToWeCom && !string.IsNullOrWhiteSpace(settings.WebhookUrl))
                            _ = _weComPushService?.PushImageAsync(img, settings.WebhookUrl);
                    }
                }
                else
                {
                    int count = _captureService.ScreenCount;
                    for (int i = 0; i < count; i++)
                    {
                        using var img = _captureService.CaptureScreen(i);
                        string suffix = count > 1 ? GetScreenSuffix(i) : "";
                        _fileStorageService.SaveImage(img, settings, suffix);
                        CaptureCount++;
                    }
                }
            }
            else
            {
                using var img = _captureService.CaptureFullScreen();
                _fileStorageService.SaveImage(img, settings, "");
                CaptureCount++;
                if (settings.PushToWeCom && !string.IsNullOrWhiteSpace(settings.WebhookUrl))
                    _ = _weComPushService?.PushImageAsync(img, settings.WebhookUrl);
            }
            StatusText = $"已截图: {CaptureCount} 张";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Capture error: {ex.Message}");
        }
    }

    public void DoQuickSave()
    {
        try
        {
            using var img = _captureService.CaptureFullScreen();
            _fileStorageService.SaveImage(img, _settingsService.Settings, ".full");
            CaptureCount++;
            StatusText = "已保存全屏截图";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Quick save error: {ex.Message}");
        }
    }

    public Action? ToggleWindowAction { get; set; }

    public void SaveSettings()

    {
        _settingsService.Save();
        StatusText = "设置已保存";
    }

    public void ToggleWindowVisibility()
    {
        ToggleWindowAction?.Invoke();
    }

    private static string GetScreenSuffix(int index) => index switch
    {
        0 => "L", 1 => "R", 2 => "A", 3 => "B", 4 => "C", _ => $"S{index}"
    };
}
