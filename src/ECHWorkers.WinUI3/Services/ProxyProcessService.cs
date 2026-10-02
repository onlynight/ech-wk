using System.Diagnostics;
using System.IO;
using System.Text;
using ECHWorkers.WinUI3.Models;

namespace ECHWorkers.WinUI3.Services;

/// <summary>
/// 管理 Go 核心代理进程（ech-workers.exe）的生命周期，
/// 启动时根据 ServerProfile 构造命令行参数，停止时终止进程树。
/// </summary>
public sealed class ProxyProcessService
{
    private Process? _process;
    private CancellationTokenSource? _cts;

    /// <summary>标准输出/错误日志输出事件。</summary>
    public event Action<string>? LogReceived;

    /// <summary>进程退出事件。</summary>
    public event Action<int>? Exited;

    public bool IsRunning => _process is { HasExited: false };

    /// <summary>查找 ech-workers.exe，优先在应用程序目录中查找。</summary>
    private static string FindBinary()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "ech-workers.exe"),
            Path.Combine(baseDir, "..", "ech-workers.exe"),
            Path.Combine(baseDir, "..", "..", "ech-workers.exe"),
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path)) return Path.GetFullPath(path);
        }
        return Path.Combine(baseDir, "ech-workers.exe");
    }

    /// <summary>将 UI 分流模式映射为 Go 程序的分流参数。</summary>
    private static string MapRouting(string routing) => routing switch
    {
        "global" => "global",
        "bypass_cn" => "bypass_cn",
        "none" => "none",
        _ => "bypass_cn",
    };

    /// <summary>
    /// 兜底：清理后台残留的 ech-workers 进程（如上次异常退出未清理、或手动启动的实例），
    /// 避免监听端口被占用导致新实例启动失败。返回清理掉的进程数。
    /// </summary>
    private static int KillOrphanProcesses()
    {
        var killed = 0;
        try
        {
            foreach (var p in Process.GetProcessesByName("ech-workers"))
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                    killed++;
                }
                catch { /* 已退出或无权限，忽略 */ }
                finally
                {
                    p.Dispose();
                }
            }
        }
        catch { }
        return killed;
    }

    public async Task<bool> Start(ServerProfile profile)
    {
        if (IsRunning)
        {
            LogReceived?.Invoke("[错误] 代理服务已在运行。");
            return false;
        }

        var binary = FindBinary();
        if (!File.Exists(binary))
        {
            LogReceived?.Invoke($"[错误] 未找到代理程序: {binary}");
            LogReceived?.Invoke("[错误] 请将 ech-workers.exe 放置在应用程序目录中。");
            return false;
        }

        // 兜底：启动前清掉后台残留的 ech-workers，再拉起新实例
        var cleaned = KillOrphanProcesses();
        if (cleaned > 0)
        {
            LogReceived?.Invoke($"[启动] 检测到 {cleaned} 个后台残留的代理进程，已先停止。");
            // 稍等端口释放
            await Task.Delay(300);
        }

        _cts = new CancellationTokenSource();

        var args = new List<string>();

        if (!string.IsNullOrWhiteSpace(profile.Listen))
            args.Add($"-l {profile.Listen}");

        if (!string.IsNullOrWhiteSpace(profile.Server))
            args.Add($"-f {profile.Server}");

        if (!string.IsNullOrWhiteSpace(profile.Token))
            args.Add($"-token {profile.Token}");

        if (!string.IsNullOrWhiteSpace(profile.Ip))
            args.Add($"-ip {profile.Ip}");

        if (!string.IsNullOrWhiteSpace(profile.Dns) && profile.Dns != "dns.alidns.com/dns-query")
            args.Add($"-dns {profile.Dns}");

        if (!string.IsNullOrWhiteSpace(profile.Ech) && profile.Ech != "cloudflare-ech.com")
            args.Add($"-ech {profile.Ech}");

        args.Add($"-routing {MapRouting(profile.Routing)}");

        var startInfo = new ProcessStartInfo
        {
            FileName = binary,
            Arguments = string.Join(" ", args),
            WorkingDirectory = Path.GetDirectoryName(binary)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            // Go 程序以 UTF-8 输出日志，必须显式指定编码，否则会按系统 ANSI（GBK）解码出乱码
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                LogReceived?.Invoke(e.Data);
                AppendConsoleLog(e.Data);
            }
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                LogReceived?.Invoke(e.Data);
                AppendConsoleLog(e.Data);
            }
        };
        _process.Exited += (_, _) =>
        {
            var exitCode = _process?.ExitCode ?? -1;
            Exited?.Invoke(exitCode);
        };

        try
        {
            _process.Start();
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke($"[错误] 启动代理失败: {ex.Message}");
            return false;
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        LogReceived?.Invoke($"[启动] 代理服务已启动，监听 {profile.Listen}");
        LogReceived?.Invoke($"[启动] 后端: {profile.Server}，分流: {profile.Routing}");

        _ = Task.Run(async () =>
        {
            await _process.WaitForExitAsync(_cts.Token);
        }, _cts.Token);

        return true;
    }

    public void Stop()
    {
        if (!IsRunning) return;

        try
        {
            if (_process != null)
            {
                // 先尝试发送 Ctrl+C 等价信号（通过 stdin 关闭）
                _process.StandardInput?.Close();
                if (!_process.WaitForExit(3000))
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            _process?.Dispose();
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke($"[错误] 停止代理失败: {ex.Message}");
        }
        finally
        {
            _process = null;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            LogReceived?.Invoke("[停止] 代理服务已停止。");
        }
    }

    private static void AppendConsoleLog(string line)
    {
        try { System.Console.WriteLine(line); } catch { }
    }
}
