# WinUI 3 客户端 UI 设计文档

> 描述 `src/ECHWorkers.WinUI3` 的界面结构、页面布局与交互设计。当前仅实现 UI 与交互（导航、跳转、开窗），业务功能（连接、持久化、系统代理等）后续补齐。
>
> 本版本为 **ChatGPT 风格重设计**：固定不收起的左侧栏、深色胶囊主按钮、圆角卡片、统一的视觉语言，整体追求 Windows 11 Mica 材质下的高级模糊质感。

## 1. 技术栈与窗口特性

- .NET 8 + WinUI 3 + Windows App SDK（包版本 `Microsoft.WindowsAppSDK 2.5.1`，实际解析到 WinUI `2.3.9`）。
- 无包部署（`WindowsPackageType=None`，`PublishWindowsAppPackage=false`）。
- 窗口采用 **Mica** 背景（`SystemBackdrop = new MicaBackdrop()`），模糊材质延伸至整个窗口，包括标题栏。
- 标题栏与窗口内容合并（`ExtendsContentIntoTitleBar = true`），标题栏按钮背景设为全透明，文字颜色随系统浅/深色主题切换（`ApplyCaptionColors`）。
- 窗口默认尺寸 1100×740（`AppWindow.Resize`），避免首次启动过窄导致内容裁剪。

### 1.1 SDK 2.3.9 的已知限制

本 SDK 版本的 XAML 编译器（Markup Compiler）**不支持独立资源字典中的 `ControlTemplate.Triggers` / `Style.Triggers`**，会报 `WMC0011: Unknown member 'Triggers'` / `WMC0001: Unknown type 'Trigger'`。

因此：
- 全局样式字典 `Themes/ThemeStyles.xaml` 只定义**静态外观**（背景、圆角、字号、间距），不写状态切换。
- 所有交互反馈（侧边栏悬停/选中、按钮按压缩放等）统一由**代码后置**处理。

## 2. 整体布局

`MainWindow` 是唯一的导航外壳，采用 **2 列 Grid**：左侧固定 264px 侧边栏 + 右侧弹性内容区。不再使用 `NavigationView`（其 `LeftMinimal` 模式会折叠收起，与"侧边常驻"的需求冲突），改为手写按钮实现精确可控的 ChatGPT 风格侧栏。

```
┌──────────────────────────────────────────────────────────────┐
│                                                              │  标题栏（Mica 透出，48px）
├──────────┬───────────────────────────────────────────────────┤
│          │  首页                                    ─ □ ×    │
│  [E] ECH │                                              页面标题
│  Workers │───────────────────────────────────────────────────│
│          │                                                   │
│  ⌂ 首页  │          当前页面内容（PageHost）                  │
│  🌐 服务器│                                                   │
│          │                                                   │
│          │                                                   │
│          │                                                   │
│  ⓘ 关于  │                                                   │  ← 关于固定在底部
└──────────┴───────────────────────────────────────────────────┘
```

| 元素 | 说明 |
|------|------|
| 左栏 Grid | 宽 264px，`Margin="0,48,0,0"` 避开标题栏。4 行：品牌 / 首页 / 服务器 / 关于（最后一行 `VerticalAlignment="Bottom"` 固定在底部）。 |
| 品牌区 | 32×32 圆角 logo（`E`，深色 `#1C1C1E` 底）+ "ECH Workers" 文字。 |
| 导航按钮 | `SidebarItemButton` 样式，高 44，圆角 10。选中项加 `SelectedBrush` 底色 + 字重加粗；悬停加更浅的 `HoverBrush`；未选中略降透明度。 |
| 右栏标题栏 | `TitleBarStrip`（48px 高，`Background="Transparent"`）作为拖拽区，内含 `PageTitle` 文本显示当前页名。 |
| `PageHost` | 一个 `Grid`，切换页面时清空后 `Add` 新页实例。 |

页面采用**懒创建并缓存**：首次导航到某页时才 `new` 实例，之后复用，避免重复构建。

