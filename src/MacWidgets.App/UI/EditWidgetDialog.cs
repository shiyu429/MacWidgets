using System.Windows;
using System.Windows.Controls;
using MacWidgets.Core.Stocks;
using MacWidgets.Core.Weather;
using MacWidgets.Data;
using MacWidgets.Hosting;
using MacWidgets.Services;
using MacWidgets.Widgets;

namespace MacWidgets.UI;

/// <summary>“编辑小组件”面板：通用锁定+ 组件专属设置。</summary>
public sealed class EditWidgetDialog : Window
{
    private static readonly Dictionary<string, EditWidgetDialog> _open = [];

    private readonly WidgetHost _host;
    private readonly WidgetWindow _w;
    private readonly WidgetInstance _inst;
    private readonly List<Action> _apply = [];

    public static void Open(WidgetHost host, WidgetWindow w)
    {
        if (_open.TryGetValue(w.Instance.Id, out var existing))
        {
            existing.Activate();
            return;
        }
        var d = new EditWidgetDialog(host, w);
        _open[w.Instance.Id] = d;
        d.Closed += (_, _) => _open.Remove(w.Instance.Id);
        d.Show();
        d.Activate();
    }

    private EditWidgetDialog(WidgetHost host, WidgetWindow w)
    {
        _host = host;
        _w = w;
        _inst = w.Instance;
        Title = "编辑小组件 · " + Catalog.Name(_inst.Kind);
        Width = 480;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 760;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.ApplyTheme(this);

        var sp = new StackPanel { Margin = new Thickness(20) };
        sp.Children.Add(Ui.H2(Catalog.Name(_inst.Kind)));
        sp.Children.Add(Common());
        var specific = Specific();
        if (specific != null)
        {
            sp.Children.Add(Ui.H2("组件设置"));
            sp.Children.Add(specific);
        }

        var buttons = new DockPanel { Margin = new Thickness(0, 16, 0, 0), LastChildFill = false };
        var ok = Ui.Btn("完成", () => { Apply(); Close(); }, accent: true);
        var cancel = Ui.Btn("取消", Close);
        var remove = Ui.Btn("移除小组件", () => { _host.Remove(_w); Close(); });
        DockPanel.SetDock(ok, Dock.Right);
        DockPanel.SetDock(cancel, Dock.Right);
        DockPanel.SetDock(remove, Dock.Left);
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        buttons.Children.Add(remove);
        sp.Children.Add(buttons);
        Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void Apply()
    {
        foreach (var a in _apply) a();
        _w.View.Refresh();
        Store.SaveLayoutSoon();
        _host.NotifyChanged();
    }

    private UIElement Common()
    {
        var locked = new CheckBox { Content = "锁定此组件位置（防止误拖动）", IsChecked = _inst.Locked, MinHeight = 36, Margin = new Thickness(0, 4, 0, 4) };
        _apply.Add(() => _inst.Locked = locked.IsChecked == true);
        return Ui.Card(new StackPanel { Children = { locked, Ui.P("组件尺寸已固定为组件库默认大小；请使用“设置 → 全局缩放”统一调整。", 12, Ui.Gray) } });
    }

    private UIElement? Specific() => _inst.Kind switch
    {
        WidgetKind.Clock => ClockPanel(),
        WidgetKind.Weather => WeatherPanel(),
        WidgetKind.Reminders => RemindersPanel(),
        WidgetKind.Stocks => StocksPanel(),
        WidgetKind.Photo => PhotoPanel(),
        WidgetKind.Plan => PlanPanel(),
        WidgetKind.Notes => NotesPanel(),
        _ => null,
    };

    private UIElement ClockPanel()
    {
        var o = _inst.Options;
        var h24 = new CheckBox { Content = "24 小时制", IsChecked = o.Clock24, Margin = new Thickness(0, 4, 0, 4) };
        var sweep = new CheckBox { Content = "显示走秒刻度环（彗尾效果）", IsChecked = o.ClockSweep, Margin = new Thickness(0, 4, 0, 4) };
        _apply.Add(() => { o.Clock24 = h24.IsChecked == true; o.ClockSweep = sweep.IsChecked == true; });
        return Ui.Card(new StackPanel
        {
            Children = { h24, sweep, Ui.P("“平滑流动 / 每秒跳一格”在 主界面 → 设置 中统一切换。", 12, Ui.Gray) },
        });
    }

    private UIElement WeatherPanel()
    {
        var o = _inst.Options;
        var sp = new StackPanel();
        var auto = new RadioButton { Content = "自动定位（按 IP 估算位置）", GroupName = "loc", IsChecked = o.WeatherAuto, Margin = new Thickness(0, 4, 0, 4) };
        var manual = new RadioButton { Content = "指定城市", GroupName = "loc", IsChecked = !o.WeatherAuto, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(auto);
        sp.Children.Add(manual);

        var current = Ui.P(o.City != null ? $"当前城市：{o.City}" : "尚未选择城市", 12, Ui.Gray);
        var query = new TextBox { Width = 220, Text = o.City ?? "" };
        var list = new ListBox { Height = 150, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
        var status = Ui.P("", 12, Ui.Gray);
        GeoPlace? picked = null;
        string? displayName = o.City;
        var nameBox = new TextBox { Width = 220, Text = o.City ?? "", ToolTip = "组件左上角显示的地名，可改成区县名" };

        async void Search()
        {
            var q = query.Text.Trim();
            if (q.Length == 0) return;
            status.Text = "搜索中…";
            try
            {
                var res = await WeatherClient.SearchAsync(q);
                list.Items.Clear();
                foreach (var p in res) list.Items.Add(new ListBoxItem { Content = p.Display, Tag = p });
                list.Visibility = res.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                status.Text = res.Count > 0 ? "选择一个结果：" : "没有找到，换个写法试试（如“北京”“Beijing”）";
            }
            catch (Exception ex) { status.Text = "搜索失败：" + ex.Message; }
        }
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is ListBoxItem { Tag: GeoPlace p })
            {
                picked = p;
                manual.IsChecked = true;
                nameBox.Text = p.Name;
                current.Text = $"已选择：{p.Display}（{p.Lat:F2}, {p.Lon:F2}）";
            }
        };
        query.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Search(); };
        sp.Children.Add(Ui.Row(query, Ui.Btn("搜索", Search)));
        sp.Children.Add(status);
        sp.Children.Add(list);
        sp.Children.Add(current);
        sp.Children.Add(Ui.Row(Ui.Label("显示名称", 80), nameBox));
        sp.Children.Add(Ui.P("数据来源：Open-Meteo（每 30 分钟刷新）。温度单位在 主界面 → 设置 中切换。", 12, Ui.Gray));

        _apply.Add(() =>
        {
            o.WeatherAuto = auto.IsChecked == true;
            if (picked != null) { o.Lat = picked.Lat; o.Lon = picked.Lon; }
            var n = nameBox.Text.Trim();
            o.City = n.Length > 0 ? n : picked?.Name ?? displayName;
            if (!o.WeatherAuto && (o.Lat == null || o.Lon == null)) o.WeatherAuto = true;
        });
        return Ui.Card(sp);
    }

