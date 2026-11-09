using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MacWidgets.Core;
using MacWidgets.Data;
using MacWidgets.Interop;
using MacWidgets.Widgets;
using static MacWidgets.Interop.Native;

namespace MacWidgets.Hosting;

/// <summary>桌面层管理器：创建/摆放组件窗口、桌面拖动循环、吸附、Z 序、显示器变化。</summary>
public sealed class WidgetHost
{
    private sealed class DragSession
    {
        public required WidgetWindow W;
        public double Ax, Ay;
        public POINT Start;
        public MonitorInfo? Mon;
        public double Lx, Ly;
        public string? OrigMonitor;
        public double OrigX, OrigY;
        public int Axis; // 0 未锁定，1 仅水平，2 仅垂直
        public double StartLx, StartLy;
        public POINT Last;
    }

    public List<WidgetWindow> Windows { get; } = [];
    public bool RaisedForDesktop { get; private set; }
    public event Action? Changed;

    private DragSession? _drag;
    private GuideOverlay? _overlay;
    private HwndSource? _msgWindow;
    private uint _taskbarCreated;
    private DispatcherTimer? _displayTimer;
    public void Start()
    {
        _overlay = new GuideOverlay();

        if (!Store.Layout.Initialized)
        {
            Store.Layout.Initialized = true;
            CreateDefaultLayout();
        }

        foreach (var inst in Store.Layout.Widgets.ToList())
        {
            try { CreateWindow(inst); }
            catch (Exception ex) { Store.Log($"创建组件 {inst.Kind} 失败：{ex}"); }
        }

        // 1.0.8：前台变成 WorkerW/Progman（Win+D / 显示桌面）时重新把组件置底并确保可见，
        // 不再把组件提到普通窗口之上，也绝不 SetParent 进 WorkerW。
        DesktopLayer.DesktopForegroundChanged += desk =>
        {
            RaisedForDesktop = false;
            if (desk)
            {
                foreach (var w in Windows)
                {
                    w.EnsureVisibleOnDesktop();
                    w.ApplyZ();
                }
            }
        };
        DesktopLayer.Start();

        // 隐藏的消息窗口：显示器变化、资源管理器重启
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        var p = new HwndSourceParameters("MacWidgetsMessageWindow") { Width = 0, Height = 0, WindowStyle = unchecked((int)0x80000000) /* WS_POPUP */ };
        _msgWindow = new HwndSource(p);
        _msgWindow.AddHook(MessageHook);
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Application.Current?.Dispatcher.BeginInvoke(ScheduleRelayout);

        ApplyDesktopMode();
        Store.SaveLayoutSoon();
    }

