using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using CCRSnap.Models;
using CCRSnap.Native;
using CCRSnap.Services;
using CCRSnap.ViewModels;
using Application = System.Windows.Application;

namespace CCRSnap.Views;

public enum KeyModifiers : uint
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    WindowsKey = 8
}

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly ISettingsService _settingsService;
    private readonly IHotkeyService _hotkeyService;
    private NotifyIcon? _trayIcon;
    private bool _forceClose;

    public MainWindow(MainViewModel vm, ISettingsService settingsService,
        IHotkeyService hotkeyService)
    {
        InitializeComponent();
        _vm = vm;
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        Loaded += OnLoaded;
        _vm.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(MainViewModel.CaptureCount))
                StatusCount.Text = _vm.CaptureCount.ToString();
        };
        this.StateChanged += (_, _) => {
            if (this.WindowState == WindowState.Minimized)
            { this.Hide(); _trayIcon!.Visible = true; }
        };
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetupTrayIcon();
        var s = _settingsService.Settings;
        TbxPath.Text = s.SavePath;
        TbxInterval.Text = s.IntervalSeconds.ToString();
        TbxDays.Text = s.KeepDays.ToString();
        TbxQuality.Text = s.JpegQuality.ToString();
        TbxDiffRatio.Text = s.DiffRatio.ToString("F1");
        TbxTolerance.Text = s.Tolerance.ToString();
        TbxPrefix.Text = s.FilePrefix;
        TbxExtName.Text = s.CustomExtension;
        TbxWebhook.Text = s.WebhookUrl;
        TbxSecretId.Text = s.TencentSecretId ?? "";
        TbxSecretKey.Text = s.TencentSecretKey ?? "";
        CboOcrApi.Items.Clear();
        foreach (var item in new[] { "通用文字识别", "高精度版", "手写体识别", "表格识别V1", "表格识别V2", "二维码识别", "图像矫正" })
            CboOcrApi.Items.Add(item);
        CboOcrApi.SelectedIndex = (int)s.SelectedOcrApi;
        CbxAutoDelete.IsChecked = s.AutoDelete;
        CbxHide.IsChecked = s.HideSaveFolder;
        CbxLogin.IsChecked = s.StartAtLogin;
        CbxAutoStart.IsChecked = s.AutoStart;
        CbxScreenshot.IsChecked = s.ScreenshotEnabled;
        CbxAutoCleanMem.IsChecked = s.AutoCleanMemory;
        CbxDetect.IsChecked = s.DetectChange;
        CbxPushWecom.IsChecked = s.PushToWeCom;
        SetScheduleMode(s.ScheduleMode);
        RbtnSeparate.IsChecked = s.CaptureMode == CaptureMode.Separate;
        RbtnCombine.IsChecked = s.CaptureMode == CaptureMode.Combined;
        SetFileFormat(s.FileFormat);
        SetScreenIndex(s.ScreenIndex);
        _hotkeyService.Attach(this);
        RegisterHotkeys();
        _vm.ToggleWindowAction = () => {
            if (this.Visibility == Visibility.Visible) {
                this.Hide(); _trayIcon!.Visible = false;
            } else {
                this.Show(); this.WindowState = WindowState.Normal;
                _trayIcon!.Visible = true;
            }
        };
        if (s.AutoStart) { _vm.StartScheduling(); UpdateRunningState(); }
    }

    private void SetupTrayIcon()
    {
        System.Drawing.Icon? appIcon = null;
        try
        {
            string? exePath = Environment.ProcessPath;
            if (exePath != null && File.Exists(exePath))
                appIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
        }
        catch { }

        _trayIcon = new NotifyIcon
        {
            Icon = appIcon ?? System.Drawing.SystemIcons.Application,
            Text = "CCRSnap - 截图工具",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _trayIcon.ContextMenuStrip.Items.Add("显示主窗口", null, (_, _) => ShowWindow());
        _trayIcon.ContextMenuStrip.Items.Add("隐藏到托盘", null, (_, _) => HideWindow());
        _trayIcon.ContextMenuStrip.Items.Add("-");
        _trayIcon.ContextMenuStrip.Items.Add("退出", null, (_, _) => ExitApp());
        _trayIcon.DoubleClick += (_, _) => ShowWindow();
    }

    private void RegisterHotkeys()
    {
        _hotkeyService.Register(100, (uint)KeyModifiers.Alt, (uint)Keys.A);
        _hotkeyService.Register(101, (uint)KeyModifiers.Ctrl, (uint)Keys.F12);
        _hotkeyService.Register(102, (uint)KeyModifiers.Ctrl, (uint)Keys.Oemtilde);
    }

    #region UI State Updates
    private void SetScheduleMode(ScheduleMode mode)
    {
        RbtnNow.IsChecked = mode == ScheduleMode.Now;
        RbtnTime.IsChecked = mode == ScheduleMode.SpecificTime;
        RbtnHourly.IsChecked = mode == ScheduleMode.Hourly;
        RbtnHalfHour.IsChecked = mode == ScheduleMode.HalfHourly;
        if (mode == ScheduleMode.SpecificTime && !string.IsNullOrEmpty(_settingsService.Settings.SpecificTime))
            TbxTime.Text = _settingsService.Settings.SpecificTime;
    }

    private void SetFileFormat(ImageFormatType fmt)
    {
        RbtnJpg.IsChecked = fmt == ImageFormatType.Jpeg;
        RbtnPng.IsChecked = fmt == ImageFormatType.Png;
        RbtnGif.IsChecked = fmt == ImageFormatType.Gif;
        RbtnTif.IsChecked = fmt == ImageFormatType.Tiff;
        RbtnBmp.IsChecked = fmt == ImageFormatType.Bmp;
    }

    private void SetScreenIndex(int idx)
    {
        var btns = new[] { Screen0, Screen1, Screen2, Screen3, Screen4, Screen5 };
        if (idx >= 0 && idx < btns.Length) btns[idx].IsChecked = true;
    }

    private void UpdateRunningState()
    {
        BtnStart.IsEnabled = !_vm.IsRunning;
        BtnStop.IsEnabled = _vm.IsRunning;
        StatusLeft.Content = _vm.IsRunning ? "定时截图运行中..." : "就绪";
    }

    private ScheduleMode GetSelectedScheduleMode()
    {
        if (RbtnNow.IsChecked == true) return ScheduleMode.Now;
        if (RbtnTime.IsChecked == true) return ScheduleMode.SpecificTime;
        if (RbtnHourly.IsChecked == true) return ScheduleMode.Hourly;
        if (RbtnHalfHour.IsChecked == true) return ScheduleMode.HalfHourly;
        return ScheduleMode.Now;
    }

    private ImageFormatType GetSelectedFileFormat()
    {
        if (RbtnJpg.IsChecked == true) return ImageFormatType.Jpeg;
        if (RbtnPng.IsChecked == true) return ImageFormatType.Png;
        if (RbtnGif.IsChecked == true) return ImageFormatType.Gif;
        if (RbtnTif.IsChecked == true) return ImageFormatType.Tiff;
        if (RbtnBmp.IsChecked == true) return ImageFormatType.Bmp;
        return ImageFormatType.Jpeg;
    }

    private int GetSelectedScreenIndex()
    {
        var btns = new[] { Screen0, Screen1, Screen2, Screen3, Screen4, Screen5 };
        for (int i = 0; i < btns.Length; i++)
            if (btns[i].IsChecked == true) return i;
        return 0;
    }
    #endregion

    #region Collect Settings from UI
    private void CollectSettings()
    {
        var s = _settingsService.Settings;
        int.TryParse(TbxInterval.Text, out int interval);
        s.IntervalSeconds = Math.Max(interval, 5);
        TbxInterval.Text = s.IntervalSeconds.ToString();
        int.TryParse(TbxDays.Text, out int days);
        s.KeepDays = Math.Max(days, 1);
        TbxDays.Text = s.KeepDays.ToString();
        int.TryParse(TbxQuality.Text, out int quality);
        s.JpegQuality = Math.Clamp(quality, 1, 100);
        TbxQuality.Text = s.JpegQuality.ToString();
        double.TryParse(TbxDiffRatio.Text, out double diff);
        s.DiffRatio = Math.Clamp(diff, 0, 100);
        int.TryParse(TbxTolerance.Text, out int tol);
        s.Tolerance = Math.Clamp(tol, 0, 255);
        s.SavePath = TbxPath.Text;
        s.FilePrefix = TbxPrefix.Text;
        s.CustomExtension = TbxExtName.Text;
        s.WebhookUrl = TbxWebhook.Text;
        s.AutoDelete = CbxAutoDelete.IsChecked == true;
        s.HideSaveFolder = CbxHide.IsChecked == true;
        s.StartAtLogin = CbxLogin.IsChecked == true;
        s.AutoStart = CbxAutoStart.IsChecked == true;
        s.ScreenshotEnabled = CbxScreenshot.IsChecked == true;
        s.AutoCleanMemory = CbxAutoCleanMem.IsChecked == true;
        s.DetectChange = CbxDetect.IsChecked == true;
        s.PushToWeCom = CbxPushWecom.IsChecked == true;
        s.ScheduleMode = GetSelectedScheduleMode();
        s.CaptureMode = RbtnSeparate.IsChecked == true ? CaptureMode.Separate : CaptureMode.Combined;
        s.FileFormat = GetSelectedFileFormat();
        s.ScreenIndex = GetSelectedScreenIndex();
        if (s.ScheduleMode == ScheduleMode.SpecificTime)
            s.SpecificTime = TbxTime.Text.Trim();
        _settingsService.Save();
    }
    #endregion

    #region Event Handlers
    private void BtnCapture_Click(object sender, RoutedEventArgs e)
    {
        CollectSettings();
        this.Hide();
        _vm.DoManualCapture();
        this.Show();
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        CollectSettings();
        _vm.StartScheduling();
        UpdateRunningState();
        if (!_settingsService.Settings.AutoStart) this.Hide();
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _vm.StopScheduling();
        UpdateRunningState();
    }

    private void BtnCleanMem_Click(object sender, RoutedEventArgs e)
    {
        StatusLeft.Content = "正在清理内存...";
        _vm.CleanMemory();
        StatusLeft.Content = "内存已清理";
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        CollectSettings();
        // Apply startup registry setting
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (CbxLogin.IsChecked == true)
                    key.SetValue("CCRSnap", $"\"{Environment.ProcessPath}\"");
                else
                    key.DeleteValue("CCRSnap", false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Startup reg error: {ex.Message}");
        }
        StatusLeft.Content = $"设置已保存 ({_settingsService.SettingsPath})";
    }

    private void BtnFixHK_Click(object sender, RoutedEventArgs e)
    {
        _hotkeyService.Unregister(100);
        _hotkeyService.Unregister(101);
        _hotkeyService.Unregister(102);
        RegisterHotkeys();
        _vm.ToggleWindowAction = () => {
            if (this.Visibility == Visibility.Visible) {
                this.Hide(); _trayIcon!.Visible = false;
            } else {
                this.Show(); this.WindowState = WindowState.Normal;
                _trayIcon!.Visible = true;
            }
        };
        StatusLeft.Content = "热键已重新注册";
    }

    private void Schedule_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        bool isTime = RbtnTime.IsChecked == true;
        TimePanel.Visibility = isTime ? Visibility.Visible : Visibility.Collapsed;
        if (RbtnNow.IsChecked == true) TbxInterval.IsEnabled = true;
        else TbxInterval.IsEnabled = isTime;
    }
    private void Detect_Changed(object sender, RoutedEventArgs e)
    {
        if (CbxDetect.IsChecked == true)
        {
            CbxScreenshot.IsChecked = true;
            CbxScreenshot.IsEnabled = false;
            RbtnSeparate.IsChecked = true;
            RbtnCombine.IsEnabled = false;
        }
        else
        {
            CbxScreenshot.IsEnabled = true;
            RbtnCombine.IsEnabled = true;
        }
    }
    #endregion

    #region Window Management
    private void LogoImage_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var ci = System.Threading.Thread.CurrentThread.CurrentUICulture;
        System.Threading.Thread.CurrentThread.CurrentUICulture =
            ci.Name.StartsWith("zh")
                ? new System.Globalization.CultureInfo("en-US")
                : new System.Globalization.CultureInfo("zh-CN");
        ApplyLanguage();
        _settingsService.Settings.Language = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
        _settingsService.Save();
        StatusLeft.Content = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh") ? "语言: 中文" : "Lang: English";
    }

    private void ApplyLanguage()
    {
        try
        {
            bool zh = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh");
            this.Title = zh ? "CCRSnap - 截图工具" : "CCRSnap - Screenshot Tool";
            BtnCapture.Content = zh ? "手动截图 (Alt+A)" : "Capture (Alt+A)";
            BtnStart.Content = zh ? "开始" : "Start";
            BtnStop.Content = zh ? "停止" : "Stop";
            BtnSave.Content = zh ? "保存设置" : "Save Settings";
            BtnFixHK.Content = zh ? "修复热键" : "Fix HotKeys";
            BtnCleanMem.Content = zh ? "清理内存" : "Clean Memory";
            CbxAutoCleanMem.Content = zh ? "定时清理内存" : "Auto Clean";
            RbtnNow.Content = zh ? "立即开始" : "Start Now";
            RbtnTime.Content = zh ? "指定时间" : "Scheduled Time";
            RbtnHourly.Content = zh ? "每小时" : "Hourly";
            RbtnHalfHour.Content = zh ? "每半小时" : "Half Hourly";
            RbtnSeparate.Content = zh ? "单独保存" : "Separate";
            RbtnCombine.Content = zh ? "合并保存" : "Combined";
            CbxScreenshot.Content = zh ? "启用截图" : "Enable Capture";
            CbxAutoDelete.Content = zh ? "自动删除旧截图" : "Auto Delete";
            CbxHide.Content = zh ? "隐藏保存文件夹" : "Hide Folder";
            CbxLogin.Content = zh ? "开机启动" : "Startup";
            CbxAutoStart.Content = zh ? "启动时自动开始" : "Auto Start";
            CbxDetect.Content = zh ? "检测屏幕变化" : "Detect Changes";
            CbxPushWecom.Content = zh ? "推送截图到企微群" : "Push to WeCom";
            GbxSchedule.Header = zh ? "定时截图设置" : "Schedule";
            GbxInterval.Header = zh ? "间隔设置" : "Interval";
            GbxFunctions.Header = zh ? "功能选项" : "Functions";
            GbxWecom.Header = zh ? "企业微信推送" : "WeCom Push";
            GbxSaveSettings.Header = zh ? "保存设置" : "Save Settings";
            GbxFileFormat.Header = zh ? "文件格式" : "File Format";
            GbxDetectSet.Header = zh ? "差异检测设置" : "Change Detection";
            GbxScreenIdx.Header = zh ? "检测屏幕" : "Monitor";
            GbxFixKey.Header = zh ? "修复热键" : "HotKeys";
            GbxOcrConfig.Header = zh ? "OCR/翻译配置" : "OCR/Translation Config";
            LabelSavePath.Content = zh ? "保存路径:" : "Save Path:";
            BtnBrowse.Content = zh ? "浏览" : "Browse";
            LabelExtName.Content = zh ? "文件后缀:" : "Suffix:";
            LabelPrefix.Content = zh ? "文件前缀:" : "Prefix:";
            LabelQuality.Content = zh ? "JPEG质量:" : "JPEG Quality:";
            LabelInterval.Content = zh ? "间隔(秒):" : "Interval (sec):";
            LabelDays.Content = zh ? "保留天数:" : "Keep Days:";
            LabelDiffRatio.Content = zh ? "变化率阈值(%):" : "Change Rate (%):";
            LabelTolerance.Content = zh ? "容差:" : "Tolerance:";
            LabelTime.Content = zh ? "时间(HH:mm):" : "Time (HH:mm):";
            LabelWebhook.Content = zh ? "Webhook:" : "Webhook:";
            StatusLeft.Content = zh ? "就绪" : "Ready";
            HotkeyHelpBlock.Text = zh
                ? "\u5982\u679c\u70ed\u952e\u65e0\u6548\uff0c\u70b9\u51fb\u201c\u4fee\u590d\u70ed\u952e\u201d\u91cd\u65b0\u6ce8\u518c\u5feb\u6377\u952e\u3002\nAlt+A = \u624b\u52a8\u622a\u56fe, Ctrl+F12 = \u663e\u793a/\u9690\u85cf, Ctrl+\u0060 = \u5168\u5c4f\u4fdd\u5b58"
                : "If hotkeys don\'t work, click \u201cFix HotKeys\u201d to re-register.\nAlt+A = Capture, Ctrl+F12 = Show/Hide, Ctrl+\u0060 = Fullscreen Save";
            if (_trayIcon != null)
            {
                _trayIcon.Text = zh ? "CCRSnap - 截图工具" : "CCRSnap - Screenshot Tool";
                if (_trayIcon.ContextMenuStrip != null && _trayIcon.ContextMenuStrip.Items.Count >= 4)
                {
                    _trayIcon.ContextMenuStrip.Items[0].Text = zh ? "显示主窗口" : "Show Window";
                    _trayIcon.ContextMenuStrip.Items[1].Text = zh ? "隐藏到托盘" : "Hide to Tray";
                    _trayIcon.ContextMenuStrip.Items[3].Text = zh ? "退出" : "Exit";
                }
            }
        }
        catch { }
    }
    private void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void HideWindow() { Hide(); }

    private void ExitApp()
    {
        _forceClose = true;
        _trayIcon?.Dispose();
        _hotkeyService.Unregister(100);
        _hotkeyService.Unregister(101);
        _hotkeyService.Unregister(102);
        _vm.StopScheduling();
        System.Windows.Application.Current.Shutdown();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceClose) { e.Cancel = true; Hide(); }
    }
    #endregion
}
