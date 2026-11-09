using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MacWidgets.Widgets;

/// <summary>提醒事项勾选圆圈：空心 → 点击填充蓝色 + 白勾（200ms 动画）。</summary>
public sealed class CheckCircle : FrameworkElement
{
    private static readonly Pen Ring = MakePen(UiKit.CircleGray, 1.5);
    private static readonly Pen CheckPen = MakePen(Brushes.White, 1.8);
    private readonly ScaleTransform _scale = new(1, 1);

    public bool IsChecked { get; private set; }
    public event Action? Checked;

    public CheckCircle(double size = 19)
    {
        Width = Height = size;
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = false;
        UseLayoutRounding = false;
        RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = _scale;
    }

    private static Pen MakePen(Brush b, double t)
    {
        var p = new Pen(b, t) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        p.Freeze();
        return p;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (IsChecked) return;
        IsChecked = true;
        InvalidateVisual();
        var anim = new DoubleAnimation(0.75, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut } };
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        Checked?.Invoke();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double s = Math.Min(ActualWidth, ActualHeight);
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        // 透明命中区域
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (!IsChecked)
        {
            dc.DrawEllipse(null, Ring, c, s / 2 - 0.75, s / 2 - 0.75);
        }
        else
        {
            dc.DrawEllipse(UiKit.Blue, null, c, s / 2, s / 2);
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(c.X - s * 0.22, c.Y + s * 0.01), false, false);
                ctx.LineTo(new Point(c.X - s * 0.06, c.Y + s * 0.17), true, true);
                ctx.LineTo(new Point(c.X + s * 0.24, c.Y - s * 0.16), true, true);
            }
            g.Freeze();
            dc.DrawGeometry(null, CheckPen, g);
        }
    }
}

/// <summary>迷你分时走势线：X 轴按交易分钟数固定比例，未走完部分留空；昨收虚线基准；线下渐变填充。</summary>
public sealed class Sparkline : FrameworkElement
{
    private double[] _points = [];
    private int _expected = 242;
    private double _prev = double.NaN;
    private Color _color = Colors.Red;

    public void SetData(double[]? points, int expected, double prevClose, Color color)
    {
        _points = points ?? [];
        _expected = Math.Max(expected, _points.Length);
        _prev = prevClose;
        _color = color;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        if (_points.Length < 2)
        {
            if (!double.IsNaN(_prev))
            {
                var dpen = new Pen(new SolidColorBrush(Color.FromArgb(110, 0x8E, 0x8E, 0x93)), 1) { DashStyle = new DashStyle([2, 2], 0) };
                dc.DrawLine(dpen, new Point(0, h / 2), new Point(w, h / 2));
            }
            return;
        }
        double min = _points.Min(), max = _points.Max();
        if (!double.IsNaN(_prev) && _prev > 0) { min = Math.Min(min, _prev); max = Math.Max(max, _prev); }
        double range = max - min;
        if (range <= 0) range = Math.Max(1e-6, Math.Abs(max) * 0.001);
        const double pad = 1.5;
        double Y(double v) => pad + (h - 2 * pad) * (1 - (v - min) / range);
        double X(int i) => w * i / Math.Max(1, _expected - 1);

        if (!double.IsNaN(_prev) && _prev > 0)
        {
            var dpen = new Pen(new SolidColorBrush(Color.FromArgb(120, 0x8E, 0x8E, 0x93)), 1) { DashStyle = new DashStyle([1.5, 2], 0) };
            dpen.Freeze();
            double y = Math.Round(Y(_prev)) + 0.5;
            dc.DrawLine(dpen, new Point(0, y), new Point(w, y));
        }

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(new Point(X(0), Y(_points[0])), false, false);
            var pts = new List<Point>(_points.Length);
            for (int i = 1; i < _points.Length; i++) pts.Add(new Point(X(i), Y(_points[i])));
            ctx.PolyLineTo(pts, true, true);
        }
        line.Freeze();

