# Windows 原生客户端改造方案

> 目标：用 Windows 原生 UI 重写当前 PyQt5 客户端，保持 `ech-workers.go` 作为本地代理内核不变。

## 1. 结论

**推荐方案：.NET 8 + WPF + ModernWpf + CommunityToolkit.Mvvm + Hardcodet.NotifyIcon.Wpf。**

原因：
- 当前机器缺少 Visual Studio / MSVC / WebView2 Runtime，不适合 WinUI3 或 WebView2。
- WPF 是 Windows 桌面原生框架，安装依赖最少。
- 可以做到类 ChatGPT 的深色现代 UI。
- 能自然迁移现有 PyQt5 的功能：配置、托盘、开机自启、系统代理、日志、进程管理。
- 不重写 Go 代理核心，风险最低。

## 2. 当前环境结论

已检测到：
- Git
- Node / npm / pnpm
- Python
- VS Code

未检测到：
- .NET SDK
- Visual Studio 实例
- MSBuild
- MSVC / cl.exe
- Windows SDK
- Edge / WebView2 Runtime

因此，当前机器的“立即可开发、风险最低”的原生方案是：**先补 .NET SDK，再写 WPF**。

## 3. 推荐技术栈

- .NET 8 LTS
- WPF
- C#
- MVVM
- CommunityToolkit.Mvvm
- ModernWpf
- Hardcodet.NotifyIcon.Wpf
- JSON 配置持久化

## 4. 架构方案

```
ech-workers.go                核心代理引擎，保持不变
ECHWorkers.Client/            新 Windows 原生 UI
  - ViewModels/
  - Views/
  - Services/
  - Models/
  - Assets/
```

## 5. UI 方案

风格参考：
- 深色主题
- 左侧导航
- 右侧内容区
- 大圆角按钮
- 卡片式信息展示
- 顶部状态胶囊
- 实时日志终端样式

### 主界面结构

```
Sidebar
- 新建服务器
- 服务器列表
- 设置
- 关于

Main Content
- 当前节点卡片
- 启动/停止代理大按钮
- 运行状态
- 日志面板
```

## 6. 功能迁移映射

| 原 PyQt5 功能 | 新 WPF 实现 |
|---|---|
| 多服务器配置 | ServerStore + JSON |
| 当前节点切换 | ServerViewModel |
| 启动/停止代理 | ProcessService |
| 实时日志 | ObservableCollection + Dispatcher |
| 系统代理 | SystemProxyService |
| 系统托盘 | NotifyIcon |
| 开机自启 | AutoStartService |
| 高 DPI | WPF 原生支持 |
| 配置持久化 | JSON |
| 分流模式 | 透传 Go 参数 |

## 7. 建议项目结构

```
src/
  ECHWorkers.Client/
    App.xaml
    MainWindow.xaml
    ViewModels/
    Views/
    Services/
    Models/
    Assets/
```

## 8. 迁移路径

1. 新建 WPF 客户端目录
2. 迁移配置模型与进程管理
3. 实现主界面和服务器列表
4. 实现托盘、系统代理、开机自启
5. 逐步替换 Python GUI
6. 更新 CI 发布产物

## 9. 当前限制

当前机器未检测到 .NET SDK，因此：
- 可以先创建项目骨架
- 但暂时不能在本机直接构建 WPF 项目

需要安装 .NET SDK 后，才能完成编译验证。
