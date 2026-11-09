using System.IO;
using System.Windows.Threading;
using MacWidgets.Core.Genshin;
using MacWidgets.Data;

namespace MacWidgets.Services;

public sealed record GenshinCalendarSnapshot(string? ImagePath, string? ImageUrl, string? Error, bool Loading, DateTime? FetchedAt);

/// <summary>原神活动日历：从官网解析日历大图，缓存到 AppData，默认每 6 小时刷新。</summary>
public static class GenshinCalendarHub
{
    public static event Action? Updated;

    private static readonly object Gate = new();
    private static DispatcherTimer? _timer;
    private static bool _busy;
    private static string? _path;
    private static string? _url;
    private static string? _error;
    private static DateTime? _fetchedAt;
    private static bool _loading;

    public static string CacheDir => Path.Combine(Store.Dir, "genshin");
    public static string CacheFile => Path.Combine(CacheDir, "calendar-panel.png");
    public static string MetaFile => Path.Combine(CacheDir, "calendar-meta.txt");

    public static void Start()
    {
        Directory.CreateDirectory(CacheDir);
        if (File.Exists(CacheFile))
        {
            _path = CacheFile;
            try
            {
                if (File.Exists(MetaFile))
                {
                    var lines = File.ReadAllLines(MetaFile);
                    if (lines.Length > 0) _url = lines[0];
                    if (lines.Length > 1 && DateTime.TryParse(lines[1], out var dt)) _fetchedAt = dt;
                }
            }
            catch { /* ignore */ }
        }
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _timer.Tick += (_, _) => _ = TickAsync(force: false);
        _timer.Start();
        _ = TickAsync(force: false);
    }

    public static GenshinCalendarSnapshot Get() =>
        new(_path != null && File.Exists(_path) ? _path : null, _url, _error, _loading, _fetchedAt);

    public static void RefreshNow() => _ = TickAsync(force: true);

    private static async Task TickAsync(bool force)
    {
        lock (Gate)
        {
            if (_busy) return;
            if (!force && _fetchedAt is DateTime t && DateTime.Now - t < TimeSpan.FromHours(6) && File.Exists(CacheFile))
                return;
            _busy = true;
            _loading = true;
        }
        Updated?.Invoke();
        try
        {
            var result = await GenshinCalendarClient.FetchAsync().ConfigureAwait(true);
            Directory.CreateDirectory(CacheDir);
            var tmp = CacheFile + ".tmp";
            await File.WriteAllBytesAsync(tmp, result.Bytes).ConfigureAwait(true);
            File.Move(tmp, CacheFile, true);
            await File.WriteAllTextAsync(MetaFile, result.ImageUrl + "\n" + DateTime.Now.ToString("o") + "\n" + result.LandingUrl)
                .ConfigureAwait(true);
            _path = CacheFile;
            _url = result.ImageUrl;
            _fetchedAt = DateTime.Now;
            _error = null;
            Store.Log("Genshin calendar OK: " + result.ImageUrl);
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            Store.Log("Genshin calendar: " + ex.Message);
            if (_path == null && File.Exists(CacheFile)) _path = CacheFile;
        }
        finally
        {
            lock (Gate) { _busy = false; _loading = false; }
            Updated?.Invoke();
        }
    }
}
