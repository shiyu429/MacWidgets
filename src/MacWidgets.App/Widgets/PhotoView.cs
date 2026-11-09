using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MacWidgets.Data;
using MacWidgets.UI;

namespace MacWidgets.Widgets;

/// <summary>照片：占位（花朵图标 + “相簿无可用照片”）或自定义图片 edge-to-edge 沉浸铺满。</summary>
public sealed class PhotoView : WidgetView
{
    public PhotoView(WidgetInstance inst, bool preview) : base(inst, preview)
    {
        Refresh();
    }

    public override bool AcceptsFileDrop => true;

    private bool HasPhoto
    {
        get
        {
            var path = PhotoPath(Instance);
            return path != null && File.Exists(path);
        }
    }

    /// <summary>有照片时透明底、无描边、无阴影：只显示被 squircle 裁切的图片。</summary>
    public override Brush BackgroundBrush => HasPhoto ? Brushes.Transparent : Brushes.White;
    public override bool Outline => !HasPhoto;
    public override bool ChromeShadow => !HasPhoto;

    public override void Refresh()
    {
        var path = PhotoPath(Instance);
        if (path != null && File.Exists(path))
        {
            Content = PhotoRenderer.Build(path, Instance.Options, DW, DH);
            SyncChrome();
            return;
        }
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        // 1.0.8：空状态使用图5的 macOS 相册图标（已做成圆角方形，嵌入资源）
        double iconSize = Instance.Kind == WidgetKind.PhotoWide ? 56 : 58;
        sp.Children.Add(BuildPhotosLogo(iconSize));
        var t = UiKit.T("相簿无可用照片", Instance.Kind == WidgetKind.PhotoWide ? 12 : 11, FontWeights.SemiBold, UiKit.B("#6E6E73"));
        t.HorizontalAlignment = HorizontalAlignment.Center;
        t.Margin = new Thickness(0, 8, 0, 0);
        sp.Children.Add(t);
        var g = new Grid { Background = Brushes.Transparent };
        g.Children.Add(sp);
        if (!IsPreview)
        {
            g.ToolTip = "双击或右键“选择照片…”添加图片，也可以把图片文件拖到这里";
            g.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount == 2) { e.Handled = true; Choose(); }
            };
        }
        Content = g;
        SyncChrome();
    }

    private static FrameworkElement BuildPhotosLogo(double size)
    {
        var border = new Border
        {
            Width = size, Height = size,
            CornerRadius = new CornerRadius(size * 0.22),
            ClipToBounds = true,
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            var stream = typeof(PhotoView).Assembly.GetManifestResourceStream("MacWidgets.photos-logo.png");
            if (stream == null) throw new InvalidOperationException("photos-logo resource missing");
            bi.StreamSource = stream;
            bi.DecodePixelWidth = (int)(size * 3);
            bi.EndInit();
            bi.Freeze();
            border.Child = new Image { Source = bi, Stretch = Stretch.UniformToFill };
        }
        catch
        {
            border.Child = new FlowerIcon(size * 0.9) { HorizontalAlignment = HorizontalAlignment.Center };
        }
        return border;
    }

    public override IEnumerable<MenuEntry> MenuItems()
    {
        var list = new List<MenuEntry> { Item("选择照片…", Choose) };
        if (Instance.Options.PhotoFile != null)
        {
            list.Add(Item("调整照片…", () => PhotoAdjustDialog.Open(Instance, this)));
            list.Add(Item("移除照片", () =>
            {
                Instance.Options.PhotoFile = null;
                Context?.SaveInstance();
                Refresh();
            }));
        }
        return list;
    }

    public void Choose()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择照片",
            Filter = "图片|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.heic;*.heif;*.tif;*.tiff|所有文件|*.*",
        };
        if (dlg.ShowDialog() == true) OnFileDropped(dlg.FileName);
    }

    public override void OnFileDropped(string path)
    {
        try
        {
            Import(Instance, path);
            Context?.SaveInstance();
            Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show("无法添加图片：" + ex.Message, "照片", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public static string? PhotoPath(WidgetInstance inst) =>
        inst.Options.PhotoFile == null ? null : Path.Combine(Store.PhotosDir, inst.Id, inst.Options.PhotoFile);

    /// <summary>把图片复制到数据目录（原图被删也不影响），并重置裁剪参数。</summary>
    public static void Import(WidgetInstance inst, string src)
    {
        using (var fs = File.OpenRead(src))
        {
            var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (dec.Frames.Count == 0) throw new InvalidOperationException("不是有效的图片");
        }
        var dir = Path.Combine(Store.PhotosDir, inst.Id);
        Directory.CreateDirectory(dir);
        string hash;
        using (var fs = File.OpenRead(src)) hash = Convert.ToHexString(SHA1.HashData(fs))[..12].ToLowerInvariant();
        var name = hash + Path.GetExtension(src).ToLowerInvariant();
        var dest = Path.Combine(dir, name);
        if (!File.Exists(dest)) File.Copy(src, dest);
        foreach (var f in Directory.GetFiles(dir))
            if (!string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase))
                try { File.Delete(f); } catch { }
        var o = inst.Options;
        o.PhotoFile = name;
        o.PhotoZoom = 1;
        o.PhotoOffsetX = o.PhotoOffsetY = 0;
        o.PhotoRotation = 0;
    }
}

public static class PhotoRenderer
{
    public static BitmapSource? Load(string path, int decodeWidth)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bi.UriSource = new Uri(path, UriKind.Absolute);
            if (decodeWidth > 0) bi.DecodePixelWidth = decodeWidth;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch (Exception ex)
        {
            Store.Log("Photo load: " + ex.Message);
            return null;
        }
    }

    private static int DecodeWidthFor(string path, double w, double zoom)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            int pw = dec.Frames[0].PixelWidth;
            int want = (int)Math.Min(3000, w * 3 * Math.Max(1, zoom));
            return pw > want ? want : 0;
        }
        catch { return 1600; }
    }

    /// <summary>
    /// edge-to-edge：图片铺满设计矩形，外层 WidgetChrome squircle 裁切；
    /// 略放大 bleed 盖住 AA 边缘，杜绝白/黑环。
    /// </summary>
    public static FrameworkElement Build(string path, WidgetOptions o, double w, double h)
    {
        var grid = new Grid
        {
            Width = w,
            Height = h,
            ClipToBounds = false,
            Background = Brushes.Transparent,
            Margin = new Thickness(0),
            SnapsToDevicePixels = false,
            UseLayoutRounding = false,
        };

        var src = Load(path, DecodeWidthFor(path, w, o.PhotoZoom));
        if (src == null)
        {
            var t = UiKit.T("图片无法显示", 12, FontWeights.Normal, UiKit.Secondary);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(t);
            return grid;
        }

        double ar = src.PixelHeight > 0 ? (double)src.PixelWidth / src.PixelHeight : 1;
        double coverScale = Math.Max(w / ar, h);
        double fitScale = Math.Min(w / ar, h);
        double ih = o.PhotoFit ? fitScale : coverScale;
        double iw = ih * ar;

        // 适应模式用极深近黑填缝（避免露桌面），覆盖模式完全透明
        if (o.PhotoFit)
            grid.Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A));

        // 放大约 2 DIP，彻底盖住 squircle AA 边缘
        const double bleed = 2.0;
        double drawW = iw + bleed * 2;
        double drawH = ih + bleed * 2;

        var canvas = new Canvas { Width = w, Height = h, ClipToBounds = false };
        var img = new Image
        {
            Source = src,
            Stretch = Stretch.Fill,
            Width = drawW,
            Height = drawH,
            RenderTransformOrigin = new Point(0.5, 0.5),
            SnapsToDevicePixels = false,
            Margin = new Thickness(0),
        };
        Canvas.SetLeft(img, (w - drawW) / 2);
        Canvas.SetTop(img, (h - drawH) / 2);
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        RenderOptions.SetEdgeMode(img, EdgeMode.Unspecified);
        img.RenderTransform = MakeTransform(o, w, h);
        canvas.Children.Add(img);
        grid.Children.Add(canvas);
        return grid;
    }

    public static Transform MakeTransform(WidgetOptions o, double w, double h)
    {
        var tg = new TransformGroup();
        tg.Children.Add(new ScaleTransform(o.PhotoZoom, o.PhotoZoom));
        tg.Children.Add(new RotateTransform(o.PhotoRotation));
        tg.Children.Add(new TranslateTransform(o.PhotoOffsetX * w, o.PhotoOffsetY * h));
        return tg;
    }
}
