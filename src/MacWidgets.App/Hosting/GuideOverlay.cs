using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using MacWidgets.Core;
using MacWidgets.Interop;
using static MacWidgets.Interop.Native;
using Shapes = System.Windows.Shapes;

namespace MacWidgets.Hosting;

/// <summary>拖动时的吸附参考线覆盖层：覆盖目标显示器工作区，透明、点击穿透、置顶。</summary>
public sealed class GuideOverlay : Window
{
    private readonly Canvas _canvas = new();
    private IntPtr _hwnd;
    private string? _device;
    // 白色主线 + 深色光晕，浅色壁纸上也能看清
    private static readonly Brush LineBrush = Brushes.White;
    private static readonly Brush HaloBrush = new SolidColorBrush(Color.FromArgb(0xA0, 0x20, 0x20, 0x20));

    public GuideOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        IsHitTestVisible = false;
        Focusable = false;
        Width = 10; Height = 10;
        Left = -32000; Top = -32000;
        Content = _canvas;
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            SetExStyle(_hwnd, (GetExStyle(_hwnd) | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
        };
    }

    public void ShowGuides(MonitorInfo mon, IReadOnlyList<SnapGuide> guides)
    {
        if (!IsVisible) Show();
        if (_device != mon.Device)
        {
            _device = mon.Device;
            for (int i = 0; i < 2; i++)
                SetWindowPos(_hwnd, HWND_TOPMOST, mon.Work.Left, mon.Work.Top, mon.Work.Width, mon.Work.Height, SWP_NOACTIVATE);
        }
        _canvas.Children.Clear();
        foreach (var g in guides)
        {
            void Add(Brush brush, double thickness)
            {
                var line = new Shapes.Line
                {
                    Stroke = brush,
                    StrokeThickness = thickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    SnapsToDevicePixels = true,
                };
                if (g.Vertical) { line.X1 = line.X2 = g.Position; line.Y1 = g.Start; line.Y2 = g.End; }
                else { line.Y1 = line.Y2 = g.Position; line.X1 = g.Start; line.X2 = g.End; }
                _canvas.Children.Add(line);
            }
            Add(HaloBrush, 3.5);
            Add(LineBrush, 1.25);
        }
    }

    public void HideGuides()
    {
        _canvas.Children.Clear();
        if (IsVisible) Hide();
        _device = null;
    }
}
