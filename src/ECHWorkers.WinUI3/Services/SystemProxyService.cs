using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ECHWorkers.WinUI3.Services;

/// <summary>
/// Windows 系统代理管理：通过注册表 + WinINET 设置/清除系统代理。
/// </summary>
public static class SystemProxyService
{
    private const string ProxyKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_PROXY_CHANGED = 38;

    [DllImport("wininet.dll")]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int lpdwBufferLength);

    /// <summary>设置系统代理，指向 127.0.0.1:port。</summary>
    public static bool Enable(int port)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ProxyKeyPath, writable: true);
            key?.SetValue("ProxyServer", $"127.0.0.1:{port}", RegistryValueKind.String);
            key?.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
            key?.SetValue("ProxyOverride", "localhost;127.*;<local>", RegistryValueKind.String);

            // 通知 WinINET 设置已变更，使所有使用 WinINET 的应用（浏览器等）立即生效
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_PROXY_CHANGED, IntPtr.Zero, 0);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SystemProxy] 设置代理失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>清除系统代理设置。</summary>
    public static bool Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ProxyKeyPath, writable: true);
            key?.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);

            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_PROXY_CHANGED, IntPtr.Zero, 0);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SystemProxy] 清除代理失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>读取当前系统代理设置（用于启动时检测残留状态）。</summary>
    public static bool IsEnabled(out int port)
    {
        port = 0;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ProxyKeyPath, writable: false);
            if (key == null) return false;

            var enable = key.GetValue("ProxyEnable");
            if (enable is not int intEnable || intEnable != 1) return false;

            var server = key.GetValue("ProxyServer") as string;
            if (string.IsNullOrEmpty(server)) return false;

            var parts = server.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[^1], out var p))
            {
                port = p;
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
