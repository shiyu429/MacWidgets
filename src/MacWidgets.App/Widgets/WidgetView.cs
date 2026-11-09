using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MacWidgets.Data;
using MacWidgets.UI;

namespace MacWidgets.Widgets;

/// <summary>组件宿主提供给视图的能力（桌面窗口实现；主界面预览为 null）。</summary>
public interface IWidgetContext
{
    /// <summary>需要键盘输入时临时让窗口可激活。</summary>
    void BeginTextInput();
    void EndTextInput();
    void SaveInstance();
    void OpenEditor();
}

/// <summary>所有组件视图的基类：固定设计尺寸布局，外层由宿主做 LayoutTransform 缩放。</summary>
public abstract class WidgetView : UserControl
{
    /// <summary>卡片圆角（设计坐标系）。桌面组件与主界面组件库预览共用，禁止另写魔法数。1.0.18：按 weather-bg-ref 实测 ≈28@170（328px 参考图圆角区 ~54px），SoftSquircleMask 抗锯齿。</summary>
    public const double CardRadius = 28;

    public WidgetInstance Instance { get; }
    public bool IsPreview { get; }
    public IWidgetContext? Context { get; set; }
    protected double DW { get; }
    protected double DH { get; }
    private bool _attached;

    protected WidgetView(WidgetInstance inst, bool preview)
    {
        Instance = inst;
        IsPreview = preview;
        var sz = Catalog.DesignSize(inst.Size);
        Width = DW = sz.Width;
        Height = DH = sz.Height;
        FontFamily = UiKit.Text;
        // UserControl 默认背景可能被 Fluent 主题刷成浅色矩形，必须显式透明。
        Background = Brushes.Transparent;
        // 圆角/矢量走 AA：像素吸附会让 squircle、刻度环出现锯齿
        UseLayoutRounding = false;
        SnapsToDevicePixels = false;
        RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale); // ClearType 在透明窗上会留下矩形底色
        TextOptions.SetTextHintingMode(this, TextHintingMode.Fixed);
        Loaded += (_, _) => { if (!_attached) { _attached = true; OnAttach(); } };
        Unloaded += (_, _) => { if (_attached) { _attached = false; OnDetach(); } };
    }

    public virtual Brush BackgroundBrush => Brushes.White;
    /// <summary>白色组件在浅色桌面上加 1px 极淡描边。</summary>
    public virtual bool Outline => true;
    /// <summary>沉浸铺满类组件（有图照片/原神日历）可关掉外阴影，避免看起来像描边框。</summary>
    public virtual bool ChromeShadow => true;

    protected virtual void OnAttach() { }
    protected virtual void OnDetach() { }

    /// <summary>选项改变后刷新。</summary>
    public virtual void Refresh() { }

    /// <summary>右键菜单中的组件专属项。</summary>
    public virtual IEnumerable<MenuEntry> MenuItems() => [];

    /// <summary>拖入文件（照片组件）。</summary>
    public virtual bool AcceptsFileDrop => false;
    public virtual void OnFileDropped(string path) { }

    protected static MenuEntry Item(string header, Action click) => MenuEntry.Item(header, click);

    /// <summary>内容变化导致底色/描边改变时，通知外层 WidgetChrome 同步。</summary>
    protected void SyncChrome()
    {
        for (DependencyObject? p = this; p != null; p = System.Windows.Media.VisualTreeHelper.GetParent(p))
        {
            if (p is WidgetChrome chrome)
            {
                chrome.SyncFromView();
                return;
            }
        }
    }
}

/// <summary>组件外观：squircle 背景 + 极淡阴影/描边 + 内容裁剪。桌面窗口与主界面预览共用。</summary>
public sealed class WidgetChrome : Grid
{
    public WidgetView View { get; }
    private readonly System.Windows.Shapes.Path _bg;

    public WidgetChrome(WidgetView view)
    {
        View = view;
        var sz = Catalog.DesignSize(view.Instance.Size);
        Width = sz.Width;
        Height = sz.Height;
        UseLayoutRounding = false;
        SnapsToDevicePixels = false;
        RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        Background = Brushes.Transparent;
        double r = WidgetView.CardRadius;
        var geo = UiKit.Squircle(sz.Width, sz.Height, r);
        // 软边 OpacityMask：圆角外 alpha→0，边缘带 ~0.85DIP 半透明 fringe，抗锯齿更顺。
        // 1.0.13：任何异常都回退到 1.0.11 的硬边 mask，再不行就不加 mask，绝不让外观问题阻断创建。
        try { OpacityMask = UiKit.SoftSquircleMask(sz.Width, sz.Height, r); }
        catch (Exception ex)
        {
            Store.Log($"WidgetChrome 软边 mask 失败，回退硬边：{ex}");
            try { OpacityMask = UiKit.HardSquircleMask(sz.Width, sz.Height, r); }
            catch (Exception ex2) { Store.Log($"WidgetChrome 硬边 mask 也失败，不加 mask：{ex2}"); OpacityMask = null; }
        }
        _bg = new System.Windows.Shapes.Path
        {
            Data = geo,
            Fill = view.BackgroundBrush,
            // 极淡描边参与 AA（不用 Effect，避免透明窗黑边）
            Stroke = view.Outline ? UiKit.B("#000000", 0.06) : UiKit.B("#000000", 0.02),
            StrokeThickness = view.Outline ? 0.75 : 0.5,
            StrokeLineJoin = PenLineJoin.Round,
            Effect = view.ChromeShadow ? UiKit.Shadow() : null,
            IsHitTestVisible = true,
            SnapsToDevicePixels = false,
            UseLayoutRounding = false,
        };
        RenderOptions.SetEdgeMode(_bg, EdgeMode.Unspecified);
        RenderOptions.SetBitmapScalingMode(_bg, BitmapScalingMode.HighQuality);
        Children.Add(_bg);
        // 1.0.13 回归修复：UiKit.Squircle 返回的是已 Freeze 的 StreamGeometry，
        // 1.0.12 在它上面再赋值 .Transform 会抛 InvalidOperationException（对象只读），
        // 导致所有 WidgetChrome 构造失败 → 组件库预览全挂、添加到桌面/启动恢复组件全失败。
        // 冻结的 Geometry 可以直接共享给 Clip，无需（也不能）再改。
        view.Clip = geo;
        view.UseLayoutRounding = false;
        view.SnapsToDevicePixels = false;
        RenderOptions.SetEdgeMode(view, EdgeMode.Unspecified);
        Children.Add(view);
    }

    /// <summary>按当前 View.BackgroundBrush / Outline / ChromeShadow 刷新底色。</summary>
    public void SyncFromView()
    {
        _bg.Fill = View.BackgroundBrush;
        if (View.Outline)
        {
            _bg.Stroke = UiKit.B("#000000", 0.06);
            _bg.StrokeThickness = 0.75;
        }
        else
        {
            // 无描边时仍留极淡描边参与 AA，避免纯色硬边锯齿
            _bg.Stroke = UiKit.B("#000000", 0.02);
            _bg.StrokeThickness = 0.5;
        }
        _bg.Effect = View.ChromeShadow ? UiKit.Shadow() : null;
    }
}
