using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MacWidgets.Data;
using MacWidgets.Hosting;
using MacWidgets.Interop;
using MacWidgets.Services;
using MacWidgets.UI;

namespace MacWidgets;

public sealed class App : Application
{
    private const string MutexName = @"Local\MacWidgets.SingleInstance";
    private const string ShowEventName = @"Local\MacWidgets.ShowMain";

    public static WidgetHost Host { get; private set; } = null!;
    public static TrayIcon? Tray { get; private set; }
    private static MainWindow? _main;
    private static Mutex? _mutex;
    private static EventWaitHandle? _showEvent;

    [STAThread]
    public static int Main(string[] args)
    {
        _mutex = new Mutex(true, MutexName, out bool created);
        if (!created)
        {
            // 已在运行：通知现有实例显示主界面
            try
            {
                using var ev = EventWaitHandle.OpenExisting(ShowEventName);
                ev.Set();
            }
            catch { }
            return 0;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        // 使用 Win10+ 的 WPF Pointer/Stylus 栈；不要全局禁用 Stylus/Touch。
        AppContext.SetSwitch("Switch.System.Windows.Input.Stylus.EnablePointerSupport", true);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        bool autostart = args.Any(a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));
        app.Startup += (_, _) => app.Init(autostart);
        int code = app.Run();
        GC.KeepAlive(_mutex);
        return code;
    }

    private void Init(bool autostart)
    {
        DispatcherUnhandledException += (_, e) =>
        {
            Store.Log("未处理异常：" + e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Store.Log("致命异常：" + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) => { Store.Log("任务异常：" + e.Exception); e.SetObserved(); };

        Store.Load();
        Store.Log($"启动 v{typeof(App).Assembly.GetName().Version?.ToString(3)} / {Environment.OSVersion.Version} / .NET {Environment.Version} " + (autostart ? "(开机自启)" : ""));
        try
        {
            // 以注册表实际状态为准
            Store.Settings.Autostart = Autostart.IsEnabled();
        }
        catch { }

        AppClock.Start();
        StockHub.Start();
        WeatherHub.Start();

        Host = new WidgetHost();
        Host.Start();
        Tray = new TrayIcon();

        // 到点提醒：有时间的提醒事项到点时弹出托盘通知
        var notified = new HashSet<string>();
        AppClock.MinuteTick += () =>
        {
            var now = DateTime.Now;
            foreach (var r in Store.Reminders.Items)
            {
                if (r.Completed || !r.HasTime || r.Due is not { } due) continue;
                if (due <= now && now - due < TimeSpan.FromMinutes(1) && notified.Add(r.Id + due.Ticks))
                    Notify("提醒事项：" + r.Title);
            }
        };

        if (!autostart) ShowMainWindow();

        var t = new Thread(() =>
        {
            while (_showEvent != null)
            {
                try
                {
                    _showEvent.WaitOne();
                    Current?.Dispatcher.BeginInvoke(ShowMainWindow);
                }
                catch { break; }
            }
        }) { IsBackground = true, Name = "MacWidgets.ShowListener" };
        t.Start();
    }

    public static void ShowMainWindow()
    {
        try
        {
        if (_main == null)
        {
            _main = new MainWindow();
            _main.Closed += (_, _) => _main = null;
        }
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
        _main.Topmost = true;
        _main.Topmost = false;
        _main.Focus();
        }
        catch (Exception ex)
        {
            Store.Log("主界面打开失败：" + ex);
            try { _main?.Close(); } catch { }
            _main = null;
            MessageBox.Show("主界面打开失败，详细原因已写入 log.txt。\n\n" + ex.Message, "MacWidgets", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static void Notify(string text) => Tray?.Balloon(text);

    public static bool Quitting { get; private set; }

    public static void Quit()
    {
        Quitting = true;
        try
        {
            Store.FlushAll();
            Host?.Shutdown();
            Tray?.Dispose();
        }
        catch (Exception ex) { Store.Log("退出：" + ex.Message); }
        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { Store.FlushAll(); } catch { }
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        try { Store.FlushAll(); } catch { }
        base.OnSessionEnding(e);
    }
}