        double baseY = !double.IsNaN(_prev) && _prev > 0 ? Y(_prev) : h;
        var fill = new StreamGeometry();
        using (var ctx = fill.Open())
        {
            ctx.BeginFigure(new Point(X(0), baseY), true, true);
            var pts = new List<Point>(_points.Length + 2);
            for (int i = 0; i < _points.Length; i++) pts.Add(new Point(X(i), Y(_points[i])));
            pts.Add(new Point(X(_points.Length - 1), baseY));
            ctx.PolyLineTo(pts, false, true);
        }
        fill.Freeze();
        var grad = new LinearGradientBrush(Color.FromArgb(70, _color.R, _color.G, _color.B), Color.FromArgb(0, _color.R, _color.G, _color.B), 90);
        grad.Freeze();
        dc.DrawGeometry(grad, null, fill);

        var pen = new Pen(new SolidColorBrush(_color), 1.5) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, line);
    }
}

/// <summary>“照片”占位图标：白底浅灰渐变圆角方块 + 8 瓣彩色花朵（自绘近似）。</summary>
public sealed class FlowerIcon : FrameworkElement
{
    private static readonly string[] PetalColors =
        ["#FF9F0A", "#FFD60A", "#8CD941", "#34C759", "#30B0C7", "#3A82F7", "#AF52DE", "#FF375F"];

    public FlowerIcon(double size = 58)
    {
        Width = Height = size;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double s = ActualWidth;
        var bg = new LinearGradientBrush(Colors.White, UiKit.C("#EDEDF0"), 90);
        bg.Freeze();
        var sq = UiKit.Squircle(s, s, s * 0.225);
        var edge = new Pen(UiKit.B("#000000", 0.08), 0.6);
        edge.Freeze();
        dc.DrawGeometry(bg, edge, sq);

        var c = new Point(s / 2, s / 2);
        double pw = s * 0.205, ph = s * 0.30, dist = s * 0.155;
        for (int i = 0; i < 8; i++)
        {
            double ang = i * 45; // 0 = 正上方
            var col = UiKit.C(PetalColors[i]);
            var brush = new RadialGradientBrush(Color.FromArgb(235, col.R, col.G, col.B), Color.FromArgb(190, col.R, col.G, col.B))
            {
                GradientOrigin = new Point(0.5, 0.85),
                Center = new Point(0.5, 0.7),
            };
            brush.Freeze();
            dc.PushTransform(new RotateTransform(ang, c.X, c.Y));
            dc.DrawEllipse(brush, null, new Point(c.X, c.Y - dist), pw / 2, ph / 2);
            dc.Pop();
        }
    }
}

/// <summary>时钟面：60 根刻度（彗尾走秒）+ 窄体粗数字（按参考比例拉伸适配）。</summary>
public sealed class ClockFace : FrameworkElement
{
    private const int Levels = 64;
    private IReadOnlyList<MacWidgets.Core.Tick> _ticks = [];
    private Pen[] _pens = [];
    private Size _lastSize;
    private Geometry? _text;
    private string _textKey = "";
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public bool Hour24 { get; set; } = true;
    public bool Sweep { get; set; } = true;
    public bool Smooth { get; set; } = true;

    public ClockFace()
    {
        // 曲线/刻度走抗锯齿，禁止像素吸附造成锯齿环
        SnapsToDevicePixels = false;
        UseLayoutRounding = false;
        RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        _timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => InvalidateVisual();
        IsVisibleChanged += (_, _) => UpdateTimer();
        Loaded += (_, _) => UpdateTimer();
        Unloaded += (_, _) => _timer.Stop();
    }

    public void UpdateTimer()
    {
        // 平滑开：约 30fps 连续重绘；平滑关：每秒跳一格即可
        _timer.Interval = Sweep && Smooth ? TimeSpan.FromMilliseconds(33) : TimeSpan.FromMilliseconds(200);
        if (IsVisible && IsLoaded) _timer.Start(); else _timer.Stop();
        InvalidateVisual();
    }

