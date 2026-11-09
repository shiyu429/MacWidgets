using System.Windows;
using MacWidgets.Data;
using MacWidgets.Interop;
using MacWidgets.Widgets;
using WF = System.Windows.Forms;

namespace MacWidgets.UI;

/// <summary>托盘图标：左键显示主界面；右键弹出与组件一致的现代深色 PopupMenu（无「添加小组件」）。</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WF.NotifyIcon _icon;

    public TrayIcon()
    {
        _icon = new WF.NotifyIcon
        {
            Text = "MacWidgets 桌面小组件",
            Icon = LoadIcon(),
            Visible = true,
            // 不用 WinForms 原生菜单，改走自绘 PopupMenu
            ContextMenuStrip = null,
        };
        _icon.MouseUp += OnMouseUp;
    }

    private void OnMouseUp(object? sender, WF.MouseEventArgs e)
    {
        if (e.Button == WF.MouseButtons.Left)
        {
            App.Current?.Dispatcher.BeginInvoke(App.ShowMainWindow);
            return;
        }
        if (e.Button != WF.MouseButtons.Right) return;

        // NotifyIcon 的右键在 WinForms 线程；切到 WPF 调度器弹 PopupMenu
        Native.GetCursorPos(out var pt);
        var sp = new Point(pt.X, pt.Y);
        App.Current?.Dispatcher.BeginInvoke(() =>
        {
            PopupMenu.Show(BuildEntries(), sp);
        });
    }

    private static List<MenuEntry> BuildEntries()
    {
        var list = new List<MenuEntry>
        {
            MenuEntry.Item("显示主界面", App.ShowMainWindow),
            MenuEntry.Check("锁定布局", Store.Settings.LayoutLocked, () => App.Host.SetLayoutLocked(!Store.Settings.LayoutLocked)),
            MenuEntry.Item("重置位置", () => App.Host.ArrangeLikeReference()),
            MenuEntry.Sep(),
            MenuEntry.Item("退出", App.Quit),
        };
        return list;
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            using var s = typeof(TrayIcon).Assembly.GetManifestResourceStream("MacWidgets.app.ico");
            if (s != null) return new System.Drawing.Icon(s, WF.SystemInformation.SmallIconSize);
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    /// <summary>编辑模式切换后调用；菜单每次打开现读 Store，无需缓存勾选态。</summary>
    public void Sync() { }

    /// <summary>资源管理器重启后重新显示图标。</summary>
    public void Recreate()
    {
        _icon.Visible = false;
        _icon.Visible = true;
    }

    public void Balloon(string text)
    {
        try { _icon.ShowBalloonTip(4000, "MacWidgets", text, WF.ToolTipIcon.Info); } catch { }
    }

    public void Dispose()
    {
        _icon.MouseUp -= OnMouseUp;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
