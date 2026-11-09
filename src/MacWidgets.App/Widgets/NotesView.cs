using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MacWidgets.Data;
using MacWidgets.UI;
using Shapes = System.Windows.Shapes;

namespace MacWidgets.Widgets;

/// <summary>备忘录：黄色顶栏 + 点状虚线 + 正文；双击进入编辑，失焦/Esc 退出并保存（防抖 500ms）。</summary>
public sealed class NotesView : WidgetView
{
    private static readonly Brush Header = MakeHeader();
    private static Brush MakeHeader()
    {
        // 对照参考：鲜明金黄 #FFD60A，顶部略亮、底部略沉，过渡柔和不脏不断层
        var b = new LinearGradientBrush();
        b.StartPoint = new Point(0.5, 0);
        b.EndPoint = new Point(0.5, 1);
        b.GradientStops.Add(new GradientStop(UiKit.C("#FFE34A"), 0));
        b.GradientStops.Add(new GradientStop(UiKit.C("#FFD60A"), 0.55));
        b.GradientStops.Add(new GradientStop(UiKit.C("#F5C400"), 1));
        b.Freeze();
        return b;
    }

    private readonly TextBox _box;
    private readonly TextBlock _placeholder;
    private readonly Border _overlay;
    private readonly DispatcherTimer _save;
    private bool _editing;

    public NotesView(WidgetInstance inst, bool preview) : base(inst, preview)
    {
        var root = new Grid { Background = Brushes.White };
        // 内外都使用同一几何裁切，黄头/正文不会覆盖组件库与桌面圆角。
        root.Clip = UiKit.Squircle(DW, DH, CardRadius);
        // 干净黄头：无折角；圆角由外层 WidgetChrome squircle 裁切；顶部轻高光更自然
        var header = new Border { Height = 44, VerticalAlignment = VerticalAlignment.Top, Background = Header };
        header.Child = new Border
        {
            Height = 18,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new LinearGradientBrush(
                Color.FromArgb(55, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255), 90),
        };
        root.Children.Add(header);
        root.Children.Add(new Shapes.Line
        {
            X1 = 6, X2 = DW - 6, Y1 = 50.5, Y2 = 50.5,
            Stroke = UiKit.B("#D1D1D6"),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection([0, 2.7]),
            StrokeDashCap = PenLineCap.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        });

        _box = new TextBox
        {
            Margin = new Thickness(14, 62, 14, 12),
            FontSize = 13,
            FontFamily = UiKit.Text,
            Foreground = UiKit.Label,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            AcceptsReturn = true,
            AcceptsTab = false,
            TextWrapping = TextWrapping.Wrap,
            IsReadOnly = true,
            Focusable = false,
            IsHitTestVisible = false,
            FocusVisualStyle = null,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Text = inst.Options.NoteText ?? "",
            IsUndoEnabled = true,
        };
        TextOptions.SetTextFormattingMode(_box, TextFormattingMode.Ideal);
        _placeholder = UiKit.T(preview ? "备忘录" : "双击输入备忘录", 13, FontWeights.Normal, UiKit.B("#808080"));
        _placeholder.Margin = new Thickness(16, 62, 16, 0);
        _placeholder.VerticalAlignment = VerticalAlignment.Top;
        _placeholder.IsHitTestVisible = false;
        root.Children.Add(_placeholder);
        root.Children.Add(_box);

        _overlay = new Border { Background = Brushes.Transparent, Margin = new Thickness(0, 44, 0, 0) };
        root.Children.Add(_overlay);
        Content = root;

        _save = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _save.Tick += (_, _) => { _save.Stop(); Save(); };
        _box.TextChanged += (_, _) =>
        {
            UpdatePlaceholder();
            if (_editing) { _save.Stop(); _save.Start(); }
        };
        UpdatePlaceholder();

        if (!preview)
        {
            _overlay.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount == 2) { e.Handled = true; BeginEdit(); }
            };
            _box.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; EndEdit(); }
            };
            _box.LostKeyboardFocus += (_, _) => EndEdit();
            DataObject.AddPastingHandler(_box, (_, e) =>
            {
                // 只粘贴纯文本
                if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
                {
                    var text = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? "";
                    var d = new DataObject();
                    d.SetData(DataFormats.UnicodeText, text);
                    e.DataObject = d;
                }
                else e.CancelCommand();
            });
        }
    }

    private void UpdatePlaceholder() =>
        _placeholder.Visibility = string.IsNullOrEmpty(_box.Text) && !_editing ? Visibility.Visible : Visibility.Collapsed;

    public override IEnumerable<MenuEntry> MenuItems() => [Item("编辑文字", BeginEdit)];

    public override void Refresh()
    {
        if (!_editing) _box.Text = Instance.Options.NoteText ?? "";
    }

    public void BeginEdit()
    {
        if (IsPreview || _editing) return;
        _editing = true;
        _overlay.Visibility = Visibility.Collapsed;
        _box.IsReadOnly = false;
        _box.Focusable = true;
        _box.IsHitTestVisible = true;
        _box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        UpdatePlaceholder();
        Context?.BeginTextInput();
        Dispatcher.BeginInvoke(() =>
        {
            _box.Focus();
            Keyboard.Focus(_box);
            _box.CaretIndex = _box.Text.Length;
        }, DispatcherPriority.Input);
    }

    public void EndEdit()
    {
        if (!_editing) return;
        _editing = false;
        _save.Stop();
        Save();
        _box.IsReadOnly = true;
        _box.Focusable = false;
        _box.IsHitTestVisible = false;
        _box.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        _box.ScrollToHome();
        _overlay.Visibility = Visibility.Visible;
        UpdatePlaceholder();
        Context?.EndTextInput();
    }

    private void Save()
    {
        Instance.Options.NoteText = _box.Text;
        Context?.SaveInstance();
    }
}
