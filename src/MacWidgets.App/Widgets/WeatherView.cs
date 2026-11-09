using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MacWidgets.Core;
using MacWidgets.Data;
using MacWidgets.Services;
using MacWidgets.UI;
using Shapes = System.Windows.Shapes;

namespace MacWidgets.Widgets;

/// <summary>天气：还原 v1.0.0 图标与版式；仅温度数字加大加粗。底色随天气/昼夜变化（晴天对齐参考图竖向蓝渐变）。</summary>
public sealed class WeatherView : WidgetView
{
    private Brush _bg;
    private readonly TextBlock _place, _temp, _hint;
    private readonly Shapes.Path _icon;
    private readonly StackPanel _forecast = new();

    public override Brush BackgroundBrush => _bg;
    public override bool Outline => false;

    public WeatherView(WidgetInstance inst, bool preview) : base(inst, preview)
    {
        _bg = BackgroundFor(0, isDay: true); // 默认晴天参考渐变，数据到达后 SyncChrome
        var root = new Grid { Margin = new Thickness(16, 13, 16, 15) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (inst.Size == WidgetSize.M) root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });

        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var top = new StackPanel { Orientation = Orientation.Horizontal };
        _place = UiKit.T("—", 15, FontWeights.SemiBold, UiKit.White);
        _place.MaxWidth = 120;
        top.Children.Add(_place);
        left.Children.Add(top);

        // v1.0.0 是 44 Light；按要求只加大加粗温度
        _temp = UiKit.T("--°", 52, FontWeights.Bold, UiKit.White, UiKit.Text);
        _temp.Margin = new Thickness(-1, -8, 0, -2);
        Grid.SetRow(_temp, 1);
        left.Children.Add(_temp);

