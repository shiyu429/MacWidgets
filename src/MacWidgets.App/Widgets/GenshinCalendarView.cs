using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MacWidgets.Data;
using MacWidgets.Services;
using MacWidgets.UI;

namespace MacWidgets.Widgets;

/// <summary>原神活动日历：沉浸显示官网活动日历大图。</summary>
public sealed class GenshinCalendarView : WidgetView
{
    private readonly Grid _root = new();
    private bool _hasImage;

    public override Brush BackgroundBrush => _hasImage ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(0x1A, 0x1F, 0x2E));
    public override bool Outline => false;
    public override bool ChromeShadow => !_hasImage;

    public GenshinCalendarView(WidgetInstance inst, bool preview) : base(inst, preview)
    {
        Content = _root;
        ShowPlaceholder("正在获取原神活动日历…");
    }

    protected override void OnAttach()
    {
        GenshinCalendarHub.Updated += OnUpdated;
        OnUpdated();
    }

    protected override void OnDetach() => GenshinCalendarHub.Updated -= OnUpdated;

    public override void Refresh() => OnUpdated();

    public override IEnumerable<MenuEntry> MenuItems() =>
    [
        MenuEntry.Item("立即刷新活动日历", GenshinCalendarHub.RefreshNow),
        MenuEntry.Item("打开原神官网", () =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://ys.mihoyo.com/") { UseShellExecute = true });
            }
            catch { /* ignore */ }
        }),
    ];

    private void OnUpdated()
    {
        var snap = GenshinCalendarHub.Get();
        if (snap.ImagePath != null && File.Exists(snap.ImagePath))
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bi.UriSource = new Uri(snap.ImagePath, UriKind.Absolute);
                bi.DecodePixelWidth = 2000;
                bi.EndInit();
                bi.Freeze();

                _hasImage = true;
                _root.Children.Clear();
                // edge-to-edge + bleed，与照片组件一致
                const double bleed = 2;
                var img = new Image
                {
                    Source = new CroppedBitmap(bi, new Int32Rect(0,0,bi.PixelWidth,(int)(bi.PixelHeight*.53))),
                    Stretch = Stretch.Fill,
                    Width = DW + bleed * 2,
                    Height = DH + bleed * 2,
                    Margin = new Thickness(-bleed),
                    // 官网日历图上部活动日程区域已裁出，铺满与“计划”一致的 356×170 卡片。
                    SnapsToDevicePixels = false,
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                _root.Children.Add(img);
                SyncChrome();
                ToolTip = "原神活动日历" + (snap.FetchedAt is DateTime t ? $"\n更新于 {t:MM-dd HH:mm}" : "") +
                          (snap.ImageUrl != null ? "\n" + snap.ImageUrl : "") +
                          "\n数据来源：ys.mihoyo.com 版本落地页";
                return;
            }
            catch (Exception ex)
            {
                Store.Log("Genshin image decode: " + ex.Message);
            }
        }

        _hasImage = false;
        string msg = snap.Loading ? "正在获取原神活动日历…"
            : snap.Error != null ? "活动日历暂不可用\n" + snap.Error
            : "暂无活动日历图片";
        ShowPlaceholder(msg);
        SyncChrome();
    }

    private void ShowPlaceholder(string msg)
    {
        _root.Children.Clear();
        var sp = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(16),
        };
        var title = UiKit.T("原神活动日历", 15, FontWeights.SemiBold, UiKit.White);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(title);
        var body = UiKit.T(msg, 12, FontWeights.Normal, UiKit.B("#FFFFFF", 0.75));
        body.TextWrapping = TextWrapping.Wrap;
        body.TextAlignment = TextAlignment.Center;
        body.Margin = new Thickness(0, 8, 0, 0);
        body.MaxWidth = DW - 40;
        sp.Children.Add(body);
        _root.Children.Add(sp);
        ToolTip = msg;
    }
}
