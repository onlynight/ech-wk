# ECHWorkers.WinUI3

ECH Workers 的 Windows 原生客户端，基于 WinUI3 构建（Mica 云母背景、亮暗主题跟随系统），替代旧版 Python GUI（`gui.py` 保留用于 macOS/Linux）。

## 功能

- **首页** — 启动/停止代理、实时日志、一键系统代理（全局 / 跳过中国大陆 / 直连）
- **服务器管理** — 多服务器配置，弹窗新增/编辑，快速切换
- **设置** — 开机自启动、关闭时最小化到托盘
- **系统托盘** — 图标右下角状态圆点（绿=代理运行中，灰=已停止）；左键打开主窗口，右键菜单（打开主窗口 / 启动或暂停代理 / 退出）；Windows 11 自动显示在任务栏可见区域
- 高 DPI（PerMonitorV2）、标题栏与内容合并、窗口最小尺寸限制

## 技术栈

- .NET 8 (`net8.0-windows10.0.19041.0`)
- WinUI3 / Windows App SDK
- 托盘：Win32 P/Invoke（`Shell_NotifyIcon`）
- System.Drawing.Common（托盘图标绘制）
- YamlDotNet（配置读写）

## 目录结构

```
Pages/            HomePage / ServersPage / SettingsPage / AboutPage + AddServerDialog
Services/
  ProxyProcessService    拉起/停止 ech-workers.exe（Go 核心，构建时自动复制到输出目录）
  ServerConfigService    服务器配置读写
  SystemProxyService     系统代理设置与清理
  AppSettingsService     应用设置（开机自启、关闭到托盘），settings.json
  SystemTrayService      系统托盘图标（Win32 Shell_NotifyIcon 封装）
Themes/           主题样式 XAML
```

## 构建

要求：.NET 8 SDK、Windows App SDK；Go 核心需先编译到仓库根目录（`go build -o ech-workers ech-workers.exe`），构建时自动复制进输出目录。

```bash
# Debug
dotnet build -c Debug

# Release 发布
dotnet publish -c Release
```

- Debug 产物：`bin/Debug/net8.0-windows10.0.19041.0/`
- Release 产物：`bin/Release/net8.0-windows10.0.19041.0/publish/`（仓库根目录的 `release.zip` 即此目录打包）

csproj 内含两个自定义 publish Target，勿删：

- `CopyXamlArtifactsToPublish` — 复制 `.xbf` / `.pri` 到发布目录（缺失会导致启动即崩）
- `CopyNuGetDllsToPublish` — 复制 `System.Drawing.Common.dll` 等被 publish 流程丢弃的程序集（缺失会导致托盘图标无法绘制）

分发时保持发布目录所有文件在同一目录即可，无其他运行时依赖。

## 托盘实现要点（踩坑记录）

`SystemTrayService` 直接 P/Invoke `Shell_NotifyIconW`，以下问题都实际踩过：

- **NOTIFYICONDATAW 必须是完整的 976 字节布局（x64）**：`dwStateMask` 字段不能漏、`szInfoTitle` 是内嵌 `WCHAR[64]` 而非指针、`guidItem` 是 16 字节 GUID。缺任何一个都会让后续字段整体错位（表现为 tooltip 乱码）。
- **图标必须从 exe 内嵌资源提取**（`Icon.ExtractAssociatedIcon(Environment.ProcessPath)`）。不能用 `Assembly.Location` —— 那指向托管 DLL（无图标资源），加载失败被吞掉后 `HICON=0`，注册出一个空图标。
- **.NET 8 上 `GCHandle.Alloc(Icon)` 抛 `ArgumentException`**，HICON 直接用 `DestroyIcon` 管理，不要套 GCHandle。
- **Win11 默认把新托盘图标藏进溢出区(^)**：注册成功后向 `HKCU\Control Panel\NotifyIconSettings\<条目>`（按 `ExecutablePath` 匹配）写入 `IsPromoted=1`，图标才会出现在任务栏可见区域。
- 排障：程序目录下的 `tray_error.log` 记录托盘初始化/修改失败详情。

## 系统要求

Windows 10 19041+ / Windows 11（推荐，完整支持 Mica 与托盘常驻）。
