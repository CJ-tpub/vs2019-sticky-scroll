# Sticky Scroll for Visual Studio 2019 / VS2019 粘滞滚动扩展

> **English**: A VSIX extension that brings **sticky scroll** (like VS2022 17.5+ / VSCode) to Visual Studio 2019: while scrolling, the nested scope headers of the current code (e.g. `namespace → class → method`) are **pinned at the top of the editor**. Click to jump.
>
> **中文**：为 VS2019 实现 VS2022 17.5+ / VSCode 风格的**粘滞滚动**：滚动代码时，把当前可见区域所属的嵌套作用域声明链（如 `namespace → class → method`，含 `if/for/while/try` 等语句块）**钉在编辑器顶部**，点击可跳转。

## Features / 功能

- **粘滞作用域链 Sticky scope chain**：滚动时编辑器顶部固定显示当前代码块的外层声明行（声明级 + 语句块，基于编辑器原生 outlining + 启发式扫描器）
- **实时更新 Real-time**：滚动、编辑、缩放、主题切换均即时刷新
- **点击跳转 Click to jump**：点击任意粘滞行，编辑器滚动到该行（任意缩放下位置准确）
- **视觉一致 Visual parity**：行号列、语法高亮、字体/行高/缩进/背景与编辑器一致（含缩放），hover 高亮，超长文本省略号 + ToolTip
- **文件顶部自动隐藏 Auto-hide**：滚到顶部时粘滞栏消失，不占空间
- **多语言 Multi-language**：任何带折叠区域的语言自动支持（C#/C++/JS/TS/VB/Java...）；无语言服务的文件（如 .txt）自动不显示
- **无第三方依赖 Zero dependency**：纯编辑器 API，全离线构建，不依赖 NuGet

## Install / 安装

> VS2019 安装 VSIX 扩展的官方方式有两种：**双击 .vsix 文件** 或 **VSIXInstaller 命令行**。
> （"扩展 → 管理扩展 → 从文件安装扩展…"按钮是 VS2022 才有的，VS2019 的"管理扩展"窗口没有此入口。）

### 方式一：双击 .vsix（最简单）⭐ / Double-click (easiest)