```csharp
// MainWindow.xaml.cs —— 导航切换
private void ShowPage(string key)
{
    _currentPage = key;
    PageTitle.Text = key switch
    {
        "servers" => "服务器",
        "about" => "关于",
        _ => "首页",
    };

    UIElement page = key switch
    {
        "servers" => _serversPage ??= new ServersPage(),
        "about" => _aboutPage ??= new AboutPage(),
        _ => _homePage ??= new HomePage(),
    };

    PageHost.Children.Clear();
    PageHost.Children.Add(page);
    RefreshNavVisuals();
}
```

### 2.1 侧边栏状态反馈（代码后置）

```csharp
// MainWindow.xaml.cs —— 侧边栏视觉刷新
private void RefreshOne(Button button)
{
    var isSelected = button.Tag as string == _currentPage;
    var isActive = isSelected || button.IsPointerOver;

    button.Background = isSelected ? SelectedBrush : isActive ? HoverBrush : ClearBrush;
    button.Opacity = isSelected ? 1.0 : isActive ? 1.0 : 0.8;
}
```

状态色：
- `SelectedBrush`：`#2FFFFFFF`（半透明白，选中常驻底色）
- `HoverBrush`：`#1AFFFFFF`（更浅，仅悬停）
- `ClearBrush`：全透明

## 3. 全局样式字典

`Themes/ThemeStyles.xaml`（在 `App.xaml` 中合并到 `Application.Resources`）：

| 样式键 | 用途 |
|--------|------|
| `CardBorder` | 统一卡片外观：圆角 14，内边距 20，`ControlFillColorSecondaryBrush` 底。 |
| `CardHeaderTitle` / `CardHeaderSubtitle` | 卡片标题 / 副标题文本。 |
| `SectionTitle` / `FieldLabel` | 区块标题 / 表单字段标签。 |
| `FormTextBox` | 表单输入框：圆角 10，内边距 12,10，垂直居中。 |
| `SidebarItemButton` | 侧边导航项静态样式（见 §2.1）。 |
| `GhostIconButton` | 无边框圆形图标按钮：36×36，圆角 8，`Segoe Fluent Icons` 15。 |
| `PrimaryActionButton` | 深色胶囊主按钮：`#1C1C1E` 底 / `#FAFAFA` 字，圆角 10，不加系统强调色。 |
| `SecondaryActionButton` | 细边框圆角按钮：透明底 + `ControlStrokeColorDefaultBrush` 边框，圆角 10。 |

## 4. 首页（HomePage）

顶部节点信息卡 + 底部运行日志，单页两段式，无快捷操作栏。

```
┌──────────────────────────────────────────────────────────────┐
│ [icon] example.com:443        ● 已停止 · 127.0.0.1:30000 · 分流 │  节点信息卡
│         HTTP · SOCKS5 · global                              [▶ 启动] │
├──────────────────────────────────────────────────────────────┤
│ ⊙ 运行日志                                  点击启动后实时写入 │
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ [日志文本，等宽字体 Cascadia Mono，可滚动]                  │ │  运行日志
│ └──────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

- **节点信息卡**：左侧 44×44 图标 + 端点名（20 号半粗）+ 协议摘要；中间状态点（`Ellipse` 8×8，红色 `#EF4444` 停止 / 绿色 `#22C55E` 运行中）+ 监听地址 + 分流；右侧深色主按钮。
- **状态切换**（`StartButton_Click`）：切换按钮文字（启动/停止）、图标（`E768`/`E10E`）、状态点颜色、日志写入。
- **运行日志**：`CardBorder` 包裹，内含 `CardHeaderTitle` 标题 + 等宽字体日志区（`Cascadia Mono`，`LineHeight=20`），滚动跟随。

## 5. 服务器页（ServersPage）

列表编辑页：顶部工具栏 + 下方服务器列表，支持全选/批量删除/单条编辑/新增。

```
┌──────────────────────────────────────────────────────────────┐
│ ☑ 全选  [删除]                                    [＋ 新增服务器] │  工具栏
├──────────────────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ ☑  节点 A                    [编辑] [🗑]                   │ │
│ │     example.com:443                                             │  服务器列表
│ │     [HTTP] [socks5] [global]                                   │
│ ├──────────────────────────────────────────────────────────┤ │
│ │ ☐  节点 B                    [编辑] [🗑]                   │ │
│ │     1.2.3.4:10000                                              │
│ └──────────────────────────────────────────────────────────┘ │
│           空状态：暂无服务器配置 + 引导文字                       │
└──────────────────────────────────────────────────────────────┘
```

