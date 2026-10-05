using System.Runtime.InteropServices;

namespace ECHWorkers.WinUI3.Services;

/// <summary>
/// 系统托盘（通知区）图标：最小化到托盘 + 右键菜单（打开 / 启动或暂停代理 / 退出）。
/// 通过 P/Invoke 实现，因为 Windows 通知区图标的 API 不在 WinUI3 公共 API 中。
/// </summary>
public sealed class SystemTrayService : IDisposable
{
    // ============ Win32 P/Invoke ============
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(
        int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern ushort RegisterClassW(ref WndClass wc);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr prc);
    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenuW(IntPtr menu, uint flags, IntPtr id, string text);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool ChangeWindowMessageFilterEx(
        IntPtr hwnd, uint msg, uint action, IntPtr filterData);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW data);

    [StructLayout(LayoutKind.Sequential)] struct WndClass
    {
        public uint Style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    // x64 布局：cbSize@0 hWnd@8 uID@16 uFlags@20 uCallbackMessage@24 hIcon@32
    //   szTip[128]@40 dwState@296 dwStateMask@300 szInfo[256]@304
    //   uVersion@816 szInfoTitle[64]@820 dwInfoFlags@948 guidItem@952 hBalloonIcon@968，共 976。
    // 注意：dwStateMask 不能少；szInfoTitle 是内嵌 WCHAR[64] 而非指针；guidItem 是 16 字节 GUID。
    // 缺任何一个都会让后续字段整体错位（tooltip 变乱码、气泡错乱）。
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct NOTIFYICONDATAW
    {
        public int CbSize;
        public IntPtr Hwnd;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr HIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint DwcState;
        public uint DwcStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string SzInfo;
        public uint DwcTimeoutOrState;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string SzInfoTitle;
        public uint SzInfoTitleFlags;
        public System.Guid GuidItem;
        public IntPtr HBalloonIcon;
    }

    private static bool _isWin11;
    private const uint WM_USER = 0x0400;
    private const int SM_CXSMICON = 49;

    // ============ 常量 ============
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIM_SETVERSION = 0x00000004;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    private const uint WM_TRAYICON = WM_USER + 1;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_CONTEXTMENU = 0x007B;
    private const uint WM_COMMAND = 0x0111;

    private const uint MF_STRING = 0x0000;
    private const uint MF_SEPARATOR = 0x00000800;

    private const uint TPM_LEFTALIGN = 0x0000;
    private const uint TPM_RIGHTBUTTON = 0x0002;

    private const uint MSGFLT_ALLOW = 1;

    private const uint WM_POINTERWHEEL = 0x024E;
    private const uint WM_POINTERDOWN = 0x0246;

    private const int ICON_MENU_START = 0x100;
    private const int ICON_MENU_TOGGLE = 0x101;
    private const int ICON_MENU_EXIT = 0x102;

    // ============ 字段 ============
    private IntPtr _hwnd = IntPtr.Zero;
    // GetHicon 返回的 HICON 归调用方所有，必须用 DestroyIcon 释放。
    private IntPtr _hicon = IntPtr.Zero;
    private readonly WndProcDelegate _wndProc;
    private bool _isRunning;
    private bool _disposed;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>点击托盘图标（左键或右键之外的双击）时触发，用于"打开主窗口"。</summary>
    public event Action? IconActivated;

    /// <summary>右键菜单"启动/暂停代理"项被点击时触发。</summary>
    public event Action? ToggleProxyRequested;

    /// <summary>右键菜单"退出"项被点击时触发。</summary>
    public event Action? ExitRequested;

    public SystemTrayService(string tooltip)
    {
        _wndProc = WndProc;
        _isWin11 = IsWindows11OrGreater();

        var wc = new WndClass
        {
            Style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            lpszClassName = "ECHWorkers.Tray",
        };

        RegisterClassW(ref wc);
        // 创建隐藏的窗口承载托盘图标回调
        _hwnd = CreateWindowEx(
            0, "ECHWorkers.Tray", "ECH Workers Tray",
            0, 0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero) throw new InvalidOperationException("无法创建托盘承载窗口");

        // 允许托盘图标接收 WM_CONTEXTMENU 等被过滤的消息
        ChangeWindowMessageFilterEx(_hwnd, WM_CONTEXTMENU, MSGFLT_ALLOW, IntPtr.Zero);
        ChangeWindowMessageFilterEx(_hwnd, WM_POINTERWHEEL, MSGFLT_ALLOW, IntPtr.Zero);
        ChangeWindowMessageFilterEx(_hwnd, WM_POINTERDOWN, MSGFLT_ALLOW, IntPtr.Zero);

        // 初始图标（停止状态=灰点）
        _hicon = CreateStatusIcon(isRunning: false);
        _isRunning = false;

        var nid = BuildNotifyIconData(tooltip);
        if (!Shell_NotifyIconW(NIM_ADD, ref nid))
            Log($"NIM_ADD 失败 GetLastError={Marshal.GetLastWin32Error()}");

        nid = BuildNotifyIconData(tooltip);
        Shell_NotifyIconW(NIM_SETVERSION, ref nid);

        // Win11 默认把新图标藏进溢出区(^)，写入 IsPromoted 让它显示在可见区域
        if (_isWin11) PromoteTrayIcon();
    }

    /// <summary>切换代理状态指示（连接=绿色，断开=灰色）。</summary>
    public void SetStatus(bool isRunning, string? proxyText = null)
    {
        if (_disposed) return;
        _isRunning = isRunning;

        var tip = $"ECH Workers — {(isRunning ? "代理运行中" : "代理已停止")}" +
                  (proxyText is null ? "" : $" / {proxyText}");

        var old = _hicon;
        _hicon = CreateStatusIcon(isRunning);
        if (old != IntPtr.Zero) DestroyIcon(old);

        var nid = BuildNotifyIconData(tip);
        if (!Shell_NotifyIconW(NIM_MODIFY, ref nid))
            Log($"NIM_MODIFY 失败 GetLastError={Marshal.GetLastWin32Error()}");
    }

    /// <summary>显示主窗口（如果最小化则还原）。</summary>
    public void ShowMainWindow()
    {
        if (_hwnd == IntPtr.Zero) return;
        SetForegroundWindow(_hwnd);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hwnd != IntPtr.Zero)
        {
            var nid = BuildNotifyIconData(string.Empty);
            Shell_NotifyIconW(NIM_DELETE, ref nid);
        }

        if (_hicon != IntPtr.Zero)
        {
            DestroyIcon(_hicon);
            _hicon = IntPtr.Zero;
        }

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    // ============ 内部实现 ============

    private NOTIFYICONDATAW BuildNotifyIconData(string tip)
    {
        return new NOTIFYICONDATAW
        {
            CbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
            Hwnd = _hwnd,
            Id = 1,
            Flags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            CallbackMessage = WM_TRAYICON,
            HIcon = _hicon,
            Tip = tip,
        };
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYICON)
        {
            var mouseMsg = (uint)lParam.ToInt64() & 0xFFFF;
            if (mouseMsg == WM_RBUTTONUP || mouseMsg == WM_CONTEXTMENU)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
            if (mouseMsg == WM_LBUTTONUP)
            {
                IconActivated?.Invoke();
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        // 菜单命令返回：TrackPopupMenu 选中项时通过 WM_COMMAND 发送，菜单 ID 在 wParam 低 16 位
        if (msg == WM_COMMAND)
        {
            var cmd = wParam.ToInt32() & 0xFFFF;
            switch (cmd)
            {
                case ICON_MENU_START:
                    IconActivated?.Invoke();
                    break;
                case ICON_MENU_TOGGLE:
                    ToggleProxyRequested?.Invoke();
                    break;
                case ICON_MENU_EXIT:
                    ExitRequested?.Invoke();
                    break;
            }
            return IntPtr.Zero;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        GetCursorPos(out POINT pt);

        var menu = CreatePopupMenu();
        AppendMenuW(menu, MF_STRING, (IntPtr)ICON_MENU_START, "打开主窗口");
        AppendMenuW(menu, MF_STRING, (IntPtr)ICON_MENU_TOGGLE, _isRunning ? "暂停代理" : "启动代理");
        AppendMenuW(menu, MF_SEPARATOR, IntPtr.Zero, "");
        AppendMenuW(menu, MF_STRING, (IntPtr)ICON_MENU_EXIT, "退出");

        SetForegroundWindow(_hwnd);
        TrackPopupMenu(menu, TPM_LEFTALIGN | TPM_RIGHTBUTTON, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
        // TrackPopupMenu 返回后必须发送 WM_NULL 让菜单命令被处理，然后销毁菜单句柄
        PostMessage(_hwnd, 0x0000 /* WM_NULL */, IntPtr.Zero, IntPtr.Zero);
        PostMessage(_hwnd, 0x0000 /* WM_NULL */, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(menu);
    }

    /// <summary>
    /// 生成状态图标：应用图标 + 右下角状态圆点（运行=绿，停止=灰）。
    /// 尺寸取系统托盘小图标实际尺寸（随 DPI 变化），避免 256x256 大图被缩小后模糊。
    /// 返回的 HICON 由调用方负责 DestroyIcon。
    /// </summary>
    private IntPtr CreateStatusIcon(bool isRunning)
    {
        var size = GetSystemMetrics(SM_CXSMICON);
        if (size <= 0) size = 32;

        using var bmp = new System.Drawing.Bitmap(size, size);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.Clear(System.Drawing.Color.Transparent);

            var baseIcon = LoadApplicationIcon();
            try
            {
                // 兜底：系统应用图标（共享实例，ToBitmap 复制出来再绘制）
                using var src = baseIcon?.ToBitmap()
                    ?? System.Drawing.SystemIcons.Application.ToBitmap();
                g.DrawImage(src, 0, 0, size, size);
            }
            finally
            {
                baseIcon?.Dispose();
            }

            // 右下角状态圆点，尺寸随图标等比缩放
            var dot = Math.Max(4, size / 4);
            var margin = Math.Max(1, size / 16);
            var dotColor = isRunning
                ? System.Drawing.Color.FromArgb(255, 0x22, 0xC5, 0x5E)
                : System.Drawing.Color.FromArgb(255, 0x6B, 0x72, 0x80);
            using var dotBrush = new System.Drawing.SolidBrush(dotColor);
            g.FillEllipse(dotBrush, size - dot - margin, size - dot - margin, dot, dot);
        }

        return bmp.GetHicon();
    }

    /// <summary>
    /// 加载应用图标。注意不能用 Assembly.Location —— 那指向托管 DLL（无图标资源），
    /// 必须从 exe（&lt;ApplicationIcon&gt; 内嵌资源）提取，其次是 exe 旁的 app_icon.ico。
    /// </summary>
    private static System.Drawing.Icon? LoadApplicationIcon()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return null;

        try
        {
            if (System.IO.File.Exists(exe))
                return System.Drawing.Icon.ExtractAssociatedIcon(exe);
        }
        catch { }

        try
        {
            var icoPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(exe)!, "app_icon.ico");
            if (System.IO.File.Exists(icoPath))
                return new System.Drawing.Icon(icoPath, 32, 32);
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Win11 按图标把"是否显示在任务栏可见区"记录在
    /// HKCU\Control Panel\NotifyIconSettings\&lt;键&gt; 的 IsPromoted 值里（不设=藏进溢出区）。
    /// 键名是系统内部生成的 64 位数，这里按 ExecutablePath 匹配本应用条目后写入 IsPromoted=1。
    /// </summary>
    private static void PromoteTrayIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;

            using var root = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Control Panel\NotifyIconSettings", writable: true);
            if (root == null) return;

            foreach (var name in root.GetSubKeyNames())
            {
                using var sub = root.OpenSubKey(name, writable: true);
                var path = sub?.GetValue("ExecutablePath") as string;
                if (!string.Equals(path, exe, StringComparison.OrdinalIgnoreCase)) continue;

                sub!.SetValue("IsPromoted", 1, Microsoft.Win32.RegistryValueKind.DWord);
                return;
            }

            Log("NotifyIconSettings 中未找到本应用条目（首次注册可能延迟）");
        }
        catch (Exception ex)
        {
            Log("PromoteTrayIcon 失败: " + ex.Message);
        }
    }

    private static bool IsWindows11OrGreater()
    {
        try
        {
            var ver = Environment.OSVersion.Version;
            // Win11 = 10.0.x where build >= 22000
            if (ver.Major == 10 && ver.Minor == 0)
            {
                var build = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")
                    ?.GetValue("CurrentBuildNumber") as string;
                return !string.IsNullOrEmpty(build) && int.TryParse(build, out var b) && b >= 22000;
            }
        }
        catch { }
        return false;
    }

    /// <summary>把托盘相关错误写到 exe 旁的 tray_error.log，避免静默失败无从排查。</summary>
    private static void Log(string message)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Environment.ProcessPath);
            if (string.IsNullOrEmpty(dir)) return;
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(dir, "tray_error.log"),
                $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
        catch { }
    }
}
