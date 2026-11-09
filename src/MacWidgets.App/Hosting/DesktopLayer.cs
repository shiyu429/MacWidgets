using MacWidgets.Data;
using MacWidgets.Interop;
using static MacWidgets.Interop.Native;

namespace MacWidgets.Hosting;

/// <summary>
/// 桌面层（1.0.8）：
/// 仅保留置底模式。监听前台窗口，Win+D / 显示桌面时通知 Host 把组件重新置底并确保可见。
/// 永久移除 WorkerW 嵌入（SetParent），避免桌面图标被遮挡/组件消失。
/// </summary>
public static class DesktopLayer
{
    private static IntPtr _hook;
    private static WinEventProc? _proc;
    public static bool DesktopForeground { get; private set; }
    public static event Action<bool>? DesktopForegroundChanged;

    public static void Start()
    {
        if (_hook != IntPtr.Zero) return;
        _proc = OnForeground;
        _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    public static void Stop()
    {
        if (_hook != IntPtr.Zero) UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }

    private static void OnForeground(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            var cls = ClassName(hwnd);
            bool desk = cls is "WorkerW" or "Progman";
            if (IsOwnWindow(hwnd)) return;
            if (desk != DesktopForeground)
            {
                DesktopForeground = desk;
                DesktopForegroundChanged?.Invoke(desk);
            }
        }
        catch (Exception ex) { Store.Log("Foreground hook: " + ex.Message); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    private static bool IsOwnWindow(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid == (uint)Environment.ProcessId;
    }
}