    private IntPtr MessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DISPLAYCHANGE || (msg == WM_SETTINGCHANGE && wParam.ToInt64() == 0x2F /* SPI_SETWORKAREA */))
            ScheduleRelayout();
        else if (_taskbarCreated != 0 && msg == (int)_taskbarCreated)
        {
            // 资源管理器重启：重新挂接嵌入模式、恢复 Z 序
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                ApplyDesktopMode();
                App.Tray?.Recreate();
            }, DispatcherPriority.Background);
        }
        return IntPtr.Zero;
    }

    private void ScheduleRelayout()
    {
        _displayTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _displayTimer.Stop();
        _displayTimer.Tick -= OnDisplayTimer;
        _displayTimer.Tick += OnDisplayTimer;
        _displayTimer.Start();
    }

    private void OnDisplayTimer(object? sender, EventArgs e)
    {
        _displayTimer!.Stop();
        foreach (var w in Windows) Place(w);
    }

    public void NotifyChanged() => Changed?.Invoke();

    // ---------- 创建 / 摆放 ----------
    public WidgetWindow CreateWindow(WidgetInstance inst)
    {
        var w = new WidgetWindow(this, inst);
        try
        {
            w.Show();
            Windows.Add(w);
            Place(w);
            w.ApplyZ();
        }
        catch
        {
            Windows.Remove(w);
            try { w.Close(); } catch { }
            throw;
        }
        return w;
    }

    /// <summary>按实例保存的位置摆放窗口（显示器不存在时回退主屏并保证完全可见）。</summary>
    public void Place(WidgetWindow w)
    {
        var inst = w.Instance;
        var mon = Monitors.ByDevice(inst.Monitor);
        var size = w.LogicalSize;
        var r = SnapEngine.ClampInside(new RectD(inst.X, inst.Y, size.Width, size.Height), mon.LogicalWork);
        inst.X = r.X;
        inst.Y = r.Y;
        inst.Monitor = mon.Device;
        w.PlaceInner(mon, r.X, r.Y);
        // WPF 处理跨屏 DPI 变化后可能会按建议矩形微调位置，空闲时再校正一次
        w.Dispatcher.BeginInvoke(() => w.PlaceInner(Monitors.ByDevice(inst.Monitor), inst.X, inst.Y), DispatcherPriority.ApplicationIdle);
    }

    public WidgetInstance NewInstance(WidgetKind kind, WidgetSize? size = null)
    {
        var inst = new WidgetInstance { Kind = kind, Size = size ?? Catalog.DefaultSize(kind) };
        if (!Catalog.Sizes(kind).Contains(inst.Size)) inst.Size = Catalog.DefaultSize(kind);
        return inst;
    }

    /// <summary>直接添加到主屏空位（主界面“添加”按钮/托盘菜单）。</summary>
    public WidgetWindow AddWidget(WidgetKind kind, WidgetSize? size = null)
    {
        var inst = NewInstance(kind, size);
        var mon = Monitors.Primary();
        inst.Monitor = mon.Device;
        var d = Catalog.DesignSize(inst.Size);
        double s = Store.Settings.GlobalScale;
        var r = new RectD(32, 32, d.Width * s, d.Height * s);
        r = SnapEngine.ResolveOverlap(r, mon.LogicalWork, OtherRects(mon, null), SnapOpts(), 0.0);
        inst.X = r.X; inst.Y = r.Y;
        Store.Layout.Widgets.Add(inst);
        WidgetWindow w;
        try { w = CreateWindow(inst); }
        catch
        {
            // 1.0.13：创建失败时回滚，避免布局里留下“幽灵”实例（下次启动重复出现）
            Store.Layout.Widgets.Remove(inst);
            throw;
        }
        Store.SaveLayoutSoon();
        Changed?.Invoke();
        // 1.0.12：添加到桌面不再闪烁定位动画
        return w;
    }

    private void CreateDefaultLayout()
    {
        var mon = Monitors.Primary();
        foreach (var k in Catalog.Kinds)
        {
            var inst = NewInstance(k);
            var (x, y) = Catalog.ReferenceSlot(k);
            inst.Monitor = mon.Device;
            inst.X = 32 + x;
            inst.Y = 32 + y;
            Store.Layout.Widgets.Add(inst);
        }
        // 示例提醒（与参考图一致）
        if (Store.Reminders.Items.Count == 0)
        {
            ReminderStore.Add("你好", DateTime.Today.AddHours(23), true);
        }
    }

    /// <summary>按参考图重新排列（每种组件的第一个放到参考位置，其余找空位）。</summary>
    public void ArrangeLikeReference()
    {
        var mon = Monitors.Primary();
        var used = new HashSet<WidgetKind>();
        var placed = new List<RectD>();
        foreach (var w in Windows)
        {
            var inst = w.Instance;
            var size = w.LogicalSize;
            RectD r;
            if (used.Add(inst.Kind))
            {
                var (x, y) = Catalog.ReferenceSlot(inst.Kind);
                double s = Store.Settings.GlobalScale;
                r = new RectD(32 + x * s, 32 + y * s, size.Width, size.Height);
            }
            else
            {
                r = SnapEngine.ResolveOverlap(new RectD(32, 32, size.Width, size.Height), mon.LogicalWork, placed, SnapOpts(), 0.0);
            }
            placed.Add(r);
            inst.Monitor = mon.Device;
            inst.X = r.X; inst.Y = r.Y;
            Place(w);
        }
        Store.SaveLayoutSoon();
        Changed?.Invoke();
    }

    public void Remove(WidgetWindow w)
    {
        if (_drag?.W == w) _drag = null;
        Windows.Remove(w);
        Store.Layout.Widgets.Remove(w.Instance);
        w.Close();
        try
        {
            var dir = Path.Combine(Store.PhotosDir, w.Instance.Id);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch { }
        Store.SaveLayoutSoon();
        Changed?.Invoke();
    }

    public void ResetPosition(WidgetWindow w)
    {
        var mon = Monitors.Primary();
        var size = w.LogicalSize;
        var r = SnapEngine.ResolveOverlap(new RectD(32, 32, size.Width, size.Height), mon.LogicalWork, OtherRects(mon, w), SnapOpts(), 0.0);
        w.Instance.Monitor = mon.Device;
        w.Instance.X = r.X; w.Instance.Y = r.Y;
        Place(w);
        w.Flash();
        Store.SaveLayoutSoon();
    }

    public void SetLayoutLocked(bool locked)
    {
        Store.Settings.LayoutLocked = locked;
        if (locked && IsDragging) EndDrag(false);
        Store.SaveLayoutSoon();
        Changed?.Invoke();
    }

    /// <summary>全局设置变化后刷新所有组件。</summary>
    public void ApplySettings()
    {
        foreach (var w in Windows)
        {
            w.ApplyScale();
            w.View.Refresh();
        }
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            foreach (var w in Windows) { Place(w); w.ApplyZ(); }
        }, DispatcherPriority.Loaded);
        Store.SaveLayoutSoon();
    }

    public void ApplyDesktopMode()
    {
        // 1.0.8：仅保留置底模式，绝不嵌入 WorkerW。
        Store.Settings.DesktopMode = DesktopMode.Bottom;
        RaisedForDesktop = false;
        foreach (var w in Windows) { w.EnsureVisibleOnDesktop(); Place(w); w.ApplyZ(); }
    }

    // ---------- 吸附 ----------
    private static SnapOptions SnapOpts() => new()
    {
        Enabled = Store.Settings.SnapEnabled,
        Grid = Store.Settings.GridEnabled,
        GridSize = Math.Max(4, Store.Settings.GridSize),
        Threshold = 8,
        Gap = 16,
        Margin = 16,
    };

    /// <summary>同一显示器上其他组件的逻辑矩形。</summary>
    private List<RectD> OtherRects(MonitorInfo mon, WidgetWindow? except)
    {
        var list = new List<RectD>();
        foreach (var w in Windows)
        {
            if (w == except) continue;
            if (!string.Equals(Monitors.ByDevice(w.Instance.Monitor).Device, mon.Device, StringComparison.OrdinalIgnoreCase)) continue;
            var s = w.LogicalSize;
            list.Add(new RectD(w.Instance.X, w.Instance.Y, s.Width, s.Height));
        }
        return list;
    }

    // ---------- 拖动循环（仅桌面已有组件；组件库一键添加走 AddWidget） ----------
    public void BeginDrag(WidgetWindow w, double ax, double ay)
    {
        if (w.Instance.Locked || Store.Settings.LayoutLocked) return;
        var start = Native.Cursor();
        var mon = Monitors.FromPoint(start.X, start.Y);
        _drag = new DragSession
        {
            W = w, Ax = ax, Ay = ay, Start = start, Last = start,
            OrigMonitor = w.Instance.Monitor, OrigX = w.Instance.X, OrigY = w.Instance.Y,
            Mon = mon,
        };
        var (cx, cy) = mon.PhysToLogical(start.X, start.Y);
        var size = w.LogicalSize;
        _drag.StartLx = cx - ax * size.Width;
        _drag.StartLy = cy - ay * size.Height;
        w.Dragging = true;
        w.ApplyZ();
    }

    public bool IsDragging => _drag != null;
    public bool IsDraggingWindow(WidgetWindow w) => _drag?.W == w;

    public void UpdateDrag(POINT physicalPoint)
    {
        var s = _drag;
        if (s == null) return;
        if (s.W.Instance.Locked || Store.Settings.LayoutLocked) { EndDrag(false); return; }
        s.Last = physicalPoint;
        MoveDrag(s, physicalPoint);
    }

    public void UpdateDrag()
    {
        var s = _drag;
        if (s == null) return;
        if (s.W.Instance.Locked || Store.Settings.LayoutLocked) { EndDrag(false); return; }
        if (KeyDown(VK_ESCAPE)) { EndDrag(true); return; }
        var pt = Native.Cursor();
        s.Last = pt;
        MoveDrag(s, pt);
    }

    private void MoveDrag(DragSession s, POINT pt)
    {
        var mon = Monitors.FromPoint(pt.X, pt.Y);
        var (cx, cy) = mon.PhysToLogical(pt.X, pt.Y);
        var size = s.W.LogicalSize;
        double rx = cx - s.Ax * size.Width;
        double ry = cy - s.Ay * size.Height;
        if (KeyDown(VK_SHIFT) && s.Mon != null && s.Mon.Device == mon.Device)
        {
            if (s.Axis == 0)
            {
                int dx = Math.Abs(pt.X - s.Start.X), dy = Math.Abs(pt.Y - s.Start.Y);
                if (dx > 6 || dy > 6) s.Axis = dx >= dy ? 1 : 2;
            }
            if (s.Axis == 1) ry = s.StartLy;
            else if (s.Axis == 2) rx = s.StartLx;
        }
        else s.Axis = 0;
        var opts = SnapOpts();
        if (KeyDown(VK_MENU)) opts.Enabled = false;
        var res = SnapEngine.Snap(new RectD(rx, ry, size.Width, size.Height), mon.LogicalWork, OtherRects(mon, s.W), opts);
        if (opts.Enabled && res.Guides.Count > 0) _overlay?.ShowGuides(mon, res.Guides);
        else _overlay?.HideGuides();
        s.W.PlaceInner(mon, res.Rect.X, res.Rect.Y);
        s.Mon = mon;
        s.Lx = res.Rect.X;
        s.Ly = res.Rect.Y;
    }

    public void EndDrag(bool cancel)
    {
        var s = _drag;
        if (s == null) return;
        _drag = null;
        _overlay?.HideGuides();
        var w = s.W;
        w.Dragging = false;
        w.Opacity = 1;
        if (w.IsMouseCaptured) w.ReleaseMouseCapture();

        if (cancel || s.Mon == null)
        {
            w.Instance.Monitor = s.OrigMonitor;
            w.Instance.X = s.OrigX;
            w.Instance.Y = s.OrigY;
            Place(w);
            w.ApplyZ();
            return;
        }

        var mon = s.Mon!;
        var size = w.LogicalSize;
        var r = new RectD(s.Lx, s.Ly, size.Width, size.Height);
        if (Store.Settings.AvoidOverlap)
            r = SnapEngine.ResolveOverlap(r, mon.LogicalWork, OtherRects(mon, w), SnapOpts());
        w.Instance.Monitor = mon.Device;
        w.Instance.X = r.X;
        w.Instance.Y = r.Y;
        Place(w);
        w.ApplyZ();
        Store.SaveLayoutSoon();
        Changed?.Invoke();
    }

    public void Shutdown()
    {
        DesktopLayer.Stop();
        foreach (var w in Windows.ToList())
        {
            try { w.Close(); } catch { }
        }
        _overlay?.Close();
        _msgWindow?.Dispose();
    }
}