从 [Releases](https://github.com/CJ-tpub/vs2019-sticky-scroll/releases) 下载 `StickyScroll.vsix`，**直接双击**文件：

1. 系统会调用 VS 启动器（VSLauncher）打开 **VSIX Installer** 窗口
2. 按向导确认目标 VS 版本（Visual Studio Professional 2019）
3. 点 **安装**（UAC 确认）→ **关闭** → 重启 VS 生效

> 已安装过本扩展时：双击**更高版本**的 vsix 即可**直接升级覆盖**（无需先卸载）；
> 相同版本会提示"已安装"，此时请先卸载或用命令行 `/force` 覆盖安装。

### 方式二：命令行 / Command line

```bat
<VS2019安装目录>\Common7\IDE\VSIXInstaller.exe /q /admin StickyScroll.vsix
```

> 将 `<VS2019安装目录>` 替换为你的 VS2019 安装位置（常见：`C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional`）。
> 需要管理员权限（UAC 确认）。装到 VS2019 机器级扩展目录。

## Uninstall / 卸载

```bat
<VS2019安装目录>\Common7\IDE\VSIXInstaller.exe /q /uninstall:StickyScroll.v1
```

## Usage / 使用

1. 打开任意 C#（或其他语言）源文件 / open any source file
2. 滚动代码 —— 编辑器顶部出现当前作用域链 / scroll; the scope chain appears at the top
3. 点击任意粘滞行 —— 跳转到对应声明行 / click a sticky row to jump to that line
4. 滚回文件顶部 —— 粘滞栏自动消失 / scroll back to top; the bar hides automatically

## Settings / 设置（配置文件）

编辑 `%APPDATA%\StickyScroll\settings.ini`（首次运行自动创建模板），保存后滚动即生效：

```ini
# StickyScroll settings
MaxLines=3      # 最大粘滞行数 / max sticky lines (1-10)
Enabled=true    # 总开关 / enable or disable (true/false)
```

> 说明：VS 选项页（Tools→Options）依赖 Package/pkgdef 注册链路，部分 VSIXInstaller 版本对 VS2019 的该链路失效（扩展本身注册正常、选项页无法注册），故采用配置文件方案，简单可靠。

## Build / 构建（离线，无 NuGet/VSSDK 依赖）

本仓库支持**全离线构建**（无需 NuGet/VSSDK/网络）：

```bat
powershell -ExecutionPolicy Bypass -File .\setup-ref.ps1   rem 一次性：从 .NET 4.8 运行时生成 v4.7.2 引用程序集
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release
```

- 产出 `StickyScroll.vsix`
- VS 安装路径可通过参数指定（默认 `D:\vs2019`，其他机器请传参）：

```bat
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release -VsIdePath "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional"
```

**开发迭代部署**（免重装）：找到扩展安装目录（`<VS2019安装目录>\Common7\IDE\Extensions\` 下含 `extension.vsixmanifest` 且 Id 为 `StickyScroll.v1` 的目录），覆盖 DLL 后重启 VS：

```bat
copy StickyScroll\bin\Release\StickyScroll.dll <扩展目录>\
```

## Project Layout / 工程结构

```
StickyScroll.csproj             .NET Framework 4.7.2（引用程序集本地生成），C# 8
source.extension.vsixmanifest   安装目标 VS2019 (16.0-17.0)，MefComponent 资产
StickyScrollMarginProvider.cs   MEF 导出：IWpfTextViewMarginProvider（顶部 margin 容器）
StickyScrollMargin.cs           margin 实现：滚动同步（LayoutChanged）+ 渲染 + 点击交互
StickyLineProvider.cs           粘滞行检测：IOutliningManager 区域 + 花括号扫描器（含语句块）
StickyScrollSettings.cs         配置文件设置（MaxLines / Enabled）
build.ps1                       MSBuild 编译 + 手工 OPC 打包（[Content_Types].xml 须为 zip 首条目）
setup-ref.ps1                   离线生成 .NET Framework 4.7.2 reference assemblies
test-files/TestSticky.cs        验证用 C# 测试文件
```

## Technical Notes / 关键技术点

| 点 | 说明 |
|---|---|
| 顶部 margin | `IWpfTextViewMarginProvider` + `[MarginContainer(PredefinedMarginNames.Top)]`，VS 官方"视口顶部固定区域"机制 |
| 滚动监听 | VS2019 **没有** `ViewportTopChanged` 事件；用 `ITextView.LayoutChanged` + `TextViewLayoutChangedEventArgs.NewViewState.ViewportTop` |
| 粘滞行检测 | `IOutliningManager.GetAllRegions()` 声明级区域 + 白名单过滤的扫描器（if/for/while/try/else 等语句块），合并去重 |
| 点击跳转 | `IViewScroller.ScrollViewportVerticallyByPixels()`（方向修正后目标行精确落在粘滞栏下方第一行） |
| 主题/字体 | `IEditorFormatMap("Plain Text")` 前景/背景 + `FormattedLineSource.DefaultTextProperties`（与编辑器完全一致，含缩放） |
| 打包 | VSIX = OPC zip：`[Content_Types].xml` **必须是第一个条目**，否则 VSIXInstaller 报"不是有效的 VSIX 包" |

## Known Limitations / 已知限制

- word wrap 开启时点击跳转按"行号 × 行高"估算，长行换行后定位可能有偏差
- manifest 不含 `<Prerequisites>`（部分 VSIXInstaller 版本对 VS2019 的 Prerequisites 解析存在状态相关 bug；VS2019 必有 CoreEditor，无实际影响）

## License / 许可证

MIT License — 见 [LICENSE](LICENSE)。
