using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MacWidgets.Data;

namespace MacWidgets.UI;

/// <summary>新建 / 编辑提醒事项（标题 + 可选日期 + 可选时间）。计划组件新建时必须填写日期时间。</summary>
public sealed class ReminderDialog : Window
{
    private readonly ReminderItem? _item;
    private readonly bool _requireDate;
    private readonly TextBox _title = new() { FontSize = 14, Padding = new Thickness(6, 4, 6, 4) };
    private readonly CheckBox _hasDate = new() { Content = "日期" };
    private readonly DatePicker _date = new() { Width = 160 };
    private readonly CheckBox _hasTime = new() { Content = "时间" };
    private readonly ComboBox _hour = new() { Width = 70 };
    private readonly ComboBox _minute = new() { Width = 70 };

    public static void ShowNew(bool requireDate)
    {
        var d = new ReminderDialog(null, requireDate);
        d.Show();
        d.Activate();
    }

    public static void ShowEdit(ReminderItem r)
    {
        var d = new ReminderDialog(r, false);
        d.Show();
        d.Activate();
    }

    private ReminderDialog(ReminderItem? item, bool requireDate)
    {
        _item = item;
        _requireDate = requireDate;
        Title = item == null ? "新建提醒事项" : "编辑提醒事项";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        Ui.ApplyTheme(this);

        for (int h = 0; h < 24; h++) _hour.Items.Add(h.ToString("00"));
        for (int m = 0; m < 60; m += 5) _minute.Items.Add(m.ToString("00"));

        var now = DateTime.Now;
        var nextHour = now.Date.AddHours(now.Hour + 1);
        var due = item?.Due ?? (requireDate ? nextHour : (DateTime?)null);
        _title.Text = item?.Title ?? "";
        _hasDate.IsChecked = due != null;
        _date.SelectedDate = (due ?? DateTime.Today).Date;
        bool hasTime = item?.HasTime ?? requireDate;
        _hasTime.IsChecked = hasTime && due != null;
        var t = due ?? nextHour;
        _hour.SelectedIndex = t.Hour;
        int mi = t.Minute / 5;
        _minute.SelectedIndex = Math.Clamp(mi, 0, 11);
        if (t.Minute % 5 != 0)
        {
            _minute.Items.Add(t.Minute.ToString("00"));
            _minute.SelectedItem = t.Minute.ToString("00");
        }

        var sp = new StackPanel { Margin = new Thickness(20) };
        sp.Children.Add(new TextBlock { Text = "标题", Margin = new Thickness(0, 0, 0, 4) });
        sp.Children.Add(_title);
        sp.Children.Add(Ui.Row(_hasDate, new Border { Width = 12 }, _date));
        sp.Children.Add(Ui.Row(_hasTime, new Border { Width = 12 }, _hour, new TextBlock { Text = " : ", VerticalAlignment = VerticalAlignment.Center }, _minute));
        if (requireDate)
            sp.Children.Add(Ui.P("“计划”中的提醒需要设置日期和时间。", 12, Ui.Gray));

        var buttons = new DockPanel { Margin = new Thickness(0, 16, 0, 0), LastChildFill = false };
        var save = Ui.Btn("保存", Save, accent: true);
        var cancel = Ui.Btn("取消", Close);
        DockPanel.SetDock(save, Dock.Right);
        DockPanel.SetDock(cancel, Dock.Right);
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        if (item != null)
        {
            var del = Ui.Btn("删除", () => { ReminderStore.Delete(item.Id); Close(); });
            DockPanel.SetDock(del, Dock.Left);
            buttons.Children.Add(del);
        }
        sp.Children.Add(buttons);
        Content = sp;

        _hasDate.Checked += (_, _) => Sync();
        _hasDate.Unchecked += (_, _) => Sync();
        _hasTime.Checked += (_, _) => { _hasDate.IsChecked = true; Sync(); };
        _hasTime.Unchecked += (_, _) => Sync();
        Sync();
        Loaded += (_, _) => { _title.Focus(); _title.SelectAll(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !(Keyboard.FocusedElement is DatePickerTextBox)) { e.Handled = true; Save(); }
            if (e.Key == Key.Escape) { e.Handled = true; Close(); }
        };
    }

    private void Sync()
    {
        _date.IsEnabled = _hasDate.IsChecked == true;
        _hour.IsEnabled = _minute.IsEnabled = _hasTime.IsChecked == true;
    }

    private void Save()
    {
        var title = _title.Text.Trim();
        if (title.Length == 0) { _title.Focus(); return; }
        DateTime? due = null;
        bool hasTime = false;
        if (_hasDate.IsChecked == true)
        {
            var d = (_date.SelectedDate ?? DateTime.Today).Date;
            if (_hasTime.IsChecked == true)
            {
                int h = int.Parse((string)_hour.SelectedItem!);
                int m = int.Parse((string)(_minute.SelectedItem ?? "00"));
                d = d.AddHours(h).AddMinutes(m);
                hasTime = true;
            }
            due = d;
        }
        if (_requireDate && (due == null || !hasTime))
        {
            MessageBox.Show(this, "请设置日期和时间。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_item == null)
        {
            ReminderStore.Add(title, due, hasTime);
        }
        else
        {
            _item.Title = title;
            _item.Due = due;
            _item.HasTime = hasTime;
            ReminderStore.Update(_item);
        }
        Close();
    }
}
