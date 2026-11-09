using System.Runtime.InteropServices;
using MacWidgets.Interop;

namespace MacWidgets.UI;

/// <summary>无第三方依赖的 DWM Acrylic；不支持时保留 PopupMenu 的半透明渐变玻璃兜底。</summary>
internal static class GlassBackdrop
{
    private enum AccentState { Disabled=0, Gradient=1, TransparentGradient=2, BlurBehind=3, AcrylicBlurBehind=4 }
    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy { public AccentState AccentState; public int AccentFlags; public uint GradientColor; public int AnimationId; }
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }

    public static void TryEnable(IntPtr hwnd)
    {
        try
        {
            // ABGR: low-opacity dark tint; transparent corners are rendered by WPF, no grey backing rectangle.
            var accent = new AccentPolicy { AccentState=AccentState.AcrylicBlurBehind, AccentFlags=2, GradientColor=0x4021242A };
            int size=Marshal.SizeOf(accent);
            var ptr=Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent,ptr,false);
                var data=new WindowCompositionAttributeData { Attribute=19, Data=ptr, SizeOfData=size };
                if (SetWindowCompositionAttribute(hwnd,ref data)==0) EnableFallbackBlur(hwnd);
            }
            finally { Marshal.FreeHGlobal(ptr); }
            // Windows 11: rounded native popup corners where supported.
            int preference=2;
            DwmSetWindowAttribute(hwnd,33,ref preference,Marshal.SizeOf<int>());
        }
        catch { EnableFallbackBlur(hwnd); }
    }

    private static void EnableFallbackBlur(IntPtr hwnd)
    {
        try { int t=1; DwmSetWindowAttribute(hwnd,20,ref t,Marshal.SizeOf<int>()); } catch { }
    }
    [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(IntPtr hwnd,ref WindowCompositionAttributeData data);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
}
