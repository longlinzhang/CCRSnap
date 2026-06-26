using System.Diagnostics;
using System.IO;
using CCRSnap.Native;
using CCRSnap.Services;
using CCRSnap.ViewModels;
using CCRSnap.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using System.Windows.Threading;

namespace CCRSnap;

public partial class App : System.Windows.Application
{
    private readonly IHost? _host;

    public App()
    {
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Exception? ex = e.ExceptionObject as Exception;
            string msg = ex?.ToString() ?? "Unknown error";
            System.Diagnostics.Trace.WriteLine($"FATAL: {msg}");
            File.WriteAllText(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CCRSnap", "crash.log"), msg);
        };

        // DPI awareness
        try { NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
        catch { try { NativeMethods.SetProcessDPIAware(); } catch { } }

        // Single instance check
        string processName = Process.GetCurrentProcess().ProcessName;
        Process[] existing = Process.GetProcessesByName(processName);
        if (existing.Length > 1)
        {
            System.Windows.MessageBox.Show("程序已在运行中!", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Current.Shutdown();
            return;
        }

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IHotkeyService, HotkeyService>();
                services.AddSingleton<ISchedulingService, SchedulingService>();
                services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
                services.AddSingleton<IFileStorageService, FileStorageService>();
                services.AddSingleton<IImageDiffService, ImageDiffService>();
                services.AddSingleton<IWeComPushService, WeComPushService>();
                services.AddSingleton<IOcrService, OcrService>();
                services.AddSingleton<ITranslationService, TranslationService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Trace.WriteLine($"Dispatcher exception: {e.Exception}");
        e.Handled = true;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (_host != null)
        {
            try
            {
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                try { System.IO.File.WriteAllText(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CCRSnap", "crash.log"),
                    ex.ToString()); } catch { }
                System.Windows.MessageBox.Show($"启动失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
