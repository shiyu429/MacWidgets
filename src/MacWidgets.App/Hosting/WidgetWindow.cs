using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MacWidgets.Data;
using MacWidgets.Interop;
using MacWidgets.UI;
using MacWidgets.Widgets;
using static MacWidgets.Interop.Native;

namespace MacWidgets.Hosting;

/// <summary>
/// 每个桌面组件一个无边框透明窗口：WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE，
/// 永远置底（HWND_BOTTOM），拖动时也绝不置顶（避免股票等组件卡在最上方）。
/// 1.0.8 起完全移除嵌入桌面（SetParent WorkerW）。
/// </summary>
public sealed class WidgetWindow : Window, IWidgetContext
{
    /// <summary>无阴影、无额外底板：窗口与卡片边缘完全重合。</summary>
    public const double Pad = 0;

    private readonly WidgetHost _host;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly Grid _inner = new() { Background = Brushes.Transparent };

    public WidgetInstance Instance { get; }
    public WidgetView View { get; private set; } = null!;
    public IntPtr Hwnd { get; private set; }

    public bool Dragging { get; set; }
    private bool _textInput;
    private bool _flash;

    private bool _pending;
    private POINT _downPt;
    private int _touchId = -1;
    private bool _touchDragging;
    private POINT _lastTouchPoint;
    private System.Windows.Threading.DispatcherTimer? _longPressTimer;
    private bool _longPressFired;

