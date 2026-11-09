using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacWidgets.Data;
using MacWidgets.Services;
using MacWidgets.UI;

namespace MacWidgets.Widgets;

/// <summary>提醒事项（小号）：顶栏“今天”+数量，列表可勾选；悬停“+”内联新建，回车保存/Esc 取消。</summary>
public sealed class RemindersView : WidgetView
{
    private readonly TextBlock _title, _count, _more;
    private readonly StackPanel _list = new();
    private readonly Border _plus;
    private TextBox? _input;

    public RemindersView(WidgetInstance inst, bool preview) : base(inst, preview)
    {
        var root = new Grid { Margin = new Thickness(16, 12, 16, 10) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Height = 28 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _title = UiKit.T("今天", 17, FontWeights.Bold, UiKit.Blue);
        _title.VerticalAlignment = VerticalAlignment.Center;
        _count = UiKit.T("0", 20, FontWeights.Bold, Brushes.Black);
        _count.VerticalAlignment = VerticalAlignment.Center;
        _count.Margin = new Thickness(4, 0, 0, 0);
        Grid.SetColumn(_count, 2);
        _plus = ReminderRows.PlusButton(UiKit.Blue, BeginInlineAdd);
        _plus.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_plus, 1);
        header.Children.Add(_title);
        header.Children.Add(_plus);
        header.Children.Add(_count);
        root.Children.Add(header);

        _list.Margin = new Thickness(0, 6, 0, 0);
        Grid.SetRow(_list, 1);
        root.Children.Add(_list);

        _more = UiKit.T("", 11, FontWeights.SemiBold, UiKit.Secondary);
        _more.Margin = new Thickness(29, 0, 0, 0);
        Grid.SetRow(_more, 2);
        root.Children.Add(_more);

        // 透明背景以便悬停显示“+”
        var host = new Grid { Background = Brushes.Transparent };
        host.Children.Add(root);
        if (!preview)
        {
            host.MouseEnter += (_, _) => _plus.Opacity = 1;
            host.MouseLeave += (_, _) => { if (_input == null) _plus.Opacity = 0; };
        }
        Content = host;
    }

    protected override void OnAttach()
    {
        ReminderStore.Changed += Rebuild;
        AppClock.DayChanged += Rebuild;
        Rebuild();
    }

    protected override void OnDetach()
    {
        ReminderStore.Changed -= Rebuild;
        AppClock.DayChanged -= Rebuild;
    }

    public override void Refresh() => Rebuild();

    public override IEnumerable<MenuEntry> MenuItems() =>
    [
        Item("新建提醒…", () => ReminderDialog.ShowNew(requireDate: false)),
        Item("清除已完成", ReminderStore.ClearCompleted),
    ];

    private IReadOnlyList<ReminderItem> Items() =>
        Instance.Options.ListMode == ReminderListMode.All ? ReminderStore.AllOpen() : ReminderStore.Today();

    private void Rebuild()
    {
        var items = Items();
        _title.Text = Instance.Options.ListMode == ReminderListMode.All ? "提醒事项" : "今天";
        _count.Text = items.Count.ToString();
        _list.Children.Clear();

        double avail = DH - 12 - 10 - 28 - 6 - 14;
        int maxRows = Math.Max(1, (int)Math.Floor(avail / ReminderRows.RowHeight));
        if (_input != null)
        {
            _list.Children.Add(InputRow());
            maxRows--;
        }
        int shown = 0;
        foreach (var r in items)
        {
            if (shown >= maxRows) break;
            _list.Children.Add(ReminderRows.Build(r, Instance.Size != WidgetSize.S && r.Due?.Date != DateTime.Today, Edit, IsPreview));
            shown++;
        }
        int rest = items.Count - shown;
        _more.Text = rest > 0 ? $"+{rest} 项" : "";
        if (items.Count == 0 && _input == null)
        {
            var empty = UiKit.T(IsPreview ? "没有提醒事项" : "没有提醒事项，点 + 新建", 12, FontWeights.Normal, UiKit.Secondary);
            empty.Margin = new Thickness(0, 6, 0, 0);
            _list.Children.Add(empty);
        }
    }

    private void Edit(ReminderItem r) => ReminderDialog.ShowEdit(r);

    private FrameworkElement InputRow()
    {
        var row = new Grid { Height = ReminderRows.RowHeight };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(29) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var circle = new CheckCircle(19) { IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        row.Children.Add(circle);
        Grid.SetColumn(_input!, 1);
        if (_input!.Parent is Panel p) p.Children.Remove(_input);
        row.Children.Add(_input);
        return row;
    }

    private void BeginInlineAdd()
    {
        if (IsPreview || _input != null) return;
        _input = new TextBox
        {
            FontSize = 13,
            FontFamily = UiKit.Text,
            Foreground = UiKit.Label,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            FocusVisualStyle = null,
        };
        _input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; CommitInline(true); }
            else if (e.Key == Key.Escape) { e.Handled = true; CommitInline(false); }
        };
        _input.LostKeyboardFocus += (_, _) => CommitInline(true);
        Rebuild();
        Context?.BeginTextInput();
        var tb = _input;
        Dispatcher.BeginInvoke(() =>
        {
            tb.Focus();
            Keyboard.Focus(tb);
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void CommitInline(bool save)
    {
        if (_input == null) return;
        var text = _input.Text?.Trim() ?? "";
        _input = null;
        _plus.Opacity = 0;
        Context?.EndTextInput();
        if (save && text.Length > 0)
        {
            DateTime? due = Instance.Options.ListMode == ReminderListMode.Today ? DateTime.Today : null;
            ReminderStore.Add(text, due, false);
        }
        else
        {
            Rebuild();
        }
    }
}
