using System.Runtime.InteropServices;
using System.Text;

namespace MacWidgets.Interop;

/// <summary>手写 P/Invoke（未使用 CsWin32，便于在 Linux 上交叉编译）。仅支持 64 位。</summary>
public static class Native
{
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    public const long WS_CHILD = 0x40000000L;
    public const long WS_POPUP = 0x80000000L;
    public const long WS_EX_TOPMOST = 0x00000008L;
    public const long WS_EX_TRANSPARENT = 0x00000020L;
    public const long WS_EX_TOOLWINDOW = 0x00000080L;
    public const long WS_EX_APPWINDOW = 0x00040000L;
    public const long WS_EX_LAYERED = 0x00080000L;
    public const long WS_EX_NOACTIVATE = 0x08000000L;

    public static readonly IntPtr HWND_TOP = IntPtr.Zero;
    public static readonly IntPtr HWND_BOTTOM = new(1);
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_NOOWNERZORDER = 0x0200;
    public const uint SWP_HIDEWINDOW = 0x0080;

    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const int WM_DISPLAYCHANGE = 0x007E;
    public const int WM_SETTINGCHANGE = 0x001A;
    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int MA_NOACTIVATE = 3;
    public const int WM_POINTERDOWN = 0x0246;
    public const int WM_POINTERUPDATE = 0x0245;
    public const int WM_POINTERUP = 0x0247;
    public const int WM_POINTERCAPTURECHANGED = 0x024C;
    public static bool IsPointerMessage(int msg) => msg is WM_POINTERDOWN or WM_POINTERUPDATE or WM_POINTERUP or WM_POINTERCAPTURECHANGED;
    public static POINT PointerPoint(IntPtr lParam)
    {
        long v = lParam.ToInt64();
        return new POINT { X = (short)(v & 0xffff), Y = (short)((v >> 16) & 0xffff) };
    }
    public static bool PointerIsPrimary(IntPtr wParam) => (((wParam.ToInt64() >> 16) & 0x2000) != 0);
    public static bool PointerIsTouch(IntPtr wParam) => (((wParam.ToInt64() >> 16) & 0x000f) == 2);

    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_LBUTTON = 0x01;

    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MONITORINFOF_PRIMARY = 1;

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    public const uint LWA_ALPHA = 0x2;
    public const uint SMTO_NORMAL = 0x0;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    public const int WH_MOUSE_LL = 14;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_NCLBUTTONDOWN = 0x00A1;
    public const int WM_NCRBUTTONDOWN = 0x00A4;

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    public delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT pt);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder buf, int max);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmod, WinEventProc proc, uint idProcess, uint idThread, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? cls, string? title);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr child, IntPtr parent);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte alpha, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    public static void EnableWindowPointerMessages(IntPtr hwnd)
    {
        // Windows 7 API returns false on newer systems when pointer input is already enabled.
        // Per-Monitor V2 desktop apps receive WM_POINTER natively; legacy WM_TOUCH is deliberately not enabled.
        EnableMouseInPointer(true);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableMouseInPointer(bool fEnable);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    public static long GetExStyle(IntPtr h) => GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
    public static void SetExStyle(IntPtr h, long v) => SetWindowLongPtr(h, GWL_EXSTYLE, new IntPtr(v));
    public static long GetStyle(IntPtr h) => GetWindowLongPtr(h, GWL_STYLE).ToInt64();
    public static void SetStyle(IntPtr h, long v) => SetWindowLongPtr(h, GWL_STYLE, new IntPtr(v));

    /// <summary>
    /// 透明分层窗口必须禁止 DWM 系统圆角与系统背景（Acrylic/Mica）。
    /// DWMWCP_ROUND 会在 AllowsTransparency 窗口外画一层浅色半透明矩形底板，正是桌面组件外围方框的根因。
    /// </summary>
    public static void RemovePopupBorder(IntPtr hwnd)
    {
        try
        {
            // DWMWA_BORDER_COLOR = 34, DWMWA_COLOR_NONE = 0xFFFFFFFE
            uint none = 0xFFFFFFFE;
            DwmSetWindowAttribute(hwnd, 34, ref none, sizeof(uint));
            // DWMWA_CAPTION_COLOR = 35
            DwmSetWindowAttribute(hwnd, 35, ref none, sizeof(uint));
            // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_DONOTROUND = 1
            int donotRound = 1;
            DwmSetWindowAttribute(hwnd, 33, ref donotRound, sizeof(int));
            // DWMWA_SYSTEMBACKDROP_TYPE = 38, DWMSBT_NONE = 1
            int backdropNone = 1;
            DwmSetWindowAttribute(hwnd, 38, ref backdropNone, sizeof(int));
        }
        catch { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static bool KeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    public static string ClassName(IntPtr h)
    {
        var sb = new StringBuilder(256);
        return GetClassName(h, sb, sb.Capacity) > 0 ? sb.ToString() : "";
    }

    public static POINT Cursor()
    {
        GetCursorPos(out var p);
        return p;
    }
}