    private void EnsureGeometry(Size size)
    {
        if (size == _lastSize && _ticks.Count > 0) return;
        _lastSize = size;
        // 以短边为基准，保证非正方形量测时仍是正圆，不被 LayoutTransform / 非等比拉伸扭曲
        double side = Math.Min(size.Width, size.Height);
        double k = side / 170.0;
        double inset = 9.5 * k;
        double len = 6.3 * k; // 1.0.18：刻度略加长
        // 圆角矩形路径：半径贴近卡片 CardRadius@170，刻度贴边且四角不空
        double corner = WidgetView.CardRadius * k * 0.88;
        _ticks = MacWidgets.Core.ClockGeometry.ComputeTicks(side, side, inset, len, corner);
        if (Math.Abs(size.Width - side) > 0.01 || Math.Abs(size.Height - side) > 0.01)
        {
            double ox = (size.Width - side) / 2, oy = (size.Height - side) / 2;
            _ticks = _ticks.Select(t => new MacWidgets.Core.Tick(
                new MacWidgets.Core.PointD(t.Outer.X + ox, t.Outer.Y + oy),
                new MacWidgets.Core.PointD(t.Inner.X + ox, t.Inner.Y + oy))).ToList();
        }
        _pens = new Pen[Levels];
        for (int i = 0; i < Levels; i++)
        {
            byte a = (byte)Math.Round(255.0 * i / (Levels - 1));
            var p = new Pen(new SolidColorBrush(Color.FromArgb(a, 0x0C, 0x0C, 0x0E)), 1.95 * k) // 1.0.18：秒针色更深、刻度略粗
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            p.Freeze();
            _pens[i] = p;
        }
        _textKey = "";
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = new Size(ActualWidth, ActualHeight);
        if (size.Width <= 0 || size.Height <= 0) return;
        EnsureGeometry(size);
        var now = DateTime.Now;

        // Smooth 开启时用小数秒驱动彗尾连续滑动；关闭时取整秒，视觉上一秒跳一格
        double sec = Smooth
            ? now.Second + now.Millisecond / 1000.0
            : now.Second;
        for (int i = 0; i < _ticks.Count; i++)
        {
            double alpha = Sweep
                ? MacWidgets.Core.ClockGeometry.TickAlpha(i, sec, _ticks.Count, Smooth)
                : 0.55;
            var pen = _pens[(int)Math.Round(Math.Clamp(alpha, 0, 1) * (Levels - 1))];
            var t = _ticks[i];
            dc.DrawLine(pen, new Point(t.Outer.X, t.Outer.Y), new Point(t.Inner.X, t.Inner.Y));
        }

        int hour = Hour24 ? now.Hour : (now.Hour % 12 == 0 ? 12 : now.Hour % 12);
        string text = $"{hour:00}:{now.Minute:00}";
        if (!Hour24 && hour < 10) text = $"{hour}:{now.Minute:00}";
        if (text != _textKey || _text == null)
        {
            _textKey = text;
            _text = BuildText(text, size);
        }
        dc.DrawGeometry(Brushes.Black, null, _text);
    }

    private Geometry BuildText(string text, Size size)
    {
        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var tf = new Typeface(UiKit.Clock, FontStyles.Normal, FontWeights.Bold, FontStretches.Condensed);
        FormattedText Make(string s) => new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 100, Brushes.Black, ppd);

        // 以 "00:00" 的墨迹边界确定缩放：数字高 ≈ 37% 组件高，整体宽 ≈ 76% 组件宽
        var refGeo = Make("00:00").BuildGeometry(new Point(0, 0));
        var rb = refGeo.Bounds;
        double targetW = size.Width * 0.77, targetH = size.Height * 0.37;
        double sx = rb.Width > 0 ? targetW / rb.Width : 1;
        double sy = rb.Height > 0 ? targetH / rb.Height : 1;

        var geo = Make(text).BuildGeometry(new Point(0, 0));
        var b = geo.Bounds;
        if (b.IsEmpty) return Geometry.Empty;
        double ox = size.Width / 2 - (b.X + b.Width / 2) * sx;
        double oy = size.Height / 2 - (rb.Y + rb.Height / 2) * sy;
        var g = geo.Clone();
        g.Transform = new MatrixTransform(sx, 0, 0, sy, ox, oy);
        g.Freeze();
        return g;
    }
}