    public WidgetWindow(WidgetHost host, WidgetInstance inst)
    {
        _host = host;
        Instance = inst;
        Title = "MacWidgets · " + Catalog.Name(inst.Kind);
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        // 必须保持完全透明；任何非透明 Background 都会在圆角外露出矩形底板。
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -32000;
        Top = -32000;
        UseLayoutRounding = false;
        SnapsToDevicePixels = false;
        RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        Topmost = false;

        var outer = new Grid { Margin = new Thickness(Pad), LayoutTransform = _scale, Background = Brushes.Transparent };
        RenderOptions.SetEdgeMode(outer, EdgeMode.Unspecified);
        RenderOptions.SetBitmapScalingMode(outer, BitmapScalingMode.HighQuality);
        outer.Children.Add(_inner);
        Content = outer;

        Rebuild();
        ApplyScale();

        SourceInitialized += OnSourceInitialized;
        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        LostMouseCapture += (_, _) =>
        {
            if (Dragging) _host.EndDrag(false);
            _pending = false;
        };

        Stylus.SetIsPressAndHoldEnabled(this, false);
        Stylus.SetIsFlicksEnabled(this, false);
        IsManipulationEnabled = false;
        PreviewTouchDown += OnTouchDown;
        PreviewTouchMove += OnTouchMove;
        PreviewTouchUp += OnTouchUp;
        LostTouchCapture += (_, e) =>
        {
            if (e.TouchDevice.Id == _touchId)
            {
                CancelLongPress();
                if (_touchDragging) { try { _host.EndDrag(false); } catch { } }
                _touchId = -1;
                _touchDragging = false;
                _pending = false;
            }
        };
        Deactivated += (_, _) =>
        {
            if (_textInput)
            {
                Keyboard.ClearFocus();
                if (_textInput) EndTextInput();
            }
        };

        MouseRightButtonUp += OnContextMenu;
        DragOver += (_, e) =>
        {
            e.Effects = View.AcceptsFileDrop && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        Drop += (_, e) =>
        {
            if (View.AcceptsFileDrop && e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                View.OnFileDropped(files[0]);
        };
    }

    public double EffectiveScale => Math.Clamp(Store.Settings.GlobalScale, 0.5, 3);

    public Size LogicalSize
    {
        get
        {
            var d = Catalog.DesignSize(Instance.Size);
            return new Size(d.Width * EffectiveScale, d.Height * EffectiveScale);
        }
    }

    public void Rebuild()
    {
        _inner.Children.Clear();
        View = WidgetFactory.Create(Instance, false);
        View.Context = this;
        try { _inner.Children.Add(new WidgetChrome(View)); }
        catch (Exception ex)
        {
            // 1.0.13：外观层失败时退化为无 chrome 的裸视图（硬裁切），组件仍可用
            Store.Log($"WidgetChrome 创建失败 {Instance.Kind}，退化显示：{ex}");
            _inner.Children.Clear();
            try
            {
                var d = Catalog.DesignSize(Instance.Size);
                View.Width = d.Width; View.Height = d.Height;
                View.Clip = UiKit.Squircle(d.Width, d.Height, WidgetView.CardRadius);
            }
            catch { }
            _inner.Children.Add(View);
        }
        Instance.Size = Catalog.DefaultSize(Instance.Kind);
        Instance.Scale = 1;
        AllowDrop = View.AcceptsFileDrop;
    }

    public void ApplyScale()
    {
        _scale.ScaleX = _scale.ScaleY = EffectiveScale;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        Hwnd = new WindowInteropHelper(this).Handle;
        var ex = GetExStyle(Hwnd);
        // 绝不保留 WS_EX_TOPMOST；工具窗 + 不激活
        ex = (ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW & ~WS_EX_TOPMOST;
        SetExStyle(Hwnd, ex);
        Topmost = false;
        // 再次强制透明（防止主题/DWM 在句柄创建后改写背景）
        Background = Brushes.Transparent;
        RemovePopupBorder(Hwnd);
        HwndSource.FromHwnd(Hwnd)?.AddHook(WndProc);
        ApplyZ();
    }

    private bool AllowAbove => _textInput || _flash;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGING && lParam != IntPtr.Zero)
        {
            var wp = System.Runtime.InteropServices.Marshal.PtrToStructure<WINDOWPOS>(lParam);
            bool changed = false;
            // 防止 Win+D / Show Desktop 把组件藏起来
            if ((wp.flags & SWP_HIDEWINDOW) != 0)
            {
                wp.flags = (wp.flags & ~SWP_HIDEWINDOW) | SWP_SHOWWINDOW;
                changed = true;
            }
            // 非输入/闪烁时强制 HWND_BOTTOM，并清掉 TOPMOST
            if (!AllowAbove && !Dragging)
            {
                if ((wp.flags & SWP_NOZORDER) == 0 && wp.hwndInsertAfter != HWND_BOTTOM)
                {
                    wp.hwndInsertAfter = HWND_BOTTOM;
                    changed = true;
                }
            }
            else if (Dragging)
            {
                if ((wp.flags & SWP_NOZORDER) == 0 &&
                    (wp.hwndInsertAfter == HWND_TOPMOST || wp.hwndInsertAfter == HWND_BOTTOM))
                {
                    // 桌面拖动：普通顶层，禁止 TOPMOST / BOTTOM
                    wp.hwndInsertAfter = HWND_TOP;
                    changed = true;
                }
            }
            if (changed)
                System.Runtime.InteropServices.Marshal.StructureToPtr(wp, lParam, false);
        }
        else if (msg == WM_MOUSEACTIVATE && !_textInput)
        {
            handled = true;
            return new IntPtr(MA_NOACTIVATE);
        }
        return IntPtr.Zero;
    }

    /// <summary>按当前状态调整 Z 序。组件永远不置顶。</summary>
    public void ApplyZ()
    {
        if (Hwnd == IntPtr.Zero) return;
        Topmost = false;
        // 清掉可能残留的 TOPMOST 扩展样式
        var ex = GetExStyle(Hwnd);
        if ((ex & WS_EX_TOPMOST) != 0)
            SetExStyle(Hwnd, ex & ~WS_EX_TOPMOST);

        if (Dragging)
        {
            // 桌面内拖动：普通顶层，绝不长期 TOPMOST（曾导致股票卡在最上方）
            if ((GetExStyle(Hwnd) & WS_EX_TOPMOST) != 0)
                SetExStyle(Hwnd, GetExStyle(Hwnd) & ~WS_EX_TOPMOST);
            SetWindowPos(Hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            return;
        }
        if (AllowAbove)
        {
            SetWindowPos(Hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            return;
        }
        // 先取消可能的置顶，再沉底
        SetWindowPos(Hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        SetWindowPos(Hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    /// <summary>Win+D 后确保组件仍可见且在桌面底部。</summary>
    public void EnsureVisibleOnDesktop()
    {
        if (Hwnd == IntPtr.Zero) return;
        try
        {
            // 若曾被错误 SetParent 到桌面层，解绑回顶层弹窗
            SetParent(Hwnd, IntPtr.Zero);
            var style = GetStyle(Hwnd);
            if ((style & WS_CHILD) != 0)
                SetStyle(Hwnd, (style & ~WS_CHILD) | WS_POPUP);
            Show();
            SetWindowPos(Hwnd, HWND_BOTTOM, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
        }
        catch (Exception ex) { Store.Log("EnsureVisible: " + ex.Message); }
    }

    public void PlaceInner(MonitorInfo mon, double lx, double ly)
    {
        if (Hwnd == IntPtr.Zero) return;
        var (px, py) = mon.LogicalToPhys(lx - Pad, ly - Pad);
        SetWindowPos(Hwnd, IntPtr.Zero, px, py, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    public RECT InnerPhysicalRect()
    {
        GetWindowRect(Hwnd, out var r);
        double s = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int pad = (int)Math.Round(Pad * s);
        return new RECT { Left = r.Left + pad, Top = r.Top + pad, Right = r.Right - pad, Bottom = r.Bottom - pad };
    }

    public void BeginTextInput()
    {
        if (Hwnd == IntPtr.Zero) return;
        _textInput = true;
        SetExStyle(Hwnd, GetExStyle(Hwnd) & ~WS_EX_NOACTIVATE);
        ApplyZ();
        Activate();
        SetForegroundWindow(Hwnd);
    }

    public void EndTextInput()
    {
        if (!_textInput || Hwnd == IntPtr.Zero) return;
        _textInput = false;
        SetExStyle(Hwnd, GetExStyle(Hwnd) | WS_EX_NOACTIVATE);
        ApplyZ();
    }

    public void SaveInstance()
    {
        Store.SaveLayoutSoon();
        _host.NotifyChanged();
    }

    public void OpenEditor() => EditWidgetDialog.Open(_host, this);

    public void Flash()
    {
        _flash = true;
        ApplyZ();
        var anim = new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(380)) { AutoReverse = false, RepeatBehavior = new RepeatBehavior(3) };
        anim.Completed += (_, _) =>
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            t.Tick += (_, _) => { t.Stop(); _flash = false; ApplyZ(); };
            t.Start();
        };
        BeginAnimation(OpacityProperty, anim);
    }

    private List<MenuEntry> BuildMenu()
    {
        var items = new List<MenuEntry> { MenuEntry.Item("编辑小组件…", OpenEditor) };
        items.AddRange(View.MenuItems());
        items.Add(MenuEntry.Sep());
        items.Add(MenuEntry.Check("锁定位置", Instance.Locked, () => { Instance.Locked = !Instance.Locked; SaveInstance(); }));
        items.Add(MenuEntry.Check("锁定所有布局", Store.Settings.LayoutLocked, () => _host.SetLayoutLocked(!Store.Settings.LayoutLocked)));
        items.Add(MenuEntry.Sep());
        items.Add(MenuEntry.Item("打开主界面", App.ShowMainWindow));
        items.Add(MenuEntry.Item("移除小组件", () => _host.Remove(this)));
        return items;
    }

    private void OnContextMenu(object sender, MouseButtonEventArgs e)
    {
        if (e.StylusDevice is { TabletDevice.Type: TabletDeviceType.Touch }) return;
        e.Handled = true;
        PopupMenu.ShowAt(this, BuildMenu(), e);
    }

    private void OpenContextMenuAt(POINT pt)
    {
        CancelLongPress();
        PopupMenu.Show(BuildMenu(), new Point(pt.X, pt.Y));
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        if (Instance.Locked || Store.Settings.LayoutLocked) return;
        _pending = true;
        _downPt = Native.Cursor();
        CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (Dragging)
        {
            _host.UpdateDrag();
            return;
        }
        if (!_pending || e.LeftButton != MouseButtonState.Pressed) return;
        var p = Native.Cursor();
        double s = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double th = SystemParameters.MinimumHorizontalDragDistance * s;
        if (Math.Abs(p.X - _downPt.X) < th && Math.Abs(p.Y - _downPt.Y) < th) return;
        var r = InnerPhysicalRect();
        double ax = r.Width > 0 ? (double)(_downPt.X - r.Left) / r.Width : 0.5;
        double ay = r.Height > 0 ? (double)(_downPt.Y - r.Top) / r.Height : 0.5;
        _host.BeginDrag(this, Math.Clamp(ax, 0, 1), Math.Clamp(ay, 0, 1));
        _host.UpdateDrag();
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        bool wasDragging = Dragging;
        _pending = false;
        if (wasDragging) _host.EndDrag(false);
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (wasDragging) e.Handled = true;
    }

    private void OnTouchDown(object? sender, TouchEventArgs e)
    {
        if (_touchId >= 0) return;
        _touchId = e.TouchDevice.Id;
        _touchDragging = false;
        _longPressFired = false;
        _pending = true;
        var screen = PointToScreen(e.GetTouchPoint(this).Position);
        _downPt = _lastTouchPoint = new POINT { X = (int)Math.Round(screen.X), Y = (int)Math.Round(screen.Y) };
        CaptureTouch(e.TouchDevice);
        StartLongPress();
    }

    private void StartLongPress()
    {
        CancelLongPress();
        _longPressTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
        _longPressTimer.Tick += (_, _) =>
        {
            CancelLongPress();
            if (!_pending || _touchDragging || _longPressFired) return;
            _longPressFired = true;
            _pending = false;
            // 松开手指后再弹菜单更利于触摸命中：这里立即弹，菜单自身处理触摸
            OpenContextMenuAt(_lastTouchPoint);
        };
        _longPressTimer.Start();
    }

    private void CancelLongPress()
    {
        if (_longPressTimer == null) return;
        _longPressTimer.Stop();
        _longPressTimer = null;
    }

    private void OnTouchMove(object? sender, TouchEventArgs e)
    {
        if (e.TouchDevice.Id != _touchId) return;
        var screen = PointToScreen(e.GetTouchPoint(this).Position);
        var pt = new POINT { X = (int)Math.Round(screen.X), Y = (int)Math.Round(screen.Y) };
        _lastTouchPoint = pt;
        double dx = pt.X - _downPt.X, dy = pt.Y - _downPt.Y;
        if (dx * dx + dy * dy > 100) CancelLongPress();
        if (!_touchDragging && _pending && !_longPressFired && dx * dx + dy * dy >= 100 &&
            !Instance.Locked && !Store.Settings.LayoutLocked)
        {
            var r = InnerPhysicalRect();
            _host.BeginDrag(this,
                Math.Clamp(r.Width > 0 ? (double)(_downPt.X - r.Left) / r.Width : .5, 0, 1),
                Math.Clamp(r.Height > 0 ? (double)(_downPt.Y - r.Top) / r.Height : .5, 0, 1));
            _touchDragging = Dragging;
        }
        if (_touchDragging)
        {
            _host.UpdateDrag(pt);
            e.Handled = true;
        }
    }

    private void OnTouchUp(object? sender, TouchEventArgs e)
    {
        if (e.TouchDevice.Id != _touchId) return;
        CancelLongPress();
        _touchId = -1;
        _pending = false;
        if (_touchDragging) { _host.EndDrag(false); e.Handled = true; }
        _touchDragging = false;
        try { ReleaseTouchCapture(e.TouchDevice); } catch { }
    }
}