- **工具栏**：左侧 `全选` CheckBox + `删除` 按钮（`SecondaryActionButton`，无勾选时禁用）；右侧 `新增服务器` 深色主按钮（`PrimaryActionButton`，带 `＋` 图标）。
- **列表项**（`ServerItemTemplate`）：`CheckBox` 绑定 `IsSelected` + 名称（15 号半粗）+ 端点（12 号等宽）+ 三个标签胶囊（协议/代理类型/分流）+ 编辑按钮（`SecondaryActionButton`）+ 删除图标按钮（`GhostIconButton`，`E74D`）。
- **空状态**：`ServerList` 与 `EmptyHint` 叠加，列表为空时显示图标 + 提示文字。

## 6. 新增/编辑服务器（AddServerDialog）

独立 `Window`，Mica 背景，720×760，标题栏拖拽区 + 可滚动表单 + 固定底部按钮。

```
┌──────────────────────────────────────────────────────────────┐
│  [E] 新增服务器                                    [✕]       │  标题栏（56px，仅此处可拖拽）
├──────────────────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ 服务器名称                                                 │ │
│ │ [新节点_______________________]                           │ │
│ └──────────────────────────────────────────────────────────┘ │
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ 🌐 核心配置                                                │ │
│ │ 主机地址         端口                                      │ │
│ │ [127.0.0.1____]  [443 ▼]                                  │ │
│ │ 协议             代理类型                                  │ │
│ │ [HTTP ▼]         [socks5 ▼]                                │ │
│ └──────────────────────────────────────────────────────────┘ │
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ 🌐 高级选项                                                │ │
│ │ 本地监听端口    分流规则                                    │ │
│ │ [30000 ▼]         [global ▼]                              │ │
│ │ SNI               ECH 公钥                                │ │
│ │ [自定义 SNI...]    [Encoded public key...]                │ │
│ └──────────────────────────────────────────────────────────┘ │
├──────────────────────────────────────────────────────────────┤
│                              [取消]    [保存]                  │  底部按钮（固定，不随滚动）
└──────────────────────────────────────────────────────────────┘
```

- **标题栏**：`TitleBarStrip`（56px）承载 logo + 标题文字 + 关闭按钮（`GhostIconButton`，`E711`）。仅此区域可拖动窗口。
- **表单区**：`ScrollViewer` 包裹三个 `CardBorder` 卡片（名称 / 核心配置 / 高级选项），2 列网格布局，间距 18px。
- **底部按钮**：固定在窗口底部（`Grid.Row=2`），`取消`（`SecondaryActionButton`）+ `保存`（`PrimaryActionButton`），上方有 1px 分隔线。
- **窗口尺寸**：`AppWindow.Resize(new SizeInt32(720, 760))`。

## 7. 关于页（AboutPage）

居中展示应用信息，可滚动。

```
┌──────────────────────────────────────────────────────────────┐
│                                                              │
│                     [E]                                      │
│                ECH Workers                                   │
│        Windows 原生客户端 · 基于 ECH 协议的本地代理工具        │
│                                                              │
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ [icon] 客户端版本        v0.1.0         .NET 8 · WinUI 3 │ │
│ │ ───────────────────────────────────────────────────────  │ │
│ │ [icon] ech-workers 内核  v1.3              Go 核心        │ │
│ └──────────────────────────────────────────────────────────┘ │
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ 运行环境                          构建日期               │ │
│ │ Windows 11 · x64                    2026-10-01            │ │
│ └──────────────────────────────────────────────────────────┘ │
│                                                              │
│           客户端通过 Windows 原生 UI 管理节点、代理与系统设置    │
└──────────────────────────────────────────────────────────────┘
```

- 标题区：72×72 圆角 logo + 应用名（24 号半粗）+ 副标题。
- 版本卡片：`CardBorder` 内两行（客户端版本 / ech-workers 内核版本），每行图标 + 标签 + 版本号 + 技术栈标注，中间 1px 分隔线。
- 构建信息：运行环境（`Windows 11 · x64`，运行时架构从 `RuntimeInformation.ProcessArchitecture` 读取）+ 构建日期。

