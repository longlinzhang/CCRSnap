using System.Timers;
using CCRSnap.Models;
using Timer = System.Timers.Timer;

namespace CCRSnap.Services;

public interface ISchedulingService
{
    event Action? CaptureTriggered;
    bool IsRunning { get; }
    void Start(AppSettings settings);
    void Stop();
}

public class SchedulingService : ISchedulingService, IDisposable
{
    private Timer? _timer;
    private readonly object _lock = new();
    private AppSettings? _currentSettings;

    public event Action? CaptureTriggered;
    public bool IsRunning { get; private set; }

    public void Start(AppSettings settings)
    {
        Stop();
        _currentSettings = settings;

        _timer = new Timer();
        _timer.AutoReset = false;
        _timer.Elapsed += OnTimerElapsed;

        double delayMs = CalculateNextDelay(settings);
        _timer.Interval = Math.Max(delayMs, 1000);
        _timer.Start();
        IsRunning = true;

        System.Diagnostics.Trace.WriteLine(
            $"Scheduling started, next capture in {_timer.Interval / 1000:F1}s");
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Elapsed -= OnTimerElapsed;
                _timer.Dispose();
                _timer = null;
            }
            IsRunning = false;
        }
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        CaptureTriggered?.Invoke();

        // Recalculate for next interval (unless stopped by event handler)
        lock (_lock)
        {
            if (_timer != null && _currentSettings != null)
            {
                double delayMs = CalculateNextDelay(_currentSettings);
                _timer.Interval = Math.Max(delayMs, 1000);
                _timer.Start();
            }
        }
    }

    private static double CalculateNextDelay(AppSettings settings)
    {
        var now = DateTime.Now;

        switch (settings.ScheduleMode)
        {
            case ScheduleMode.Hourly:
                // Next whole hour
                return (60 - now.Minute) * 60000 - now.Second * 1000 - now.Millisecond;

            case ScheduleMode.HalfHourly:
                // Next half-hour or hour
                if (now.Minute < 30)
                    return (30 - now.Minute) * 60000 - now.Second * 1000 - now.Millisecond;
                else
                    return (60 - now.Minute) * 60000 - now.Second * 1000 - now.Millisecond;

            case ScheduleMode.Now:
                int intSec = settings.IntervalSeconds;
                int totalSec = now.Hour * 3600 + now.Minute * 60 + now.Second;
                int mod = totalSec % intSec;
                int delaySec = mod == 0 ? intSec : intSec - mod;
                return delaySec * 1000 - now.Millisecond + 500;

            case ScheduleMode.SpecificTime:
                if (TimeSpan.TryParse(settings.SpecificTime, out var target))
                {
                    var targetDt = now.Date + target;
                    if (targetDt <= now)
                        targetDt = targetDt.AddDays(1);
                    return (targetDt - now).TotalMilliseconds;
                }
                return 30000;

            default:
                return 30000;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
