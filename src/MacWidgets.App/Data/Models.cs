using System.Text.Json.Serialization;
using MacWidgets.Core.Stocks;
using MacWidgets.Core.Weather;

namespace MacWidgets.Data;

[JsonConverter(typeof(JsonStringEnumConverter<WidgetKind>))]
public enum WidgetKind { Clock, Weather, Reminders, Stocks, Photo, PhotoWide, Plan, Notes, GenshinCalendar }

[JsonConverter(typeof(JsonStringEnumConverter<WidgetSize>))]
public enum WidgetSize { S, M, L }

[JsonConverter(typeof(JsonStringEnumConverter<DesktopMode>))]
public enum DesktopMode { Bottom, Embed }

[JsonConverter(typeof(JsonStringEnumConverter<ReminderListMode>))]
public enum ReminderListMode { Today, All }

public sealed class AppSettings
{
    public bool Autostart { get; set; }
    public bool SnapEnabled { get; set; } = true;
    public bool GridEnabled { get; set; } = true;
    public int GridSize { get; set; } = 16;
    public double GlobalScale { get; set; } = 1.0;
    public bool RedUp { get; set; } = true;
    public bool Fahrenheit { get; set; }
    public bool ClockSmooth { get; set; } = true;
    public DesktopMode DesktopMode { get; set; } = DesktopMode.Bottom;
    public int StockRefreshSeconds { get; set; } = 15;
    public bool AvoidOverlap { get; set; } = true;
    public bool ShowOnDesktopClick { get; set; } = true;
    public bool LayoutLocked { get; set; }

}

public sealed class WidgetOptions
{
    // 时钟
    public bool Clock24 { get; set; } = true;
    public bool ClockSweep { get; set; } = true;
    // 天气
    public bool WeatherAuto { get; set; } = true;
    public string? City { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    // 股票
    public List<string> Symbols { get; set; } = [.. MacWidgets.Core.Stocks.Symbols.Defaults];
    // 照片
    public string? PhotoFile { get; set; }
    public double PhotoZoom { get; set; } = 1.0;
    public double PhotoOffsetX { get; set; }
    public double PhotoOffsetY { get; set; }
    public int PhotoRotation { get; set; }
    public bool PhotoFit { get; set; }
    // 备忘录
    public string NoteText { get; set; } = "";
    // 提醒事项
    public ReminderListMode ListMode { get; set; } = ReminderListMode.Today;
}

public sealed class WidgetInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public WidgetKind Kind { get; set; }
    public WidgetSize Size { get; set; } = WidgetSize.S;
    public double Scale { get; set; } = 1.0;
    /// <summary>显示器设备名（\\.\DISPLAY1）。</summary>
    public string? Monitor { get; set; }
    /// <summary>组件左上角相对该显示器工作区左上角的逻辑坐标（DIP）。</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public bool Locked { get; set; }
    public WidgetOptions Options { get; set; } = new();
}

public sealed class LayoutFile
{
    public int Version { get; set; } = 1;
    public bool Initialized { get; set; }
    public AppSettings Settings { get; set; } = new();
    public List<WidgetInstance> Widgets { get; set; } = [];
}

public sealed class ReminderItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    /// <summary>本地时间；HasTime=false 时只看日期部分。</summary>
    public DateTime? Due { get; set; }
    public bool HasTime { get; set; }
    public bool Completed { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    /// <summary>1.0.24：首次启动预置的示例提醒（「今天」里的「你好」），只在「今天」组件显示，不进「计划」等待办组件。</summary>
    public bool IsSample { get; set; }
}

public sealed class RemindersFile
{
    public int Version { get; set; } = 1;
    public List<ReminderItem> Items { get; set; } = [];
}

public sealed class WeatherCacheEntry
{
    public string? Name { get; set; }
    public WeatherData? Data { get; set; }
}

public sealed class CacheFile
{
    public Dictionary<string, Quote> Quotes { get; set; } = [];
    public Dictionary<string, Intraday> Intraday { get; set; } = [];
    public DateTime? QuotesAt { get; set; }
    public Dictionary<string, WeatherCacheEntry> Weather { get; set; } = [];
}