    private UIElement RemindersPanel()
    {
        var o = _inst.Options;
        var today = new RadioButton { Content = "今天（今天到期、已过期，以及今天新建的未设日期事项）", GroupName = "list", IsChecked = o.ListMode == ReminderListMode.Today, Margin = new Thickness(0, 4, 0, 4) };
        var all = new RadioButton { Content = "全部未完成", GroupName = "list", IsChecked = o.ListMode == ReminderListMode.All, Margin = new Thickness(0, 4, 0, 4) };
        _apply.Add(() => o.ListMode = all.IsChecked == true ? ReminderListMode.All : ReminderListMode.Today);
        return Ui.Card(new StackPanel
        {
            Children =
            {
                Ui.P("显示列表"), today, all,
                Ui.Row(Ui.Btn("新建提醒…", () => ReminderDialog.ShowNew(false)), Ui.Btn("清除已完成", ReminderStore.ClearCompleted)),
            },
        });
    }

    private UIElement PlanPanel() => Ui.Card(new StackPanel
    {
        Children =
        {
            Ui.P("显示所有已安排日期（今天及以后）的未完成提醒，按日期时间排序；左下角数字为计划事项总数。", 12, Ui.Gray),
            Ui.Row(Ui.Btn("新建计划提醒…", () => ReminderDialog.ShowNew(true)), Ui.Btn("清除已完成", ReminderStore.ClearCompleted)),
        },
    });

