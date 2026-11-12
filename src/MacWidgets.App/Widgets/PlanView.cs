using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MacWidgets.Data;
using MacWidgets.Services;
using MacWidgets.UI;
using Shapes = System.Windows.Shapes;

namespace MacWidgets.Widgets;

/// <summary>提醒事项·计划（中号）：左侧红色日历图标 + 数量 + “计划”，右侧按时间排序的已安排事项。</summary>
public sealed class PlanView : WidgetView
{
    private readonly TextBlock _count;
    private readonly StackPanel _list = new();
    private readonly TextBlock _more;
    private readonly TextBlock _empty;
    private readonly Border _plus;

    public PlanView(WidgetInstance inst, bool preview) : base(inst, preview)
    {
        var root = new Grid { Background = Brushes.Transparent };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DW * 0.35) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // 左列
        var left = new Grid { Margin = new Thickness(16, 16, 0, 14) };
        var icon = new Grid { Width = 34, Height = 34, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var calBg = new RadialGradientBrush(UiKit.C("#FF5A5F"), UiKit.C("#E0242A"))
        {
            GradientOrigin = new Point(0.35, 0.3),
            Center = new Point(0.4, 0.35),
            RadiusX = 0.75,
            RadiusY = 0.75,
        };
        calBg.Freeze();
        icon.Children.Add(new Shapes.Ellipse { Fill = calBg });
        icon.Children.Add(new Shapes.Path { Data = Icons.CalendarFrame, Stroke = Brushes.White, StrokeThickness = 1.6, Width = 17, Height = 15, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        var dots = new Canvas { Width = 17, Height = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 4; c++)
            {
                var d = new Shapes.Rectangle { Width = 2, Height = 2, Fill = Brushes.White, RadiusX = 0.5, RadiusY = 0.5 };
                Canvas.SetLeft(d, 3 + c * 3.2);
                Canvas.SetTop(d, 7 + r * 3.2);
                dots.Children.Add(d);
            }
        icon.Children.Add(dots);
        left.Children.Add(icon);

        var bottom = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
        _count = UiKit.T("0", 28, FontWeights.Bold, UiKit.Label);
        _count.Margin = new Thickness(0, 0, 0, -3);
        var label = UiKit.T("计划", 15, FontWeights.SemiBold, UiKit.Red);
        bottom.Children.Add(_count);
        bottom.Children.Add(label);
        left.Children.Add(bottom);
        root.Children.Add(left);

        // 右列
        var right = new Grid { Margin = new Thickness(0, 12, 16, 10) };
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.Children.Add(_list);
        // 1.0.25：空状态文字在右侧区域内水平、垂直居中
        _empty = UiKit.T("没有已安排的提醒", 12, FontWeights.Normal, UiKit.Secondary);
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.VerticalAlignment = VerticalAlignment.Center;
        _empty.TextAlignment = TextAlignment.Center;
        _empty.Visibility = Visibility.Collapsed;
        Grid.SetRowSpan(_empty, 2);
        right.Children.Add(_empty);
        var foot = new Grid();
        _more = UiKit.T("", 11, FontWeights.SemiBold, UiKit.Secondary);
        _more.Margin = new Thickness(29, 0, 0, 0);
        _more.VerticalAlignment = VerticalAlignment.Center;
        _plus = ReminderRows.PlusButton(UiKit.Red, () => ReminderDialog.ShowNew(requireDate: true));
        _plus.HorizontalAlignment = HorizontalAlignment.Right;
        foot.Children.Add(_more);
        foot.Children.Add(_plus);
        Grid.SetRow(foot, 1);
        right.Children.Add(foot);
        Grid.SetColumn(right, 1);
        root.Children.Add(right);

        if (!preview)
        {
            root.MouseEnter += (_, _) => _plus.Opacity = 1;
            root.MouseLeave += (_, _) => _plus.Opacity = 0;
        }
        Content = root;
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
        Item("新建计划提醒…", () => ReminderDialog.ShowNew(requireDate: true)),
        Item("清除已完成", ReminderStore.ClearCompleted),
    ];

    private void Rebuild()
    {
        var items = ReminderStore.Scheduled();
        _count.Text = items.Count.ToString();
        _list.Children.Clear();
        double avail = DH - 12 - 10 - 20;
        int maxRows = Math.Max(1, (int)Math.Floor(avail / ReminderRows.RowHeight));
        int shown = 0;
        foreach (var r in items)
        {
            if (shown >= maxRows) break;
            _list.Children.Add(ReminderRows.Build(r, true, ReminderDialog.ShowEdit, IsPreview));
            shown++;
        }
        _more.Text = items.Count > shown ? $"+{items.Count - shown} 项" : "";
        _empty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
