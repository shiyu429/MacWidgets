using MacWidgets.Core;
using static MacWidgets.Interop.Native;

namespace MacWidgets.Interop;

/// <summary>显示器信息（物理像素）。Scale = 有效 DPI / 96。</summary>
public sealed record MonitorInfo(IntPtr Handle, string Device, RECT Bounds, RECT Work, double Scale, bool Primary)
{
    /// <summary>以工作区左上角为原点的逻辑坐标工作区。</summary>
    public RectD LogicalWork => new(0, 0, Work.Width / Scale, Work.Height / Scale);

    public (double X, double Y) PhysToLogical(double px, double py) => ((px - Work.Left) / Scale, (py - Work.Top) / Scale);

    public (int X, int Y) LogicalToPhys(double lx, double ly) =>
        ((int)Math.Round(Work.Left + lx * Scale), (int)Math.Round(Work.Top + ly * Scale));
}

public static class Monitors
{
    public static List<MonitorInfo> All()
    {
        var list = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref RECT r, IntPtr d) =>
        {
            var mi = Info(h);
            if (mi != null) list.Add(mi);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static MonitorInfo? Info(IntPtr h)
    {
        var info = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>(), szDevice = "" };
        if (!GetMonitorInfo(h, ref info)) return null;
        double scale = 1.0;
        try
        {
            if (GetDpiForMonitor(h, 0 /* MDT_EFFECTIVE_DPI */, out var dx, out _) == 0 && dx > 0) scale = dx / 96.0;
        }
        catch { /* 老系统无 shcore */ }
        return new MonitorInfo(h, info.szDevice ?? "", info.rcMonitor, info.rcWork, scale, (info.dwFlags & MONITORINFOF_PRIMARY) != 0);
    }

    public static MonitorInfo FromPoint(int x, int y)
    {
        var h = MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST);
        return Info(h) ?? Primary();
    }

    public static MonitorInfo FromCursor()
    {
        var p = Native.Cursor();
        return FromPoint(p.X, p.Y);
    }

    public static MonitorInfo Primary()
    {
        var h = MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);
        return Info(h) ?? new MonitorInfo(IntPtr.Zero, "", new RECT { Right = 1920, Bottom = 1080 }, new RECT { Right = 1920, Bottom = 1040 }, 1, true);
    }

    public static MonitorInfo ByDevice(string? device)
    {
        if (!string.IsNullOrEmpty(device))
        {
            var m = All().FirstOrDefault(x => string.Equals(x.Device, device, StringComparison.OrdinalIgnoreCase));
            if (m != null) return m;
        }
        return Primary();
    }
}
