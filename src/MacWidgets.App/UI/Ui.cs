using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacWidgets.UI;

/// <summary>主界面/对话框的小工具方法（代码构建界面，Fluent 主题提供控件样式）。</summary>
internal static class Ui
{
    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI");

    public static void ApplyTheme(Window w)
    {
        w.FontFamily = Font;
        try { w.ThemeMode = ThemeMode.Light; }
        catch (Exception ex) { MacWidgets.Data.Store.Log("Fluent 主题不可用：" + ex.Message); }
    }

    public static TextBlock H1(string t) => new() { Text = t, FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) };
    public static TextBlock H2(string t) => new() { Text = t, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 6) };
    public static TextBlock P(string t, double size = 13, Brush? fg = null) => new() { Text = t, FontSize = size, TextWrapping = TextWrapping.Wrap, Foreground = fg ?? Brushes.Black, Margin = new Thickness(0, 2, 0, 2) };
    public static readonly Brush Gray = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x73));

    public static Button Btn(string text, Action click, bool accent = false)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(14, 8, 14, 9), MinWidth = 72, MinHeight = 44 };
        if (accent)
        {
            try { b.SetResourceReference(FrameworkElement.StyleProperty, "AccentButtonStyle"); } catch { }
        }
        b.Click += (_, _) => click();
        return b;
    }

    public static CheckBox Check(string text, bool value, Action<bool> changed)
    {
        var c = new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 4, 0, 4) };
        c.Checked += (_, _) => changed(true);
        c.Unchecked += (_, _) => changed(false);
        return c;
    }

    public static StackPanel Row(params UIElement[] children)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        foreach (var c in children) sp.Children.Add(c);
        return sp;
    }

    public static TextBlock Label(string t, double width = 120) => new() { Text = t, Width = width, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 };

    public static Border Card(UIElement child, double padding = 16) => new()
    {
        Background = Brushes.White,
        CornerRadius = new CornerRadius(10),
        BorderBrush = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(padding),
        Margin = new Thickness(0, 0, 0, 12),
        Child = child,
    };
}
