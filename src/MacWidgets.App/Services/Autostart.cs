using Microsoft.Win32;

namespace MacWidgets.Services;

/// <summary>开机自启：HKCU\Software\Microsoft\Windows\CurrentVersion\Run。</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "MacWidgets";

    public static bool IsEnabled()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(Name) is string;
        }
        catch { return false; }
    }

    public static void Set(bool enable)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enable)
        {
            var exe = Environment.ProcessPath ?? "";
            k.SetValue(Name, $"\"{exe}\" --autostart");
        }
        else
        {
            k.DeleteValue(Name, false);
        }
    }
}
