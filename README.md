# Sticky Scroll for Visual Studio 2019

类 VS2022 17.5+ / VSCode 的**粘滞滚动** VSIX 扩展：滚动代码时，把当前可见区域所属的嵌套作用域声明链（如 `namespace → class → method`）**钉在编辑器顶部**，点击可跳转。

## 功能

- **粘滞作用域链**：滚动时编辑器顶部固定显示当前代码块的外层声明行（基于编辑器原生 outlining / 折叠区域，C# 由 Roslyn 提供，准确可靠）
- **实时更新**：滚动、编辑、缩放、主题切换均即时刷新
- **点击跳转**：点击任意粘滞行，编辑器滚动到该行
- **视觉适配**：文字/背景/字体与编辑器一致（含缩放），hover 高亮，行间分隔线，超长文本省略号 + ToolTip
- **文件顶部自动隐藏**：滚到顶部时粘滞栏消失，不占空间
- **多语言**：任何带折叠区域的语言自动支持（C#/C++/JS/TS/VB/Java...）；无语言服务的文件（如 .txt）自动不显示
- **无第三方依赖**：纯编辑器 API，离线构建，不依赖 NuGet

## 安装

```bat
D:\vs2019\Common7\IDE\VSIXInstaller.exe /q /admin StickyScroll.vsix
```

> 需要管理员权限（UAC 确认）。装到 VS2019 机器级扩展目录。

## 卸载

```bat
D:\vs2019\Common7\IDE\VSIXInstaller.exe /q /uninstall:StickyScroll.v1
```

## 使用

1. 打开任意 C#（或其他语言）源文件
2. 滚动代码 —— 编辑器顶部出现当前作用域链（如 `namespace StickyScrollTest` → `public class OuterClass` → `public void MethodD()`）
3. 点击任意粘滞行 —— 跳转到对应声明行
4. 滚回文件顶部 —— 粘滞栏自动消失

## 构建（离线，无 NuGet/VSSDK 依赖）

本机环境约束：HTTPS 不通、无 VSSDK BuildTools → 采用**全离线构建**：

```bat
powershell -ExecutionPolicy Bypass -File .\setup-ref.ps1   rem 一次性：从 .NET 4.8 运行时生成 v4.7.2 引用程序集
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release
```

产出 `StickyScroll.vsix`。

**开发迭代部署**（免重装）：扩展目录已授权可写，直接覆盖 DLL 后重启 VS：

```bat
copy StickyScroll\bin\Release\StickyScroll.dll D:\vs2019\Common7\IDE\Extensions\cb2zzibj.chj\
```

## 工程结构

```
StickyScroll.csproj             .NET Framework 4.7.2（引用程序集本地生成），C# 8
source.extension.vsixmanifest   安装目标 VS2019 (16.0-17.0)，MefComponent 资产
StickyScrollMarginProvider.cs   MEF 导出：IWpfTextViewMarginProvider（顶部 margin 容器）
StickyScrollMargin.cs           margin 实现：滚动同步（LayoutChanged）+ 渲染 + 点击交互
StickyLineProvider.cs           粘滞行检测：IOutliningManager 区域树 + 花括号扫描器回退
build.ps1                       MSBuild 编译 + 手工 OPC 打包（[Content_Types].xml 须为 zip 首条目）
setup-ref.ps1                   离线生成 .NET Framework 4.7.2 reference assemblies
test-files/TestSticky.cs        验证用 C# 测试文件
```

## 关键技术点

| 点 | 说明 |
|---|---|
| 顶部 margin | `IWpfTextViewMarginProvider` + `[MarginContainer(PredefinedMarginNames.Top)]`，VS 官方"视口顶部固定区域"机制 |
| 滚动监听 | VS2019 **没有** `ViewportTopChanged` 事件；用 `ITextView.LayoutChanged` + `TextViewLayoutChangedEventArgs.NewViewState.ViewportTop` |
| 粘滞行检测 | `IOutliningManager.GetAllRegions()` 建区域树，取视口顶行所在最深区域 → 祖先链（≤3 层）；无 outlining 时回退到忽略注释/字符串的花括号扫描器 |
| 点击跳转 | `IViewScroller.ScrollViewportVerticallyByPixels()` |
| 主题/字体 | `IEditorFormatMap("Plain Text")` 前景/背景 + `FormattedLineSource.DefaultTextProperties`（与编辑器完全一致，含缩放） |
| 打包 | VSIX = OPC zip：`[Content_Types].xml` **必须是第一个条目**，否则 VSIXInstaller 报"不是有效的 VSIX 包" |

## 已知限制 / 后续计划

- **选项页**（工具→选项→Sticky Scroll：最大行数/透明度/开关）：需要 Package + pkgdef 注册，列为 v1.1
- 点击跳转按"行号 × 行高"估算像素偏移，启用 word wrap 时定位可能略有偏差
- 默认最大显示 3 层（对齐 VS2022 观感；VSCode 为 5-20）
- 本机 VSIXInstaller(17.14) 对带 `<Prerequisites>` 的包存在状态相关 bug（报"无效包"），故 manifest 不含 Prerequisites（VS2019 必有 CoreEditor，无实际影响）
