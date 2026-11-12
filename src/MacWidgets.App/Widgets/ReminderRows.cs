using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MacWidgets.Data;
using MacWidgets.UI;
using Shapes = System.Windows.Shapes;

namespace MacWidgets.Widgets;

/// <summary>提醒事项行（勾选圆圈 + 标题 + 右侧时间/时钟图标），小号与计划组件共用。</summary>
public static class ReminderRows
{
    public const double RowHeight = 30;

    public static FrameworkElement Build(ReminderItem r, bool showTimeText, Action<ReminderItem> onEdit, bool preview)
    {
        var row = new Grid { Height = RowHeight, Background = Brushes.Transparent };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(29) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var circle = new CheckCircle(19) { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        row.Children.Add(circle);

        var title = UiKit.T(r.Title, 13, FontWeights.Normal, UiKit.Label);
        title.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(title, 1);
        row.Children.Add(title);

        if (showTimeText && !ReminderStore.IsSeedSample(r))
        {
            var t = UiKit.T(ReminderStore.FormatDue(r), 13, FontWeights.Normal, UiKit.Secondary);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.Margin = new Thickness(8, 0, 0, 0);
            System.Windows.Documents.Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular);
            Grid.SetColumn(t, 2);
            row.Children.Add(t);
        }
        else if (r.HasTime && !ReminderStore.IsSeedSample(r))
        {
            var icon = new Shapes.Path { Data = Icons.ClockSmall, Stroke = UiKit.Secondary, StrokeThickness = 1.1, Width = 12, Height = 12, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), ToolTip = ReminderStore.FormatDue(r) };
            Grid.SetColumn(icon, 2);
            row.Children.Add(icon);
        }

        if (preview)
        {
            circle.IsHitTestVisible = false;
            return row;
        }

        circle.Checked += () =>
        {
            // 0.6 秒后淡出并标记完成
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
                fade.Completed += (_, _) => ReminderStore.Complete(r.Id);
                row.BeginAnimation(UIElement.OpacityProperty, fade);
            };
            t.Start();
        };

        title.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { e.Handled = true; onEdit(r); }
        };

        row.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            PopupMenu.ShowAt(row, [
                MenuEntry.Item("编辑提醒…", () => onEdit(r)),
                MenuEntry.Item("标记为完成", () => ReminderStore.Complete(r.Id)),
                MenuEntry.Item("删除提醒", () => ReminderStore.Delete(r.Id)),
            ], e);
        };
        return row;
    }

    /// <summary>悬停时显示的“+”按钮。</summary>
    public static Border PlusButton(Brush color, Action click)
    {
        var b = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = "新建提醒",
            Child = new Shapes.Path { Data = Icons.Plus, Stroke = color, StrokeThickness = 1.8, Width = 11, Height = 11, Stretch = Stretch.Uniform, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Opacity = 0,
        };
        b.MouseEnter += (_, _) => b.Background = UiKit.B("#000000", 0.06);
        b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        b.MouseLeftButtonDown += (_, e) => { e.Handled = true; click(); };
        return b;
    }
}
