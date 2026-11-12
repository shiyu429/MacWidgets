using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using MacWidgets.Interop;
using MacWidgets.Widgets;

namespace MacWidgets.UI;

/// <summary>一条菜单项。</summary>
public sealed class MenuEntry
{
    public string Header { get; init; } = "";
    public Action? Click { get; init; }
    public bool IsSeparator { get; init; }
    public bool IsCheckable { get; init; }
    public bool IsChecked { get; init; }
    public bool IsEnabled { get; init; } = true;
    public List<MenuEntry>? Children { get; init; }

    public static MenuEntry Sep() => new() { IsSeparator = true };
    public static MenuEntry Item(string header, Action click) => new() { Header = header, Click = click };
    public static MenuEntry Check(string header, bool on, Action click) =>
        new() { Header = header, IsCheckable = true, IsChecked = on, Click = click };
}

/// <summary>
/// 现代浅色右键菜单（1.0.8 完全重写）：
/// 不透明白色卡片 + 细边框 + 圆角 + 悬停高亮，无液态玻璃、无 Acrylic、无全局鼠标钩。
/// 鼠标左键抬起、TouchUp、StylusUp 均可触发；外部点击通过 Deactivated 关闭。
/// </summary>
public sealed class PopupMenu : Window
{
    private static PopupMenu? _open;
    private readonly List<MenuEntry> _entries;
    private PopupMenu? _sub;
    private bool _dismissing;

