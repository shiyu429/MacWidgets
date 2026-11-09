using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace MacWidgets.Widgets;

public static class UiKit
{
    /// <summary>中文/西文字体回退链（不内置苹果字体）。</summary>
    public static readonly FontFamily Text = new("PingFang SC, Segoe UI, Microsoft YaHei UI, Microsoft YaHei");
    public static readonly FontFamily TextLight = new("PingFang SC Light, Segoe UI Light, Segoe UI, Microsoft YaHei UI Light, Microsoft YaHei UI");
    // WPF 不支持可变字体命名实例（Bahnschrift Condensed 可能解析不到），因此优先用系统自带的窄体粗字 Impact
    public static readonly FontFamily Clock = new("Impact, Bahnschrift Condensed, Bahnschrift, Arial Narrow, Segoe UI");

    public static SolidColorBrush B(string hex, double opacity = 1)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        var b = new SolidColorBrush(c) { Opacity = opacity };
        b.Freeze();
        return b;
    }

    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static readonly SolidColorBrush Blue = B("#0088FF");
    public static readonly SolidColorBrush Red = B("#FF383C");
    public static readonly SolidColorBrush UpRed = B("#FC3B30");
    public static readonly SolidColorBrush DownGreen = B("#30D158");
    public static readonly SolidColorBrush Label = B("#1C1C1E");
    public static readonly SolidColorBrush Secondary = B("#8E8E93");
    public static readonly SolidColorBrush CircleGray = B("#C7C7CC");
    public static readonly SolidColorBrush White = B("#FFFFFF");

    public static TextBlock T(string text, double size, FontWeight weight, Brush fg, FontFamily? family = null)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            Foreground = fg,
            FontFamily = family ?? Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(tb, TextRenderingMode.ClearType);
        TextOptions.SetTextHintingMode(tb, TextHintingMode.Fixed);
        return tb;
    }

    /// <summary>
    /// 1.0.8：禁用 DropShadowEffect——透明分层窗口上它会在圆角外产生黑色阴影边。
    /// 保留方法签名以兼容旧调用；组件圆角外现在完全透明，无黑色光晕。
    /// </summary>
    public static DropShadowEffect? Shadow() => null;

    /// <summary>1.0.11 的硬边 OpacityMask（软边失败时的回退）。</summary>
    public static Brush HardSquircleMask(double w, double h, double r)
    {
        var brush = new DrawingBrush(new GeometryDrawing(Brushes.White, null, Squircle(w, h, r)))
        {
            Stretch = Stretch.None,
            Viewbox = new Rect(0, 0, w, h),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, w, h),
            ViewportUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// 软边 OpacityMask：实心 squircle + ~0.85DIP 半透明描边，让圆角外缘有高斯感 AA，
    /// 避免硬裁切在 100%/125%/150% DPI 下的锯齿感。外缘仍落在窗口矩形内（Pad=0 的四角透明区）。
    /// </summary>
    public static Brush SoftSquircleMask(double w, double h, double r, double softDip = 0.85)
    {
        var geo = Squircle(w, h, r);
        var dg = new DrawingGroup();
        dg.Children.Add(new GeometryDrawing(Brushes.White, null, geo));
        var fringeBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
        fringeBrush.Freeze();
        var fringe = new Pen(fringeBrush, Math.Max(0.6, softDip * 2))
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            MiterLimit = 1,
        };
        fringe.Freeze();
        dg.Children.Add(new GeometryDrawing(null, fringe, geo));
        // 更外一圈更淡，再柔一点
        var outerBrush = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));
        outerBrush.Freeze();
        var outer = new Pen(outerBrush, Math.Max(1.0, softDip * 3))
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        outer.Freeze();
        dg.Children.Add(new GeometryDrawing(null, outer, geo));
        dg.Freeze();
        var brush = new DrawingBrush(dg)
        {
            Stretch = Stretch.None,
            Viewbox = new Rect(0, 0, w, h),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, w, h),
            ViewportUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>连续曲率圆角（squircle 近似）：每个角用超椭圆四分之一曲线，过渡区 = 1.45r。</summary>
    public static Geometry Squircle(double w, double h, double r)
    {
        double k = Math.Min(r * 1.45, Math.Min(w, h) / 2);
        const double n = 3.6;
        const int seg = 36;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            // 从左上角曲线的起点 (0,k) 开始，顺时针
            ctx.BeginFigure(new Point(0, k), true, true);
            var pts = new List<Point>(seg * 4 + 8);
            void Corner(double cx, double cy, int sx, int sy, bool reverse)
            {
                // 局部坐标：角点在 (cx,cy)，曲线从 (0,k) 到 (k,0)（相对角点、朝内方向 sx/sy）
                var local = new List<Point>(seg + 1);
                for (int i = 0; i <= seg; i++)
                {
                    double t = Math.PI / 2 * i / seg;
                    double x = k * (1 - Math.Pow(Math.Cos(t), 2.0 / n));
                    double y = k * (1 - Math.Pow(Math.Sin(t), 2.0 / n));
                    local.Add(new Point(cx + sx * x, cy + sy * y));
                }
                if (reverse) local.Reverse();
                pts.AddRange(local);
            }
            Corner(0, 0, 1, 1, false);        // 左上：(0,k) → (k,0)
            Corner(w, 0, -1, 1, true);        // 右上：(w-k,0) → (w,k)
            Corner(w, h, -1, -1, false);      // 右下：(w,h-k) → (w-k,h)
            Corner(0, h, 1, -1, true);        // 左下：(k,h) → (0,h-k)
            ctx.PolyLineTo(pts, true, true);
        }
        g.Freeze();
        return g;
    }

    public static Geometry G(string path)
    {
        var g = Geometry.Parse(path);
        g.Freeze();
        return g;
    }
}

