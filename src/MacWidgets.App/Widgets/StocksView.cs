using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MacWidgets.Core.Stocks;
using MacWidgets.Data;
using MacWidgets.Services;
using MacWidgets.UI;
using Shapes = System.Windows.Shapes;

namespace MacWidgets.Widgets;

/// <summary>股票（中号 3 行 / 大号 7 行）：三角 + 名称/全称、迷你分时线、点位与涨跌幅。</summary>
public sealed class StocksView : WidgetView
{
    private static readonly Brush Bg = new LinearGradientBrush(UiKit.C("#363837"), UiKit.C("#1B1B1B"), 90);
    private static readonly Brush Sep = UiKit.B("#414140");
    private static readonly Brush Gray = UiKit.B("#98989D");
    static StocksView() { Bg.Freeze(); }

    private sealed class Row
    {
        public required string Symbol;
        public required Shapes.Path Tri;
        public required TextBlock Name, Full, Price, Pct;
        public required Sparkline Spark;
    }

    private readonly List<Row> _rows = [];
    private readonly Grid _root = new() { Margin = new Thickness(16, 12, 16, 12) };

    public override Brush BackgroundBrush => Bg;
    public override bool Outline => false;

    public StocksView(WidgetInstance inst, bool preview) : base(inst, preview)
    {
        Content = _root;
        Build();
    }

    private int RowCount => Instance.Size == WidgetSize.L ? 7 : 3;

    private List<string> Symbols()
    {
        var list = Instance.Options.Symbols.Select(MacWidgets.Core.Stocks.Symbols.Normalize).Where(s => s != null).Select(s => s!).Distinct().ToList();
        return list.Take(RowCount).ToList();
    }

    private void Build()
    {
        _root.Children.Clear();
        _root.RowDefinitions.Clear();
        _rows.Clear();
        var syms = Symbols();
        int n = Math.Max(1, Math.Min(RowCount, Math.Max(syms.Count, 1)));
        int slots = RowCount;
        for (int i = 0; i < slots; i++) _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < syms.Count; i++)
        {
            var cell = new Grid();
            cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DW * 0.5 - 16) });
            cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var line1 = new StackPanel { Orientation = Orientation.Horizontal };
            var tri = new Shapes.Path { Data = Icons.TriangleUp, Fill = UiKit.UpRed, Width = 9, Height = 8, Stretch = Stretch.Fill, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(1, 1, 4, 0) };
            var name = UiKit.T(syms[i], 15, FontWeights.SemiBold, UiKit.White);
            name.MaxWidth = DW * 0.5 - 16 - 16;
            line1.Children.Add(tri);
            line1.Children.Add(name);
            var full = UiKit.T(MacWidgets.Core.Stocks.Symbols.FullName(syms[i], null), 12, FontWeights.Normal, Gray);
            full.Margin = new Thickness(0, 1, 8, 0);
            left.Children.Add(line1);
            left.Children.Add(full);
            cell.Children.Add(left);

            var spark = new Sparkline { Height = 24, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(spark, 1);
            cell.Children.Add(spark);

            var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            var price = UiKit.T("--", 15, FontWeights.SemiBold, UiKit.White);
            price.HorizontalAlignment = HorizontalAlignment.Right;
            System.Windows.Documents.Typography.SetNumeralAlignment(price, FontNumeralAlignment.Tabular);
            var pct = UiKit.T("", 13, FontWeights.SemiBold, UiKit.UpRed);
            pct.HorizontalAlignment = HorizontalAlignment.Right;
            pct.Margin = new Thickness(0, 1, 0, 0);
            System.Windows.Documents.Typography.SetNumeralAlignment(pct, FontNumeralAlignment.Tabular);
            right.Children.Add(price);
            right.Children.Add(pct);
            Grid.SetColumn(right, 2);
            cell.Children.Add(right);

            Grid.SetRow(cell, i);
            _root.Children.Add(cell);

            if (i < slots - 1 && i < syms.Count - 1)
            {
                var sep = new Border { Height = 1, Background = Sep, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
                Grid.SetRow(sep, i);
                _root.Children.Add(sep);
            }
            _rows.Add(new Row { Symbol = syms[i], Tri = tri, Name = name, Full = full, Price = price, Pct = pct, Spark = spark });
        }
        if (syms.Count == 0)
        {
            var t = UiKit.T("右键 → 编辑小组件 添加股票代码", 13, FontWeights.Normal, Gray);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetRowSpan(t, slots);
            _root.Children.Add(t);
        }
        _ = n;
    }

    protected override void OnAttach()
    {
        StockHub.Updated += Update;
        StockHub.Register(this, Symbols());
        Update();
    }

    protected override void OnDetach()
    {
        StockHub.Updated -= Update;
        StockHub.Unregister(this);
    }

    public override void Refresh()
    {
        Build();
        StockHub.Register(this, Symbols());
        Update();
    }

    public override IEnumerable<MenuEntry> MenuItems() => [Item("立即刷新行情", StockHub.RefreshNow)];

    private static string FormatPrice(double v) =>
        Math.Abs(v) >= 10000 ? v.ToString("N0", CultureInfo.InvariantCulture) : v.ToString("N2", CultureInfo.InvariantCulture);

    private void Update()
    {
        bool redUp = Store.Settings.RedUp;
        foreach (var r in _rows)
        {
            var q = StockHub.GetQuote(r.Symbol);
            if (q == null)
            {
                r.Price.Text = "--";
                r.Pct.Text = "";
                r.Tri.Visibility = Visibility.Collapsed;
                continue;
            }
            bool up = q.Change >= 0;
            var brush = up == redUp ? UiKit.UpRed : UiKit.DownGreen;
            r.Tri.Visibility = Visibility.Visible;
            r.Tri.Data = up ? Icons.TriangleUp : Icons.TriangleDown;
            r.Tri.Fill = brush;
            r.Name.Text = q.Name;
            r.Full.Text = MacWidgets.Core.Stocks.Symbols.FullName(r.Symbol, q.Name);
            r.Price.Text = FormatPrice(q.Price);
            r.Pct.Text = (q.ChangePct >= 0 ? "+" : "") + q.ChangePct.ToString("0.00", CultureInfo.InvariantCulture) + "%";
            r.Pct.Foreground = brush;
            var intra = StockHub.GetIntraday(r.Symbol);
            var col = ((SolidColorBrush)brush).Color;
            r.Spark.SetData(intra?.Points, intra?.ExpectedPoints ?? MacWidgets.Core.Stocks.Symbols.ExpectedPoints(r.Symbol), q.PrevClose, col);
        }
        var at = StockHub.LastUpdate;
        ToolTip = (at != null ? $"更新于 {at:HH:mm:ss}" : "尚未获取行情") +
                  (StockHub.LastSource != null ? $" · 来源：{StockHub.LastSource}" : "") +
                  (StockHub.LastError != null ? $"\n最近一次失败：{StockHub.LastError}" : "") +
                  "\n数据仅供参考（腾讯/新浪/东方财富公开接口）";
    }
}
