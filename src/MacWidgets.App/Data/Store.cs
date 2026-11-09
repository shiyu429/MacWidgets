using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows.Threading;

namespace MacWidgets.Data;

/// <summary>JSON 持久化：%AppData%\MacWidgets\（layout.json / reminders.json / cache.json / photos\）。</summary>
public static class Store
{
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacWidgets");
    public static readonly string PhotosDir = Path.Combine(Dir, "photos");
    private static string LayoutPath => Path.Combine(Dir, "layout.json");
    private static string RemindersPath => Path.Combine(Dir, "reminders.json");
    private static string CachePath => Path.Combine(Dir, "cache.json");
    public static string LogPath => Path.Combine(Dir, "log.txt");

    public static LayoutFile Layout { get; private set; } = new();
    public static RemindersFile Reminders { get; private set; } = new();
    public static CacheFile Cache { get; private set; } = new();
    public static AppSettings Settings => Layout.Settings;

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private static DispatcherTimer? _layoutTimer, _remTimer, _cacheTimer;

    public static void Load()
    {
        Directory.CreateDirectory(Dir);
        Directory.CreateDirectory(PhotosDir);
        Layout = Read<LayoutFile>(LayoutPath) ?? new LayoutFile();
        Reminders = Read<RemindersFile>(RemindersPath) ?? new RemindersFile();
        Cache = Read<CacheFile>(CachePath) ?? new CacheFile();
        Layout.Settings ??= new AppSettings();
        Layout.Widgets ??= [];
        foreach (var w in Layout.Widgets) w.Options ??= new WidgetOptions();
        // 1.0.8：永久移除嵌入桌面；旧布局里的 Embed 强制回置底，避免 WorkerW 残留导致桌面图标点不了
        if (Layout.Settings.DesktopMode != DesktopMode.Bottom)
        {
            Log("迁移：DesktopMode Embed → Bottom");
            Layout.Settings.DesktopMode = DesktopMode.Bottom;
        }
        // 移除原神活动日历实例（功能已删除）
        int removed = Layout.Widgets.RemoveAll(w => string.Equals(w.Kind.ToString(), "GenshinCalendar", StringComparison.Ordinal));
        // 兼容旧 JSON 枚举名：若反序列化失败会丢弃；再按字符串保险清理一次
        if (removed > 0) Log($"迁移：已移除 {removed} 个原神活动日历组件");
    }

    private static T? Read<T>(string path) where T : class
    {
        foreach (var p in new[] { path, path + ".bak" })
        {
            try
            {
                if (File.Exists(p)) return JsonSerializer.Deserialize<T>(File.ReadAllText(p), Json);
            }
            catch (Exception ex) { Log($"读取 {p} 失败：{ex.Message}"); }
        }
        return null;
    }

    private static void Write<T>(string path, T data)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, Json));
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Move(tmp, path, true);
        }
        catch (Exception ex) { Log($"写入 {path} 失败：{ex.Message}"); }
    }

    private static void Debounce(ref DispatcherTimer? timer, TimeSpan delay, Action action)
    {
        if (timer == null)
        {
            var t = new DispatcherTimer { Interval = delay };
            t.Tick += (_, _) => { t.Stop(); action(); };
            timer = t;
        }
        timer.Stop();
        timer.Start();
    }

    public static void SaveLayoutSoon() => Debounce(ref _layoutTimer, TimeSpan.FromMilliseconds(500), SaveLayoutNow);
    public static void SaveRemindersSoon() => Debounce(ref _remTimer, TimeSpan.FromMilliseconds(500), SaveRemindersNow);
    public static void SaveCacheSoon() => Debounce(ref _cacheTimer, TimeSpan.FromSeconds(5), SaveCacheNow);

    public static void SaveLayoutNow() => Write(LayoutPath, Layout);
    public static void SaveRemindersNow() => Write(RemindersPath, Reminders);
    public static void SaveCacheNow() => Write(CachePath, Cache);

    public static void FlushAll()
    {
        _layoutTimer?.Stop(); _remTimer?.Stop(); _cacheTimer?.Stop();
        SaveLayoutNow(); SaveRemindersNow(); SaveCacheNow();
    }

    private static readonly object _logLock = new();

    public static void Log(string msg)
    {
        try
        {
            lock (_logLock)
            {
                Directory.CreateDirectory(Dir);
                var fi = new FileInfo(LogPath);
                if (fi.Exists && fi.Length > 1_000_000) File.Move(LogPath, LogPath + ".old", true);
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