    private UIElement StocksPanel()
    {
        var o = _inst.Options;
        var box = new TextBox
        {
            AcceptsReturn = true,
            Height = 150,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = string.Join(Environment.NewLine, o.Symbols),
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Microsoft YaHei UI"),
        };
        var status = Ui.P("", 12, Ui.Gray);
        List<string> Parse() => box.Text.Split(['\n', '\r', ',', '，', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(Symbols.Normalize).Where(s => s != null).Select(s => s!).Distinct().ToList();

        async void Validate()
        {
            var syms = Parse();
            if (syms.Count == 0) { status.Text = "没有有效代码"; return; }
            status.Text = "校验中…";
            try
            {
                var q = await StockHub.ValidateAsync(syms);
                var ok = q.Select(x => $"{x.Symbol} {x.Name} {x.Price:0.##}").ToList();
                var bad = syms.Where(s => q.All(x => x.Symbol != s)).ToList();
                status.Text = "有效：" + string.Join("；", ok) + (bad.Count > 0 ? "\n无数据：" + string.Join("、", bad) : "");
            }
            catch (Exception ex) { status.Text = "校验失败：" + ex.Message; }
        }

        _apply.Add(() =>
        {
            var syms = Parse();
            o.Symbols = syms.Count > 0 ? syms : [.. Symbols.Defaults];
        });
        return Ui.Card(new StackPanel
        {
            Children =
            {
                Ui.P("每行一个代码（中号显示前 3 个，大号显示前 7 个）：", 13),
                Ui.P("A 股 / 指数：sh000001 上证指数、sz399006 创业板指、sh600519；也可只写 6 位数字\n港股：hkHSI 恒生指数、hk00700；美股：usAAPL、us.DJI、us.IXIC", 12, Ui.Gray),
                box,
                Ui.Row(Ui.Btn("校验代码", Validate), Ui.Btn("恢复默认", () => box.Text = string.Join(Environment.NewLine, Symbols.Defaults))),
                status,
                Ui.P("涨跌颜色在 主界面 → 设置 中切换（默认红涨绿跌）。", 12, Ui.Gray),
            },
        });
    }

    private UIElement PhotoPanel()
    {
        var sp = new StackPanel();
        var view = _w.View as PhotoView;
        var info = Ui.P(_inst.Options.PhotoFile != null ? "已设置照片" : "尚未设置照片", 12, Ui.Gray);
        sp.Children.Add(info);
        sp.Children.Add(Ui.Row(
            Ui.Btn("选择照片…", () => { view?.Choose(); info.Text = _inst.Options.PhotoFile != null ? "已设置照片" : "尚未设置照片"; }),
            Ui.Btn("调整照片…", () => { if (_inst.Options.PhotoFile != null && view != null) PhotoAdjustDialog.Open(_inst, view); }),
            Ui.Btn("移除照片", () => { _inst.Options.PhotoFile = null; view?.Refresh(); Store.SaveLayoutSoon(); info.Text = "尚未设置照片"; })));
        sp.Children.Add(Ui.P("图片会复制到数据文件夹，原图删除也不影响。支持 JPG/PNG/BMP/GIF/WebP/TIFF，HEIC 需系统已安装 HEIF 扩展。", 12, Ui.Gray));
        return Ui.Card(sp);
    }

    private UIElement NotesPanel()
    {
        var o = _inst.Options;
        var box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 180,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = o.NoteText ?? "",
        };
        _apply.Add(() => o.NoteText = box.Text);
        return Ui.Card(new StackPanel
        {
            Children = { Ui.P("备忘录内容（也可以在桌面上双击组件直接编辑）："), box },
        });
    }

}