        _icon = new Shapes.Path
        {
            Data = Icons.Thermometer,
            Stroke = UiKit.White,
            StrokeThickness = 1.3,
            Width = 9,
            Height = 15,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(1, 0, 0, 5),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        Grid.SetRow(_icon, 3);
        left.Children.Add(_icon);

        _hint = UiKit.T("正在获取天气…", 13, FontWeights.SemiBold, UiKit.White);
        _hint.TextWrapping = TextWrapping.Wrap;
        _hint.LineHeight = 16.5;
        _hint.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _hint.MaxHeight = 33.5;
        Grid.SetRow(_hint, 4);
        left.Children.Add(_hint);
        root.Children.Add(left);

        if (inst.Size == WidgetSize.M)
        {
            _forecast.Margin = new Thickness(16, 4, 0, 0);
            _forecast.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_forecast, 1);
            root.Children.Add(_forecast);
        }
        Content = root;
        ToolTipService.SetInitialShowDelay(this, 800);
    }

    protected override void OnAttach()
    {
        WeatherHub.Updated += Update;
        WeatherHub.Subscribe(this, Instance.Options);
        Update();
    }

    protected override void OnDetach()
    {
        WeatherHub.Updated -= Update;
        WeatherHub.Unsubscribe(this);
    }

    public override void Refresh()
    {
        WeatherHub.Unsubscribe(this);
        WeatherHub.Subscribe(this, Instance.Options);
        Update();
    }

    public override IEnumerable<MenuEntry> MenuItems() =>
        [MenuEntry.Item("立即刷新天气", WeatherHub.RefreshNow)];

    private void Update()
    {
        var snap = WeatherHub.Get(this);
        bool f = Store.Settings.Fahrenheit;
        _place.Text = snap.Name;
        var d = snap.Data;
        if (d == null)
        {
            _temp.Text = "--°";
            _hint.Text = snap.Error != null ? "天气获取失败，稍后重试" : "正在获取天气…";
            _icon.Data = Icons.Thermometer;
            _icon.Width = 9;
            _icon.Height = 15;
            ToolTip = snap.Error;
            ApplyBackground(0, isDay: true);
            return;
        }
        _temp.Text = $"{Math.Round(WeatherText.ToUnit(d.CurrentTemp, f))}°";
        var (kind, text) = WeatherText.Current(d.CurrentCode);
        _hint.Text = text;
        ApplyBackground(d.CurrentCode, d.IsDay);
        (_icon.Data, _icon.Width, _icon.Height) = kind == HintKind.Rain
            ? (Icons.Umbrella, 15.0, 14.0)
            : (Icons.Sun, 14.0, 14.0);
        ToolTip = $"{WeatherText.Describe(d.CurrentCode)} · 更新于 {d.FetchedAt:HH:mm}" +
                  (snap.Error != null ? $"\n最近一次刷新失败：{snap.Error}" : "") +
                  "\n数据来源：Open-Meteo";

        if (Instance.Size == WidgetSize.M)
        {
            _forecast.Children.Clear();
            foreach (var day in d.Daily.Take(4))
            {
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                string label = day.Date.Date == DateTime.Today ? "今天"
                    : day.Date.Date == DateTime.Today.AddDays(1) ? "明天"
                    : "周" + "日一二三四五六"[(int)day.Date.DayOfWeek];
                var a = UiKit.T(label, 13, FontWeights.SemiBold, UiKit.White);
                var b = UiKit.T(WeatherText.Describe(day.Code), 12, FontWeights.Normal, UiKit.B("#FFFFFF", 0.75));
                var c = UiKit.T($"{Math.Round(WeatherText.ToUnit(day.Min, f))}° / {Math.Round(WeatherText.ToUnit(day.Max, f))}°", 13, FontWeights.SemiBold, UiKit.White);
                Grid.SetColumn(b, 1);
                Grid.SetColumn(c, 2);
                row.Children.Add(a);
                row.Children.Add(b);
                row.Children.Add(c);
                _forecast.Children.Add(row);
            }
        }
    }

    private void ApplyBackground(int wmo, bool isDay)
    {
        var next = BackgroundFor(wmo, isDay);
        if (!ReferenceEquals(_bg, next))
        {
            _bg = next;
            SyncChrome();
        }
    }

    /// <summary>
    /// 竖向渐变底色：白天晴/局部多云用更明亮的晴天蓝（相对参考采样提亮 mid/bottom，避免读成夜间）；
    /// 夜间晴用更深靛蓝，与白天明显区分。其它天气各有独立色板。不改布局/字体/图标。
    /// </summary>
    public static Brush BackgroundFor(int wmo, bool isDay)
    {
        var (top, bottom) = GradientColors(wmo, isDay);
        var brush = new LinearGradientBrush(top, bottom, 90);
        brush.Freeze();
        return brush;
    }

    public static (Color Top, Color Bottom) GradientColors(int wmo, bool isDay)
    {
        // 白天色板（晴天相对参考 #1E4E8C→#4A7FB7 整体提亮，读出「白天」）
        (string t, string b) day = wmo switch
        {
            0 or 1 => ("#2E7CC6", "#7EB8E8"),           // 晴 / 大部晴朗 — 明亮白天
            2 => ("#2E7CC6", "#7EB8E8"),                 // 局部多云 — 同白天晴
            3 => ("#5A6B7C", "#8FA0B0"),                 // 阴 — 冷灰蓝（1.0.19 指定）
            45 or 48 => ("#6A7580", "#8A959E"),          // 雾
            >= 51 and <= 67 or >= 80 and <= 82 => ("#2C3E55", "#4A6480"), // 雨 — 深蓝灰湿感（1.0.19 指定）
            >= 71 and <= 77 or 85 or 86 => ("#7A9BB8", "#D0E4F2"),        // 雪 — 最亮冷白蓝（1.0.19 指定）
            >= 95 and <= 99 => ("#2A3548", "#3A4A60"),   // 雷暴
            _ => ("#2E7CC6", "#7EB8E8"),
        };
        if (isDay) return (UiKit.C(day.t), UiKit.C(day.b));

        // 夜间：明显压暗，与白天晴区分
        (string t, string b) night = wmo switch
        {
            0 or 1 or 2 => ("#0A1528", "#152A48"),
            3 => ("#2A343C", "#3A4854"),                 // 阴夜 — 白天灰蓝压暗
            45 or 48 => ("#1E242C", "#2E343C"),
            >= 51 and <= 67 or >= 80 and <= 82 => ("#141E2A", "#243848"), // 雨夜 — 深蓝灰压暗
            >= 71 and <= 77 or 85 or 86 => ("#3A4E5C", "#5A7088"),        // 雪夜 — 冷蓝压暗
            >= 95 and <= 99 => ("#060A12", "#101820"),
            _ => ("#0A1528", "#152A48"),
        };
        return (UiKit.C(night.t), UiKit.C(night.b));
    }
}