    private static readonly Brush CardBg = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFC));
    private static readonly Brush CardBorder = new SolidColorBrush(Color.FromRgb(0xE3, 0xE3, 0xE8));
    private static readonly Brush RowHover = new SolidColorBrush(Color.FromRgb(0xEB, 0xEB, 0xF0));
    private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(0x1D, 0x1D, 0x1F));
    private static readonly Brush SepBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xED));

    private PopupMenu(List<MenuEntry> entries)
    {
        _entries = entries;
        Title = "MacWidgetsMenu";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Topmost = true;
        ShowActivated = true;
        Focusable = true;

        // 窗口外围不预留阴影空间、不绘制任何底板，只显示圆角卡片。
        var root = new Grid { Margin = new Thickness(0), Background = Brushes.Transparent };
        var card = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = CardBorder,
            Background = CardBg,
            Padding = new Thickness(5),
            MinWidth = 180,
        };
        var sp = new StackPanel { Background = Brushes.Transparent };
        foreach (var e in entries) sp.Children.Add(BuildRow(e));
        card.Child = sp;
        root.Children.Add(card);
        Content = root;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; DismissAll(); }
        };
        Deactivated += (_, _) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_sub is { IsVisible: true }) return;
                if (IsActive) return;
                if (_sub != null)
                {
                    var fg = Native.GetForegroundWindow();
                    var sh = new WindowInteropHelper(_sub).Handle;
                    if (fg == sh) return;
                }
                Dismiss();
            });
        };
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var ex = Native.GetExStyle(hwnd);
            Native.SetExStyle(hwnd, (ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST) & ~Native.WS_EX_APPWINDOW);
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_FRAMECHANGED);
            Native.RemovePopupBorder(hwnd);
        };
    }

    private UIElement BuildRow(MenuEntry e)
    {
        if (e.IsSeparator)
        {
            return new Border
            {
                Height = 1,
                Margin = new Thickness(4, 4, 4, 4),
                Background = SepBrush,
            };
        }

        var row = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 4, 10, 4),
            MinHeight = 27,
            Background = Brushes.Transparent,
            Cursor = e.IsEnabled ? Cursors.Hand : Cursors.Arrow,
            Opacity = e.IsEnabled ? 1 : 0.4,
            Tag = e,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (e.IsCheckable && e.IsChecked)
        {
            var check = new System.Windows.Shapes.Path
            {
                // 1.0.24：几何整体内移 1.5px 并放大容器（10×8 → 14×12），避免圆头笔触在左/下/右侧被裁掉
                Data = Geometry.Parse("M 1.5,6 L 5,9.5 L 11.5,2.5"),
                Stroke = LabelBrush,
                StrokeThickness = 1.6,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Width = 14,
                Height = 12,
                ClipToBounds = false,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };
            grid.Children.Add(check);
        }

        var label = new TextBlock
        {
            Text = e.Header, FontSize = 13,
            FontFamily = UiKit.Text,
            Foreground = LabelBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 8, 0),
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        if (e.Children is { Count: > 0 })
        {
            var chev = new TextBlock
            {
                Text = "›", FontSize = 15, Foreground = LabelBrush,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(chev, 2);
            grid.Children.Add(chev);
        }

        row.Child = grid;
        if (!e.IsEnabled) return row;

        void Highlight(bool on) => row.Background = on ? RowHover : Brushes.Transparent;

        row.MouseEnter += (_, _) =>
        {
            Highlight(true);
            if (e.Children is { Count: > 0 })
            {
                var pt = row.PointToScreen(new Point(row.ActualWidth - 4, 0));
                OpenSub(e.Children, pt);
            }
            else _sub?.Dismiss();
        };
        row.MouseLeave += (_, _) => Highlight(false);

        // 鼠标：抬起触发（手指触摸会产生提升鼠标事件，用 _touchHandled 去重）
        row.MouseLeftButtonUp += (_, ev) =>
        {
            if (row.Tag is MenuEntry entry && ReferenceEquals(entry, e))
            {
                if (_touchHandledRows.Contains(row)) return;
                ev.Handled = true;
                InvokeRow(e);
            }
        };

        // 触摸：捕获 + 抬起触发，明确 Handled 抑制提升鼠标
        row.PreviewTouchDown += (_, ev) =>
        {
            try { row.CaptureTouch(ev.TouchDevice); } catch { }
            ev.Handled = true;
        };
        row.TouchUp += (_, ev) =>
        {
            _touchHandledRows.Add(row);
            Dispatcher.BeginInvoke(() => _touchHandledRows.Remove(row));
            ev.Handled = true;
            try { row.ReleaseTouchCapture(ev.TouchDevice); } catch { }
            InvokeRow(e);
        };
        row.LostTouchCapture += (_, _) => { /* 释放即可，不触发 */ };

        // 触控笔
        row.PreviewStylusUp += (_, ev) =>
        {
            if (ev.StylusDevice.TabletDevice.Type == TabletDeviceType.Touch) return;
            ev.Handled = true;
            InvokeRow(e);
        };
        return row;
    }

    private static readonly HashSet<Border> _touchHandledRows = [];

    private void InvokeRow(MenuEntry entry)
    {
        if (!entry.IsEnabled || entry.Children is { Count: > 0 }) return;
        var act = entry.Click;
        DismissAll();
        try { act?.Invoke(); }
        catch (Exception ex) { MacWidgets.Data.Store.Log("菜单命令失败：" + ex); }
    }

    private void OpenSub(List<MenuEntry> children, Point screenPixels)
    {
        _sub?.Dismiss();
        var keep = _open;
        var sub = new PopupMenu(children);
        _sub = sub;
        sub.Left = -10000;
        sub.Top = -10000;
        sub.Show();
        sub.UpdateLayout();
        var hwnd = new WindowInteropHelper(sub).Handle;
        double scale = 1.0;
        try
        {
            uint dpi = Native.GetDpiForWindow(hwnd);
            if (dpi > 0) scale = dpi / 96.0;
        }
        catch { }
        double px = screenPixels.X, py = screenPixels.Y;
        double wPx = sub.ActualWidth * scale, hPx = sub.ActualHeight * scale;
        var mon = Native.MonitorFromPoint(new Native.POINT { X = (int)px, Y = (int)py }, Native.MONITOR_DEFAULTTONEAREST);
        var info = new Native.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFOEX>() };
        if (mon != IntPtr.Zero && Native.GetMonitorInfo(mon, ref info))
        {
            if (px + wPx > info.rcWork.Right) px = info.rcWork.Right - wPx - 4;
            if (py + hPx > info.rcWork.Bottom) py = info.rcWork.Bottom - hPx - 4;
            if (px < info.rcWork.Left) px = info.rcWork.Left + 4;
            if (py < info.rcWork.Top) py = info.rcWork.Top + 4;
        }
        sub.Left = px / scale;
        sub.Top = py / scale;
        _open = keep;
    }

    public void Dismiss()
    {
        if (_dismissing) return;
        _dismissing = true;
        try
        {
            _sub?.Close();
            _sub = null;
            if (IsVisible) Close();
        }
        catch { }
        if (_open == this) _open = null;
    }

    public static void DismissAll()
    {
        var m = _open;
        _open = null;
        m?.Dismiss();
    }

    public static void Show(IEnumerable<MenuEntry> entries, Point screenPixels)
    {
        DismissAll();
        var list = entries.Where(e => e != null).ToList();
        if (list.Count == 0) return;
        var menu = new PopupMenu(list);
        _open = menu;

        menu.Left = -10000;
        menu.Top = -10000;
        menu.Show();
        menu.UpdateLayout();

        var hwnd = new WindowInteropHelper(menu).Handle;
        double scale = 1.0;
        try
        {
            uint dpi = Native.GetDpiForWindow(hwnd);
            if (dpi > 0) scale = dpi / 96.0;
        }
        catch { scale = 1.0; }

        double px = screenPixels.X, py = screenPixels.Y;
        if (double.IsNaN(px) || double.IsNaN(py))
        {
            Native.GetCursorPos(out var cp);
            px = cp.X; py = cp.Y;
        }

        double wPx = menu.ActualWidth * scale, hPx = menu.ActualHeight * scale;
        var mon = Native.MonitorFromPoint(new Native.POINT { X = (int)px, Y = (int)py }, Native.MONITOR_DEFAULTTONEAREST);
        var info = new Native.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFOEX>() };
        int left = 0, top = 0, right = (int)SystemParameters.PrimaryScreenWidth, bottom = (int)SystemParameters.PrimaryScreenHeight;
        if (mon != IntPtr.Zero && Native.GetMonitorInfo(mon, ref info))
        {
            left = info.rcWork.Left; top = info.rcWork.Top;
            right = info.rcWork.Right; bottom = info.rcWork.Bottom;
        }
        if (px + wPx > right) px = right - wPx - 4;
        if (py + hPx > bottom) py = bottom - hPx - 4;
        if (px < left) px = left + 4;
        if (py < top) py = top + 4;

        menu.Left = px / scale;
        menu.Top = py / scale;
        try { menu.Activate(); menu.Focus(); } catch { }
    }

    public static void ShowAt(FrameworkElement anchor, IEnumerable<MenuEntry> entries, MouseButtonEventArgs? e = null)
    {
        Point sp;
        if (e != null)
        {
            try { sp = anchor.PointToScreen(e.GetPosition(anchor)); }
            catch { Native.GetCursorPos(out var p); sp = new Point(p.X, p.Y); }
        }
        else
        {
            Native.GetCursorPos(out var p);
            sp = new Point(p.X, p.Y);
        }
        Show(entries, sp);
    }
}