/// <summary>自绘矢量图标（近似 SF Symbols 观感，非苹果素材）。</summary>
public static class Icons
{
    /// <summary>定位箭头（实心导航针，略朝右上），近似 Apple Weather 位置标。</summary>
    public static readonly Geometry LocationArrow = UiKit.G("M 6.5,0 L 0,2.8 L 3.6,3.9 L 4.8,10.5 Z");
    /// <summary>温度计外轮廓，9×16（描边）。</summary>
    public static readonly Geometry Thermometer = UiKit.G("M 2.6,10.6 L 2.6,2.2 A 1.7,1.7 0 0 1 6,2.2 L 6,10.6 A 3.1,3.1 0 1 1 2.6,10.6 Z M 4.3,12.9 L 4.3,6 M 7.6,3 L 9,3 M 7.6,5 L 9,5 M 7.6,7 L 9,7");
    public static readonly Geometry Umbrella = UiKit.G("M 0.5,7 A 7,6 0 0 1 14.5,7 Z M 7.5,7 L 7.5,13 A 1.6,1.6 0 0 1 4.3,13");
    public static readonly Geometry Sun = UiKit.G("M 7,4 A 3,3 0 1 1 6.99,4 Z M 7,0 L 7,1.8 M 7,12.2 L 7,14 M 0,7 L 1.8,7 M 12.2,7 L 14,7 M 2,2 L 3.3,3.3 M 10.7,10.7 L 12,12 M 2,12 L 3.3,10.7 M 10.7,3.3 L 12,2");
    /// <summary>小时钟（提醒时间），12×12。</summary>
    public static readonly Geometry ClockSmall = UiKit.G("M 6,0.75 A 5.25,5.25 0 1 1 5.99,0.75 Z M 6,3 L 6,6.3 L 8.2,7.6");
    public static readonly Geometry TriangleUp = UiKit.G("M 0,8 L 4.6,0 L 9.2,8 Z");
    public static readonly Geometry TriangleDown = UiKit.G("M 0,0 L 9.2,0 L 4.6,8 Z");
    public static readonly Geometry Plus = UiKit.G("M 6,1 L 6,11 M 1,6 L 11,6");
    public static readonly Geometry Minus = UiKit.G("M 1,6 L 11,6");
    /// <summary>日历网格（白色描边 + 圆点），18×16。</summary>
    public static readonly Geometry CalendarFrame = UiKit.G("M 2.5,1.5 L 15.5,1.5 A 2,2 0 0 1 17.5,3.5 L 17.5,13.5 A 2,2 0 0 1 15.5,15.5 L 2.5,15.5 A 2,2 0 0 1 0.5,13.5 L 0.5,3.5 A 2,2 0 0 1 2.5,1.5 Z M 0.5,5 L 17.5,5");
}
