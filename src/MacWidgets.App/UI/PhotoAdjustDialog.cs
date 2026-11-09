using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacWidgets.Data;
using MacWidgets.Widgets;

namespace MacWidgets.UI;

/// <summary>调整照片：预览与组件同比例，拖动平移、滚轮/滑块缩放（1×–5×）、旋转 90°、填充/适应、重置。</summary>
public sealed class PhotoAdjustDialog : Window
{
    private readonly WidgetInstance _inst;
    private readonly PhotoView _view;
    private readonly WidgetOptions _work;
    private readonly Border _stage;
    private readonly Slider _zoom;
    private readonly double _pw, _ph;
    private bool _dragging;
    private Point _last;

    public static void Open(WidgetInstance inst, PhotoView view)
    {
        var d = new PhotoAdjustDialog(inst, view);
        d.Show();
        d.Activate();
    }

    private PhotoAdjustDialog(WidgetInstance inst, PhotoView view)
    {
        _inst = inst;
        _view = view;
        var o = inst.Options;
        _work = new WidgetOptions
        {
            PhotoFile = o.PhotoFile,
            PhotoZoom = o.PhotoZoom,
            PhotoOffsetX = o.PhotoOffsetX,
            PhotoOffsetY = o.PhotoOffsetY,
            PhotoRotation = o.PhotoRotation,
            PhotoFit = o.PhotoFit,
        };
        Title = "调整照片";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.ApplyTheme(this);

        var d = Catalog.DesignSize(inst.Size);
        double k = Math.Min(420 / d.Width, 360 / d.Height);
        _pw = d.Width * k;
        _ph = d.Height * k;

        _stage = new Border
        {
            Width = _pw,
            Height = _ph,
            CornerRadius = new CornerRadius(WidgetView.CardRadius * k),
            ClipToBounds = true,
            Cursor = Cursors.SizeAll,
            Background = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _stage.Clip = UiKit.Squircle(_pw, _ph, WidgetView.CardRadius * k);
        _stage.MouseLeftButtonDown += (_, e) => { _dragging = true; _last = e.GetPosition(_stage); _stage.CaptureMouse(); };
        _stage.MouseMove += (_, e) =>
        {
            if (!_dragging) return;
            var p = e.GetPosition(_stage);
            _work.PhotoOffsetX += (p.X - _last.X) / _pw;
            _work.PhotoOffsetY += (p.Y - _last.Y) / _ph;
            _last = p;
            Render();
        };
        _stage.MouseLeftButtonUp += (_, _) => { _dragging = false; _stage.ReleaseMouseCapture(); };
        _stage.MouseWheel += (_, e) => { _zoom!.Value = Math.Clamp(_zoom.Value + (e.Delta > 0 ? 0.1 : -0.1), 1, 5); };

        _zoom = new Slider { Minimum = 1, Maximum = 5, Value = _work.PhotoZoom, Width = 240, VerticalAlignment = VerticalAlignment.Center };
        _zoom.ValueChanged += (_, _) => { _work.PhotoZoom = _zoom.Value; Render(); };

        var fit = new CheckBox { Content = "完整显示（适应，不裁剪）", IsChecked = _work.PhotoFit, Margin = new Thickness(0, 4, 0, 4) };
        fit.Checked += (_, _) => { _work.PhotoFit = true; Render(); };
        fit.Unchecked += (_, _) => { _work.PhotoFit = false; Render(); };

        var sp = new StackPanel { Margin = new Thickness(20) };
        sp.Children.Add(Ui.P("拖动画面平移，滚轮或滑块缩放。", 12, Ui.Gray));
        sp.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(0xF6, 0xE0, 0xCB)), Padding = new Thickness(16), CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 8, 0, 8), Child = _stage });
        sp.Children.Add(Ui.Row(Ui.Label("缩放", 60), _zoom));
        sp.Children.Add(fit);
        sp.Children.Add(Ui.Row(
            Ui.Btn("向右旋转 90°", () => { _work.PhotoRotation = (_work.PhotoRotation + 90) % 360; Render(); }),
            Ui.Btn("重置", () => { _work.PhotoZoom = 1; _work.PhotoOffsetX = _work.PhotoOffsetY = 0; _work.PhotoRotation = 0; _zoom.Value = 1; Render(); })));

        var buttons = new DockPanel { Margin = new Thickness(0, 16, 0, 0), LastChildFill = false };
        var ok = Ui.Btn("完成", Commit, accent: true);
        var cancel = Ui.Btn("取消", Close);
        DockPanel.SetDock(ok, Dock.Right);
        DockPanel.SetDock(cancel, Dock.Right);
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        sp.Children.Add(buttons);
        Content = sp;
        Render();
    }

    private void Render()
    {
        var path = PhotoView.PhotoPath(_inst);
        if (path == null) return;
        // 预览按与组件相同的相对参数渲染（平移为比例，故与尺寸无关）
        _stage.Child = PhotoRenderer.Build(path, _work, _pw, _ph);
    }

    private void Commit()
    {
        var o = _inst.Options;
        o.PhotoZoom = _work.PhotoZoom;
        o.PhotoOffsetX = _work.PhotoOffsetX;
        o.PhotoOffsetY = _work.PhotoOffsetY;
        o.PhotoRotation = _work.PhotoRotation;
        o.PhotoFit = _work.PhotoFit;
        _view.Refresh();
        Store.SaveLayoutSoon();
        Close();
    }
}
