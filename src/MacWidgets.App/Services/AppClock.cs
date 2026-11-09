using System.Windows.Threading;

namespace MacWidgets.Services;

/// <summary>全局分钟节拍（跨天时刷新提醒事项等）。</summary>
public static class AppClock
{
    public static event Action? MinuteTick;
    public static event Action? DayChanged;
    private static DispatcherTimer? _timer;
    private static int _lastMinute = -1;
    private static DateTime _lastDay = DateTime.Today;

    public static void Start()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            var now = DateTime.Now;
            if (now.Minute != _lastMinute)
            {
                _lastMinute = now.Minute;
                MinuteTick?.Invoke();
            }
            if (now.Date != _lastDay)
            {
                _lastDay = now.Date;
                DayChanged?.Invoke();
            }
        };
        _timer.Start();
        var d = Dispatcher.CurrentDispatcher;
        Microsoft.Win32.SystemEvents.TimeChanged += (_, _) => d.BeginInvoke(() => MinuteTick?.Invoke());
    }
}