## 8. 主题与配色

应用不强制 `RequestedTheme`，跟随系统浅/深色主题。主要配色：

| 用途 | 深色主题 | 浅色主题 |
|------|----------|----------|
| 卡片背景 | `ControlFillColorSecondaryBrush` | 同左（系统自适应） |
| 次级填充 | `ControlFillColorTertiaryBrush` | 同左 |
| 主按钮底 | `#1C1C1E` | 同左（固定深色，不随主题变） |
| 主按钮字 | `#FAFAFA` | 同左 |
| 主文本 | `TextFillColorPrimaryBrush` | 同左 |
| 次文本 | `TextFillColorSecondaryBrush` | 同左 |
| 三级文本 | `TextFillColorTertiaryBrush` | 同左 |
| 卡片描边 | `CardStrokeColorDefaultBrush` | 同左 |
| 控制描边 | `ControlStrokeColorDefaultBrush` | 同左 |
| 运行中状态 | `#22C55E`（绿） | 同左 |
| 已停止状态 | `#EF4444`（红） | 同左 |

## 9. 标题栏与窗口管理

- **主窗口**：`SetTitleBar(TitleBarStrip)` 将标题栏设为内容区的标题栏 Border；`ExtendsContentIntoTitleBar = true`；`AppWindow.TitleBar.ExtendsContentIntoTitleBar = true`。标题栏按钮背景全透明，文字色随主题切换。
- **对话框**：同样 `SetTitleBar(TitleBarStrip)`，仅顶部 56px 拖拽区可拖动窗口，下方表单区域正常交互。
- **窗口尺寸**：主窗口 `AppWindow.Resize(new SizeInt32(1100, 740))`；对话框 `AppWindow.Resize(new SizeInt32(720, 760))`。
- **窗口标题**：固定为 "ECH Workers"（不随页面切换变化），页面名显示在内容区标题栏 `PageTitle` 文本中。

## 10. 页面缓存策略

```csharp
private HomePage? _homePage;
private ServersPage? _serversPage;
private AboutPage? _aboutPage;

// 首次导航时 new，之后复用
"servers" => _serversPage ??= new ServersPage(),
"about" => _aboutPage ??= new AboutPage(),
_ => _homePage ??= new HomePage(),
```

## 11. 数据模型

`Models/ServerProfile.cs`：实现 `INotifyPropertyChanged`，承载服务器配置字段。

| 属性 | 说明 |
|------|------|
| `Name` / `Host` / `Port` | 服务器名称、主机、端口 |
| `Protocol` | HTTP / TLS / ECH |
| `ProxyType` | socks5 / http / mixed |
| `ListenPort` / `Routing` | 本地监听端口、分流规则 |
| `EchPublicKey` / `Sni` | ECH 公钥、自定义 SNI |
| `UseEch` | 是否启用 ECH（由 Protocol 推导） |
| `IsSelected` | UI 批量选择状态（不参与持久化） |
| `Endpoint` | 计算属性 `Host:Port`，列表展示用 |

## 12. 文件结构

```
src/ECHWorkers.WinUI3/
├── App.xaml / App.xaml.cs          # 应用入口，合并全局资源字典
├── MainWindow.xaml / .cs           # 主窗口：固定侧边栏 + 页面宿主
├── Themes/
│   └── ThemeStyles.xaml            # 全局样式字典（静态外观）
├── Models/
│   └── ServerProfile.cs            # 服务器配置模型
├── Pages/
│   ├── HomePage.xaml / .cs         # 首页
│   ├── ServersPage.xaml / .cs      # 服务器列表页
│   ├── AddServerDialog.xaml / .cs  # 新增/编辑对话框
│   └── AboutPage.xaml / .cs        # 关于页
├── ECHWorkers.WinUI3.csproj
├── app.manifest
└── README.md
```

## 13. 后续待补

- 配置持久化（JSON 文件读写）
- 节点连接与代理进程管理
- 系统代理设置（注册表 / 环境变量）
- 错误提示与消息框
- 日志实时写入（当前为内存追加）
- 图标资源（当前使用 `Segoe Fluent Icons` 字体图标）
