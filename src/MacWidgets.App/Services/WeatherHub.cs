using System.Globalization;
using System.Windows.Threading;
using MacWidgets.Core.Weather;
using MacWidgets.Data;

namespace MacWidgets.Services;

public sealed record WeatherSnapshot(string Name, WeatherData? Data, bool Auto, string? Error, bool Loading);

/// <summary>天气服务：按地点聚合订阅，Open-Meteo 每 30 分钟刷新；自动定位 = ipwho.is + Nominatim 反查区县名。</summary>
public static class WeatherHub
{
    public static event Action? Updated;

    private sealed class Entry
    {
        public string Name = "";
        public double Lat, Lon;
        public bool Auto;
        public WeatherData? Data;
        public string? Error;
        public DateTime NextFetch = DateTime.MinValue;
        public bool Busy;
    }

    private static readonly Dictionary<string, Entry> _entries = [];
    private static readonly Dictionary<object, string> _subs = [];
    private static DispatcherTimer? _timer;
    private static IpLocation? _ipLoc;
    private static string? _ipName;
    private static Task? _locating;

    public static void Start()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += (_, e) =>
        {
            if (!e.IsAvailable) return;
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                foreach (var en in _entries.Values) if (en.Error != null) en.NextFetch = DateTime.MinValue;
            });
        };
    }

    private static string KeyFor(WidgetOptions o) =>
        o.WeatherAuto || o.Lat is null || o.Lon is null
            ? "auto"
            : string.Create(CultureInfo.InvariantCulture, $"{o.Lat:F3},{o.Lon:F3}");

    public static void Subscribe(object owner, WidgetOptions o)
    {
        var key = KeyFor(o);
        _subs[owner] = key;
        if (!_entries.TryGetValue(key, out var e))
        {
            e = new Entry { Auto = key == "auto" };
            if (!e.Auto) { e.Lat = o.Lat!.Value; e.Lon = o.Lon!.Value; e.Name = o.City ?? ""; }
            if (Store.Cache.Weather.TryGetValue(key, out var c))
            {
                e.Data = c.Data;
                if (string.IsNullOrEmpty(e.Name)) e.Name = c.Name ?? "";
            }
            _entries[key] = e;
        }
        else if (!e.Auto && !string.IsNullOrEmpty(o.City))
        {
            e.Name = o.City!;
        }
        _ = TickAsync();
    }

    public static void Unsubscribe(object owner) => _subs.Remove(owner);

    public static void RefreshNow()
    {
        foreach (var e in _entries.Values) e.NextFetch = DateTime.MinValue;
        _ = TickAsync();
    }

    public static WeatherSnapshot Get(object owner)
    {
        if (!_subs.TryGetValue(owner, out var key) || !_entries.TryGetValue(key, out var e))
            return new WeatherSnapshot("—", null, true, null, true);
        return new WeatherSnapshot(string.IsNullOrEmpty(e.Name) ? (e.Auto ? "定位中…" : "—") : e.Name, e.Data, e.Auto, e.Error, e.Busy);
    }

    private static async Task TickAsync()
    {
        foreach (var (key, e) in _entries.ToList())
        {
            if (!_subs.ContainsValue(key)) continue;
            if (e.Busy || DateTime.Now < e.NextFetch) continue;
            e.Busy = true;
            try
            {
                if (e.Auto)
                {
                    await EnsureIpLocationAsync();
                    if (_ipLoc == null) throw new InvalidOperationException("自动定位失败，请在编辑小组件中手动选择城市");
                    e.Lat = _ipLoc.Lat; e.Lon = _ipLoc.Lon;
                    e.Name = _ipName ?? _ipLoc.City ?? "当前位置";
                }
                e.Data = await WeatherClient.ForecastAsync(e.Lat, e.Lon);
                e.Error = null;
                e.NextFetch = DateTime.Now.AddMinutes(30);
                Store.Cache.Weather[key] = new WeatherCacheEntry { Name = e.Name, Data = e.Data };
                Store.SaveCacheSoon();
            }
            catch (Exception ex)
            {
                e.Error = ex.Message;
                e.NextFetch = DateTime.Now.AddMinutes(e.Data == null ? 1 : 5);
                Store.Log("Weather: " + ex.Message);
            }
            finally
            {
                e.Busy = false;
            }
            Updated?.Invoke();
        }
    }

    private static Task EnsureIpLocationAsync()
    {
        if (_ipLoc != null) return Task.CompletedTask;
        return _locating ??= LocateAsync();
    }

    private static async Task LocateAsync()
    {
        try
        {
            _ipLoc = await WeatherClient.IpLocateAsync();
            try { _ipName = await WeatherClient.ReverseAsync(_ipLoc.Lat, _ipLoc.Lon); }
            catch (Exception ex) { Store.Log("Reverse geocode: " + ex.Message); }
        }
        catch (Exception ex)
        {
            Store.Log("IP locate: " + ex.Message);
        }
        finally
        {
            _locating = null;
        }
    }
}
