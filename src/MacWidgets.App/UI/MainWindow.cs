using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MacWidgets.Core;
using MacWidgets.Data;
using MacWidgets.Hosting;
using MacWidgets.Interop;
using MacWidgets.Services;
using MacWidgets.Widgets;

namespace MacWidgets.UI;

/// <summary>主界面：组件库 / 我的桌面 / 设置 / 关于。关闭 = 隐藏到托盘。</summary>
public sealed class MainWindow : Window
{
    private readonly ContentControl _page = new();
    private readonly ListBox _nav = new();
    private IntPtr _hwnd;

    public MainWindow()
    {
        Title = "MacWidgets 桌面小组件";
        // 1.0.16：默认宽到能排下组件库两列卡片（含 125%/150% DPI 常见工作区）
        // WrapPanel + GalleryCard≈366DIP；侧栏 200 + 页边距/滚动条约 250 → 两列约需 ≥1000
        Width = MainWindowLayout.GalleryTwoColumnWindowWidth;
        Height = 760;
        MinWidth = MainWindowLayout.GalleryTwoColumnMinWidth;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF7));
        Ui.ApplyTheme(this);
        try
        {
            using var s = typeof(MainWindow).Assembly.GetManifestResourceStream("MacWidgets.app.ico");
            if (s != null) Icon = System.Windows.Media.Imaging.BitmapFrame.Create(s, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        }
        catch { }

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var side = new DockPanel { Margin = new Thickness(12, 16, 8, 16) };
        var brand = new TextBlock { Text = "桌面小组件", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 16) };
        DockPanel.SetDock(brand, Dock.Top);
        side.Children.Add(brand);
        foreach (var n in new[] { "组件库", "我的桌面", "设置", "关于" })
            _nav.Items.Add(new ListBoxItem { Content = n, FontSize = 14 });
        _nav.BorderThickness = new Thickness(0);
        _nav.Background = Brushes.Transparent;
        var navStyle = new Style(typeof(ListBoxItem));
        navStyle.Setters.Add(new Setter(Control.MinHeightProperty, 44.0));
        navStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14,10,14,10)));
        navStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0,2,0,2)));
        navStyle.Setters.Add(new Setter(Control.TemplateProperty, NavTemplate()));
        _nav.ItemContainerStyle = navStyle;
        _nav.SelectionChanged += (_, _) => ShowPage(_nav.SelectedIndex);
        side.Children.Add(_nav);
        root.Children.Add(side);

        _page.Margin = new Thickness(8, 16, 16, 16);
        Grid.SetColumn(_page, 1);
        root.Children.Add(_page);
        Content = root;

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            Stylus.SetIsPressAndHoldEnabled(this, true);
            FitToWorkArea();
        };
        Loaded += (_, _) => FitToWorkArea();
        Closing += (_, e) =>
        {
            // 关闭 = 隐藏到托盘，组件继续运行
            if (App.Quitting) return;
            e.Cancel = true;
            Hide();
        };
        App.Host.Changed += OnHostChanged;
        _nav.SelectedIndex = 0;
    }

    private void FitToWorkArea()
    {
        try
        {
            var hwnd = _hwnd != IntPtr.Zero ? _hwnd : new WindowInteropHelper(this).Handle;
            var mon = hwnd != IntPtr.Zero
                ? Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST)
                : Native.MonitorFromPoint(new Native.POINT { X = 0, Y = 0 }, Native.MONITOR_DEFAULTTOPRIMARY);
            var info = new Native.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFOEX>() };
            if (mon != IntPtr.Zero && Native.GetMonitorInfo(mon, ref info))
            {
                double scale = 1;
                try { uint dpi = Native.GetDpiForWindow(hwnd); if (dpi > 0) scale = dpi / 96.0; } catch { }
                double workW = (info.rcWork.Right - info.rcWork.Left) / scale;
                double workH = (info.rcWork.Bottom - info.rcWork.Top) / scale;
                // 1.0.16：默认宽到组件库两列；无持久化窗口尺寸，此即首次打开默认
                Width = MainWindowLayout.DefaultWindowWidthForWorkArea(workW);
                Height = Math.Clamp(Math.Max(workH * 0.80, 720), MinHeight, Math.Min(900, workH - 24));
            }
        }
        catch (Exception ex) { Store.Log("FitToWorkArea: " + ex.Message); }
    }

    private static ControlTemplate NavTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.Name = "NavSurface";
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);

        var template = new ControlTemplate(typeof(ListBoxItem)) { VisualTree = border };

        // 未选中悬停：更明显的浅灰
        var hoverOnly = new MultiTrigger();
        hoverOnly.Conditions.Add(new Condition(UIElement.IsMouseOverProperty, true));
        hoverOnly.Conditions.Add(new Condition(ListBoxItem.IsSelectedProperty, false));
        hoverOnly.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xDE, 0xE3, 0xEB)), "NavSurface"));
        hoverOnly.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E))));

        // 选中（无悬停）：淡蓝底 + 蓝字 + 略加粗
        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xD4, 0xE5, 0xF9)), "NavSurface"));
        selected.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x14, 0x58, 0xB8))));
        selected.Setters.Add(new Setter(ListBoxItem.FontWeightProperty, FontWeights.SemiBold));

        // 选中 + 悬停：略加深的蓝灰，禁止跳成纯白/浅白
        var selectedHover = new MultiTrigger();
        selectedHover.Conditions.Add(new Condition(ListBoxItem.IsSelectedProperty, true));
        selectedHover.Conditions.Add(new Condition(UIElement.IsMouseOverProperty, true));
        selectedHover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xC0, 0xD7, 0xF2)), "NavSurface"));
        selectedHover.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x12, 0x4E, 0xA8))));
        selectedHover.Setters.Add(new Setter(ListBoxItem.FontWeightProperty, FontWeights.SemiBold));

        template.Triggers.Add(hoverOnly);
        template.Triggers.Add(selected);
        template.Triggers.Add(selectedHover); // 最后：选中悬停覆盖纯选中
        return template;
    }

    private int _currentPage = -1;

    private void ShowPage(int i)
    {
        try
        {
            _page.Content = i switch
            {
                0 => GalleryPage(),
                1 => DesktopPage(),
                2 => SettingsPage(),
                _ => AboutPage(),
            };
            _currentPage = i;
        }
        catch (Exception ex)
        {
            Store.Log($"主界面页面 {i} 构建失败：{ex}");
            _page.Content = ErrorPage("页面加载失败", ex);
        }
    }

    private static UIElement ErrorPage(string title, Exception ex) => new ScrollViewer
    {
        Content = new StackPanel
        {
            Children =
            {
                Ui.H1(title),
                Ui.P("程序仍在运行。请打开“设置 → 数据 → 打开数据文件夹”，把 log.txt 提供给开发者。", 14),
                Ui.P(ex.Message, 12, Ui.Gray),
            },
        },
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    };

    private void OnHostChanged()
    {
        if (_currentPage == 1 && IsVisible && !App.Host.IsDragging) _page.Content = DesktopPage();
    }

    private static ScrollViewer TouchScroll(UIElement content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Padding = new Thickness(0, 0, 8, 0),
        PanningMode = PanningMode.VerticalOnly,
        PanningDeceleration = 0.002,
        CanContentScroll = false,
    };

    // ================= 组件库 =================
    private UIElement GalleryPage()
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.H1("组件库"));
        sp.Children.Add(Ui.P("点每个组件卡片上的“添加到桌面”即可放到桌面。桌面上的组件可直接拖动，右键可编辑内容、锁定或移除；统一在设置中调整全局缩放。", 13, Ui.Gray));
        // 1.0.8：WrapPanel 自适应多列，不再每行只有一个组件
        var wrap = new WrapPanel { Margin = new Thickness(0, 12, 0, 0), Orientation = Orientation.Horizontal };
        foreach (var k in Catalog.Kinds)
        {
            try { wrap.Children.Add(GalleryCard(k)); }
            catch (Exception ex)
            {
                Store.Log($"组件库 {k} 预览失败：{ex}");
                wrap.Children.Add(Ui.Card(new StackPanel
                {
                    Children = { Ui.H2(Catalog.Name(k)), Ui.P("预览加载失败，仍可点击按钮添加。", 13, Ui.Gray), Ui.Btn("添加到桌面", () => SafeAddWidget(k, Catalog.DefaultSize(k)), true) },
                }));
            }
        }
        sp.Children.Add(wrap);
        return TouchScroll(sp);
    }

    /// <summary>「添加到桌面」：与预览是否成功完全无关；失败写 log.txt（含堆栈）并提示，不抛出。</summary>
    private void SafeAddWidget(WidgetKind kind, WidgetSize size)
    {
        try
        {
            Store.Log($"gallery-add {kind}/{size}");
            var w = App.Host.AddWidget(kind, size);
            Store.Log($"gallery-add ok {kind} hwnd={w.Hwnd}");
        }
        catch (Exception ex)
        {
            Store.Log($"gallery-add {kind}/{size} 失败：{ex}");
            try { MessageBox.Show(this, "添加组件失败，详情已写入 %AppData%\\MacWidgets\\log.txt。\n" + ex.Message, "MacWidgets", MessageBoxButton.OK, MessageBoxImage.Warning); }
            catch { }
        }
    }

    private FrameworkElement GalleryCard(WidgetKind kind)
    {
        var size = Catalog.DefaultSize(kind);
        var card = new StackPanel();
        var head = new DockPanel();
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Text = Catalog.Name(kind), FontSize = 15, FontWeight = FontWeights.SemiBold });
        title.Children.Add(new TextBlock { Text = Catalog.Description(kind), FontSize = 12, Foreground = Ui.Gray });
        head.Children.Add(title);
        card.Children.Add(head);

        var stage = new Border
        {
            Height = 240,
            Margin = new Thickness(0, 10, 0, 10),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(0xF6, 0xE0, 0xCB)),
            Name = "GalleryStage",
            ClipToBounds = true,
            IsHitTestVisible = false, // 预览只展示，添加靠下方按钮
        };
        card.Children.Add(stage);

        void SetPreview()
        {
            try { SetPreviewCore(); }
            catch (Exception ex)
            {
                // 1.0.13：预览失败只降级为占位，不影响「添加到桌面」
                Store.Log($"组件库 {kind} 预览创建失败（已降级为占位）：{ex}");
                stage.Child = new TextBlock
                {
                    Text = Catalog.Name(kind) + "\n（预览暂不可用，仍可点按钮添加）",
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Ui.Gray,
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                };
            }
        }
        void SetPreviewCore()
        {
            // 预览必须走 WidgetChrome，与桌面共用 WidgetView.CardRadius squircle，禁止另写圆角
            var inst = new WidgetInstance { Id = "preview-" + kind, Kind = kind, Size = size };
            var view = WidgetFactory.Create(inst, true);
            var chrome = new WidgetChrome(view)
            {
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            RenderOptions.SetEdgeMode(chrome, EdgeMode.Unspecified);
            RenderOptions.SetBitmapScalingMode(chrome, BitmapScalingMode.HighQuality);
            var fit = new Viewbox
            {
                Child = chrome,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Margin = new Thickness(12),
                IsHitTestVisible = false,
            };
            stage.Child = fit;
        }
        SetPreview();

        // 1.0.17：恢复 1.0.14 通栏醒目「添加到桌面」（非右对齐旧样式）
        var add = Ui.Btn("添加到桌面", () => SafeAddWidget(kind, size), accent: true);
        add.Margin = new Thickness(0);
        add.MinHeight = 44;
        add.HorizontalAlignment = HorizontalAlignment.Stretch;
        card.Children.Add(add);

        var b = Ui.Card(card, 14);
        // 1.0.16：卡片仍偏宽；右间距略收，配合更宽默认窗保证两列
        b.Width = 352;
        b.HorizontalAlignment = HorizontalAlignment.Left;
        b.Margin = new Thickness(0, 0, 12, 14);
        return b;
    }

    // ================= 我的桌面 =================
    private UIElement DesktopPage()
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.H1("我的桌面"));
        var tools = Ui.Row(
            Ui.Btn("按参考图重新排列", () => App.Host.ArrangeLikeReference()),
            Ui.Btn(Store.Settings.LayoutLocked ? "解锁布局" : "锁定布局", () => { App.Host.SetLayoutLocked(!Store.Settings.LayoutLocked); ShowPage(1); }));
        sp.Children.Add(tools);
        sp.Children.Add(Ui.P("一键锁定后所有桌面组件都无法拖动；需要调整时再点“解锁布局”。拖动时 Alt 临时关闭吸附，Shift 锁定方向，Esc 取消。", 12, Ui.Gray));

        if (App.Host.Windows.Count == 0)
            sp.Children.Add(Ui.P("桌面上还没有组件，去“组件库”点“添加到桌面”吧。", 13, Ui.Gray));

        foreach (var w in App.Host.Windows.ToList())
        {
            var inst = w.Instance;
            var row = new DockPanel();
            var info = new StackPanel();
            info.Children.Add(new TextBlock { Text = Catalog.Name(inst.Kind), FontSize = 14, FontWeight = FontWeights.SemiBold });
            var mon = Monitors.ByDevice(inst.Monitor);
            info.Children.Add(new TextBlock
            {
                Text = $"{mon.Device.TrimStart('\\', '.')} ({mon.Scale:P0}) · 位置 {inst.X:0},{inst.Y:0}" + (inst.Locked ? " · 已锁定" : ""),
                FontSize = 12,
                Foreground = Ui.Gray,
            });
            var btns = Ui.Row(
                Ui.Btn("编辑", () => EditWidgetDialog.Open(App.Host, w)),
                Ui.Btn("定位", w.Flash),
                Ui.Btn("重置位置", () => App.Host.ResetPosition(w)),
                Ui.Btn("移除", () => App.Host.Remove(w)));
            DockPanel.SetDock(btns, Dock.Right);
            row.Children.Add(btns);
            row.Children.Add(info);
            sp.Children.Add(Ui.Card(row, 12));
        }
        return TouchScroll(sp);
    }

    // ================= 设置 =================
    private UIElement SettingsPage()
    {
        var st = Store.Settings;
        var sp = new StackPanel();
        sp.Children.Add(Ui.H1("设置"));

        var general = new StackPanel();
        general.Children.Add(Ui.Check("开机自动启动（登录 Windows 后在后台运行，不弹出主界面）", st.Autostart, v =>
        {
            try { Autostart.Set(v); st.Autostart = v; }
            catch (Exception ex) { MessageBox.Show("设置开机自启失败：" + ex.Message); }
            Store.SaveLayoutSoon();
        }));
        sp.Children.Add(Ui.H2("常规"));
        sp.Children.Add(Ui.Card(general));

        var layout = new StackPanel();
        layout.Children.Add(Ui.Check("拖动时对齐吸附（屏幕边缘、中线、其他组件边缘与 16 像素等间距）", st.SnapEnabled, v => { st.SnapEnabled = v; Store.SaveLayoutSoon(); }));
        layout.Children.Add(Ui.Check("未命中对齐线时吸附到网格", st.GridEnabled, v => { st.GridEnabled = v; Store.SaveLayoutSoon(); }));
        var grid = new ComboBox { Width = 120 };
        foreach (var g in new[] { 8, 16, 24, 32 }) grid.Items.Add(g);
        grid.SelectedItem = st.GridSize;
        if (grid.SelectedItem == null) grid.SelectedItem = 16;
        grid.SelectionChanged += (_, _) => { if (grid.SelectedItem is int g) { st.GridSize = g; Store.SaveLayoutSoon(); } };
        layout.Children.Add(Ui.Row(Ui.Label("网格大小"), grid));
        layout.Children.Add(Ui.Check("放下时自动避开重叠（重叠超过 30% 时移到最近空位）", st.AvoidOverlap, v => { st.AvoidOverlap = v; Store.SaveLayoutSoon(); }));

        var scaleText = new TextBlock { Width = 60, VerticalAlignment = VerticalAlignment.Center, Text = st.GlobalScale.ToString("P0") };
        var scale = new Slider
        {
            Minimum = 0.75,
            Maximum = 2.0,
            Value = st.GlobalScale,
            Width = 260,
            TickFrequency = 0.05,
            IsSnapToTickEnabled = true,
            IsMoveToPointEnabled = true, // 点击轨道任意位置跳到对应百分比
            VerticalAlignment = VerticalAlignment.Center,
        };
        scale.ValueChanged += (_, _) => { scaleText.Text = scale.Value.ToString("P0"); };
        // 部分主题下 IsMoveToPointEnabled 不生效：轨道按下时按 X 比例设值；点在 Thumb（含模板子元素）上则交给拖动
        scale.PreviewMouseLeftButtonDown += (s, e) =>
        {
            for (var d = e.OriginalSource as DependencyObject; d != null && !ReferenceEquals(d, scale);
                 d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            {
                if (d is System.Windows.Controls.Primitives.Thumb) return;
            }
            if (!double.IsFinite(scale.ActualWidth) || scale.ActualWidth <= 0) return;
            var pos = e.GetPosition(scale);
            double w = Math.Max(1.0, scale.ActualWidth);
            double ratio = Math.Clamp(pos.X / w, 0, 1);
            double raw = scale.Minimum + ratio * (scale.Maximum - scale.Minimum);
            double tick = scale.TickFrequency > 0 ? scale.TickFrequency : 0.05;
            scale.Value = Math.Clamp(Math.Round(raw / tick) * tick, scale.Minimum, scale.Maximum);
            st.GlobalScale = Math.Round(scale.Value, 2);
            App.Host.ApplySettings();
            e.Handled = true;
        };
        scale.PreviewMouseUp += (_, _) => { st.GlobalScale = Math.Round(scale.Value, 2); App.Host.ApplySettings(); };
        scale.KeyUp += (_, _) => { st.GlobalScale = Math.Round(scale.Value, 2); App.Host.ApplySettings(); };
        layout.Children.Add(Ui.Row(Ui.Label("全局缩放"), scale, scaleText));
        layout.Children.Add(Ui.P("所有组件统一按此比例缩放，并按所在显示器的 DPI（Per-Monitor V2）清晰渲染。", 12, Ui.Gray));
        sp.Children.Add(Ui.H2("布局与缩放"));
        sp.Children.Add(Ui.Card(layout));

        st.DesktopMode = DesktopMode.Bottom;

        var data = new StackPanel();
        data.Children.Add(Ui.Check("股票红涨绿跌（关闭则绿涨红跌）", st.RedUp, v => { st.RedUp = v; App.Host.ApplySettings(); }));
        data.Children.Add(Ui.Check("温度使用华氏度 °F", st.Fahrenheit, v => { st.Fahrenheit = v; App.Host.ApplySettings(); }));
        data.Children.Add(Ui.Check("时钟刻度环平滑流动（关闭则每秒跳一格，更省电）", st.ClockSmooth, v => { st.ClockSmooth = v; App.Host.ApplySettings(); }));
        var refresh = new ComboBox { Width = 120 };
        foreach (var s in new[] { 10, 15, 30, 60 }) refresh.Items.Add(s);
        refresh.SelectedItem = st.StockRefreshSeconds;
        if (refresh.SelectedItem == null) refresh.SelectedItem = 15;
        refresh.SelectionChanged += (_, _) => { if (refresh.SelectedItem is int s) { st.StockRefreshSeconds = s; Store.SaveLayoutSoon(); } };
        data.Children.Add(Ui.Row(Ui.Label("交易时段行情刷新(秒)", 160), refresh));
        sp.Children.Add(Ui.H2("数据与显示"));
        sp.Children.Add(Ui.Card(data));

        var folder = Ui.Row(
            Ui.Btn("打开数据文件夹", () => { try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Store.Dir}\"") { UseShellExecute = true }); } catch { } }),
            Ui.Btn("退出程序", App.Quit));
        sp.Children.Add(Ui.H2("数据"));
        sp.Children.Add(Ui.Card(new StackPanel { Children = { Ui.P("布局、提醒事项、备忘录与行情缓存以 JSON 保存在：" + Store.Dir, 12, Ui.Gray), folder } }));

        return TouchScroll(sp);
    }

    // ================= 关于 =================
    private UIElement AboutPage()
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.H1("关于"));
        var ver = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        sp.Children.Add(Ui.Card(new StackPanel
        {
            Children =
            {
                Ui.P($"MacWidgets 桌面小组件 {ver}", 15),
                Ui.P("仿 macOS 风格的 Windows 桌面小组件（C# / .NET 10 / WPF）。字体使用 Windows 自带的 Segoe UI / 微软雅黑 / Impact / Bahnschrift，图标均为自绘矢量，不包含苹果字体或图标素材。", 13, Ui.Gray),
            },
        }));
        sp.Children.Add(Ui.H2("数据来源"));
        sp.Children.Add(Ui.Card(new StackPanel
        {
            Children =
            {
                Ui.P("行情：腾讯财经 qt.gtimg.cn（主）→ 新浪财经 hq.sinajs.cn → 东方财富 push2（备用）；分时：腾讯 web.ifzq.gtimg.cn / 东方财富 push2his。均为非官方公开接口，数据仅供个人参考，可能延迟或中断。", 13),
                Ui.P("天气：Open-Meteo（api.open-meteo.com，免费非商用）；城市搜索：内置中国城市库 + OpenStreetMap Nominatim（© OpenStreetMap contributors）。", 13),
                Ui.P("定位：ipwho.is（IP 定位）+ OpenStreetMap Nominatim（区县名反查，© OpenStreetMap contributors）。", 13),
            },
        }));
        sp.Children.Add(Ui.H2("快捷操作"));
        sp.Children.Add(Ui.Card(new StackPanel
        {
            Children =
            {
                Ui.P("• 拖动组件：按住任意空白处拖动；Alt = 临时关闭吸附，Shift = 锁定水平/垂直，Esc = 取消"),
                Ui.P("• 缩放：在“设置 → 布局与缩放 → 全局缩放”中统一调整"),
                Ui.P("• 备忘录：双击正文编辑，点别处或 Esc 结束（自动保存）"),
                Ui.P("• 提醒事项：悬停显示“+”新建，回车保存；点圆圈完成；双击标题编辑；右键删除"),
                Ui.P("• 照片：双击/右键“选择照片…”或把图片拖到组件上；右键“调整照片…”裁剪缩放"),
            },
        }));
        return TouchScroll(sp);
    }
}
