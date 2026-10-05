using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace ECHWorkers.WinUI3.Services;

/// <summary>
/// 应用设置持久化服务：开机自启动、关闭后保留托盘状态。
/// 配置文件路径：%APPDATA%\ECHWorkersClient\settings.json
/// </summary>
public static class AppSettingsService
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ECHWorkersClient");

    private static readonly string SettingsPath =
        Path.Combine(ConfigDir, "settings.json");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    public static bool StartOnLogin { get; private set; }
    public static bool MinimizeToTrayOnClose { get; private set; }

    public static void SetMinimizeToTrayOnClose(bool enabled)
    {
        MinimizeToTrayOnClose = enabled;
        Save();
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettingsData>(json, _jsonOptions);
                if (settings != null)
                {
                    StartOnLogin = settings.StartOnLogin;
                    MinimizeToTrayOnClose = settings.MinimizeToTrayOnClose;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] 加载设置失败: {ex.Message}");
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var settings = new AppSettingsData
            {
                StartOnLogin = StartOnLogin,
                MinimizeToTrayOnClose = MinimizeToTrayOnClose,
            };
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            var tmpPath = SettingsPath + ".tmp";
            File.WriteAllText(tmpPath, json);

            if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
            File.Move(tmpPath, SettingsPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] 保存设置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 设置开机自启动：在 HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run 中写入/删除注册表项。
    /// </summary>
    public static void SetStartOnLogin(bool enabled)
    {
        StartOnLogin = enabled;
        Save();

        try
        {
            const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    // 用引号包裹路径，防止路径含空格
                    key.SetValue("ECHWorkers", $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue("ECHWorkers", throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] 设置开机自启动失败: {ex.Message}");
        }
    }
}

internal class AppSettingsData
{
    public bool StartOnLogin { get; set; }
    public bool MinimizeToTrayOnClose { get; set; }
}
