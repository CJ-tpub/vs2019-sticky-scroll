---
name: vs2019-extension-dev
description: Visual Studio 2019 VSIX 编辑器扩展开发的完整实战指南——开发流程、关键 API、离线构建、VSIX 打包、安装部署、调试方法、测试与发布流程，以及工具链相关的全部已知坑与解决方案。当任务涉及 VS2019 扩展、VSIX 打包、IWpfTextViewMargin/adornment、MEF 编辑器组件、编辑器顶部固定区域或滚动联动 UI 等自定义编辑器功能时使用。
---

# VS2019 扩展开发实战指南（VSIX / 编辑器扩展）

> 本技能沉淀自一次完整的 VS2019 编辑器扩展开发实战，均为实测经验。
> **先读"环境前提"，再按"开发流程"执行，遇到问题查"避坑清单"。**

## 一、环境前提（开工前必查）

| 检查项 | 方法 | 备注 |
|---|---|---|
| VS2019 是否安装 | `vswhere.exe -all -products * -format json`（C:\Program Files (x86)\Microsoft Visual Studio\Installer\） | 记录 instanceId 与安装路径 |
| MSBuild | `%VS2019路径%\MSBuild\Current\Bin\MSBuild.exe` | 不在 PATH，直接用全路径 |
| Roslyn/csc 版本 | `csc.exe -version`（MSBuild\Current\Bin\Roslyn\） | 3.11 = C# 8 |
| VSIXInstaller | `%VS2019路径%\Common7\IDE\VSIXInstaller.exe` | 注意：实际可能转发到 VS Installer 的 17.x 版本（见避坑 B1） |
| .NET Framework targeting pack | 检查 `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2` | 缺失时用离线方式生成引用程序集（见 B3） |
| HTTPS 外网（NuGet/GitHub） | `curl -I https://www.baidu.com` | 不通 → 走离线构建路线（B3）；git 可配代理（HTTP_PROXY） |
| VSSDK BuildTools | `%VS2019路径%\MSBuild\Microsoft\VisualStudio\v16.0\VSSDK` | 无 → 手工打包 vsix（见三） |

## 二、开发流程（推荐顺序）

```
需求确认 → API 调研 → 工程搭建 → 最小冒烟 → 迭代开发 → 全面测试 → 打包发布
```

### 2.1 API 调研
- 官方文档：learn.microsoft.com（可切 `view=visualstudiosdk-2019` 看 VS2019 版本 API）
- **反射验证**（比查文档快）：`[Reflection.Assembly]::LoadFrom('...\Microsoft.VisualStudio.Text.UI.dll')` 后检查类型/成员/事件是否存在
- 关键程序集位置：
  - `%VS2019路径%\Common7\IDE\CommonExtensions\Microsoft\Editor\`（Text.Data/Logic/UI/UI.Wpf、CoreUtility）
  - `%VS2019路径%\Common7\IDE\PublicAssemblies\`（Shell.15.0、Shell.Framework、OLE.Interop）
  - `%VS2019路径%\Common7\IDE\Microsoft.VisualStudio.Utilities.dll`

### 2.2 工程搭建（离线模板）
- csproj：`TargetFrameworkVersion=v4.7.2`（**必须 ≥ 4.7.2**，VS2019 程序集都是 4.7.2 target，低了会 MSB3274 引用被跳过）+ `TargetFrameworkRootPath` 指向本地 ref 目录 + 本地 HintPath 引用（`<Private>False</Private>`）
- `source.extension.vsixmanifest`：Identity/Installation（Community+Professional+Enterprise `[16.0,17.0)`）/Assets（MefComponent）
- `build.ps1`：MSBuild 编译 + 手工 OPC 打包（见三）
- **先做最小冒烟**：只放一个空 margin（固定文本），验证「编译→打包→安装→VS 显示」全链路，再开发功能

### 2.3 迭代开发循环
```
改代码 → build.ps1 → 拷贝 DLL 到扩展目录（需先授权 ACL，见六）→ 重启 VS → 验证 → 改
```
功能开发完成后按"五、测试流程"全面回归，再发布。

## 三、VSIX 打包（离线手工版）

VSIX = OPC zip，**三个硬性要求**：

1. **`[Content_Types].xml` 必须是 zip 的第一个条目**——否则 VSIXInstaller 报"不是有效的 VSIX 包"。
   `Compress-Archive`/`ZipFile.CreateFromDirectory` 条目顺序不可控，必须用 `ZipArchive` 手动按序 CreateEntry。
2. manifest 的 Asset 用 `d:Source="File" Path="<你的DLL>.dll"`（手工打包不能写 d:Source="Project"）。
3. `.vsixmanifest` 文件必须命名为 `extension.vsixmanifest` 放在包根。

**PowerShell 打包脚本要点**：
- `Test-Path` 对 `[Content_Types].xml` 会把 `[]` 当通配符 → 必须 `-LiteralPath`
- 脚本里中文注释 + 无 BOM UTF-8 会让 PS 5.1 解析失败 → 脚本保持纯 ASCII 或加 UTF-8 BOM
- `[Content_Types].xml` 内容：`<Default Extension="vsixmanifest" ContentType="application/vsixmanifest"/>` + dll/pdb/pkgdef 的 Default

**vsixmanifest 内容避坑**：
- **不要带 `<Prerequisites>`**（17.14 安装器状态相关 bug：报"不是有效的 VSIX 包"，见 B1）
- 版本号（Identity Version）与 GitHub Release 标签**对齐**——同版本无法覆盖安装，升级安装靠更高版本号

## 四、编辑器扩展关键实现要点

| 需求 | 正确做法 |
|---|---|
| 视口顶部固定区域 | `IWpfTextViewMarginProvider` + `[MarginContainer(PredefinedMarginNames.Top)]` + `[Order(After=PredefinedMarginNames.Top)]` + `[TextViewRole(PredefinedTextViewRoles.Document)]` |
| 监听滚动 | **VS2019 没有 `ViewportTopChanged` 事件**（反射确认）→ `ITextView.LayoutChanged` + `e.NewViewState.ViewportTop` |
| 检测代码块结构 | `IOutliningManager.GetAllRegions(snapshot)`（注意签名是 SnapshotSpan 参数；**C# 的 outlining 只有声明级区域**：namespace/class/method，**没有 if/for/while 等语句块**）→ 语句块需自写扫描器（花括号状态机，忽略注释/字符串）+ 白名单过滤（排除含 `=`/`=>`/`new` 的初始化/lambda 行） |
| 语法高亮 | `IClassifierAggregatorService.GetClassifier(buffer)` + `IClassificationFormatMapService.GetClassificationFormatMap(view)`，按分类 span 生成 Runs |
| 主题色/字体 | `IEditorFormatMap.GetProperties("Plain Text")`（键 "Foreground"/"Background"）+ `FormattedLineSource.DefaultTextProperties` |
| 缩放跟随 | **`FormattedLineSource.FontRenderingEmSize` 与 `LineHeight` 是"未缩放"基准值** → 实际大小 = 基准 × `ZoomLevel/100`；字体/行高/缩进都要乘 |
| 点击/交互跳转 | `IViewScroller.ScrollViewportVerticallyByPixels(delta)`——**实测符号：正值 = 内容下移（往前滚）**，delta = `ViewportTop - targetY`；目标行 Y = 行号 × 当前缩放行高（`FirstVisibleLine.Height`，不能用 `_view.LineHeight` 未缩放值） |

### 4.1 几何对齐（自定义 UI 元素与编辑器逐像素对齐）
- **坐标体系**：top margin 的 x=0 ≠ viewport 左缘——二者之间隔着 outlining/行号/selection 等左 margin。用 WPF `TranslatePoint` 实测：
  - viewport 原点在 margin 坐标：`_view.VisualElement.TranslatePoint((0,0), _root).X`
  - 文本列（margin 坐标）= viewport 原点 + `FirstVisibleLine.TextLeft`（+1~2px 渲染微调）
  - 行号数字右缘：**行号 margin 元素右缘** `margin.VisualElement.TranslatePoint((ActualWidth,0), _root).X`——**VS 行号内容经缩放变换放大**（元素宽 × zoom），TranslatePoint 自动计入；这是唯一与像素实测吻合的方案
- **DPI 陷阱**：PowerShell 默认非 DPI-aware，`GetWindowRect` 返回虚拟坐标导致像素测量全乱 → 先 `SetProcessDPIAware()`
- **像素验证**：`CopyFromScreen` 全屏截图 + `GetPixel` 分析（**PrintWindow 对 GPU 渲染内容颜色会失真**，不要用）；截图的浅色判定会误匹配浏览器等其他窗口，扫描范围要限定在 VS 窗口区域

### 4.2 性能
- 内容计算缓存：按 `ITextSnapshot` 缓存扫描结果；滚动时内容相同跳过重绘（内容比较 + 几何度量变化检测）
- 缩放/几何变化必须触发强制重绘（检测 ZoomLevel/文本列/行号列宽/行号右缘的变化）

## 五、测试流程（发布前逐项回归）

1. 功能正确性：目标场景主路径（如滚动联动）在文件中部/顶部/底部行为正确
2. **边界输入**：注释/字符串中的特殊字符不误判；初始化/匿名对象/lambda 等非目标结构不误触发；无语言服务的纯文本文件不显示
3. **交互**：点击/悬停等交互在各缩放（100%/150%/200%/300%）下行为正确
4. **缩放跟随**：Ctrl+滚轮缩放，自定义 UI 的字体/行高/缩进/列位置与编辑器同步
5. **实时性**：编辑（增删行、改内容）后立即更新
6. **主题切换**：浅色/深色背景色正确
7. **性能**：大文件（万行级）滚动流畅
8. **卸载干净**：VSIXInstaller /uninstall 后无残留
9. 与折叠、word wrap、缩放等内置功能共存正常

## 六、安装 / 部署 / 权限

- 安装：双击 vsix（走 VSLauncher 向导）或 `VSIXInstaller.exe /q /admin <你的>.vsix`（**需要管理员/UAC**——写注册表 hive）
- 升级：双击更高版本 vsix 覆盖即可；同版本提示"已安装"→ 先卸载或 `/force`
- 卸载：`VSIXInstaller.exe /q /uninstall:<扩展Id>`
- 实验实例：`VSIXInstaller /rootSuffix Exp <你的>.vsix`（开发调试不污染正式环境）
- **开发迭代免重装**：机器级扩展目录默认不可写 → 提权一次 `icacls <扩展目录> /grant "<用户>:(OI)(CI)M" /T`，之后直接覆盖 DLL + 重启 VS
- 用户级扩展目录（%LOCALAPPDATA%\...\Extensions\）手动放文件**无效**（VS 只加载已注册扩展）
- 重装后若有多个同 Id 扩展目录残留（卸载中断导致），VS 可能加载到旧 DLL → 清理旧目录只留最新

## 七、调试方法（按优先级）

1. **文件日志**：`File.AppendAllText(%TEMP%\xxx.log, ...)` 在 CreateMargin/渲染/事件处理器里打点——最直接
2. **ActivityLog**：`devenv.exe /log <path>` 启动，查 Extension Manager/Pkgdef 加载记录
3. **像素验证**：SetProcessDPIAware + CopyFromScreen + GetPixel 分析（几何对齐问题）
4. **视觉树遍历**：`VisualTreeHelper` 打印元素类型/宽度/TranslatePoint 右缘（定位 VS 内部元素位置）
5. **MEF 缓存**：`%LOCALAPPDATA%\Microsoft\VisualStudio\16.0_<id>\ComponentModelCache\`——DLL 更新后不生效时删除它冷启动重建；`Default.err` 看组合错误

## 八、发布流程（GitHub）

```bash
# 仓库初始化（.gitignore 排除 bin/ obj/ ref/ staging/ *.vsix *.log）
git init && git add -A && git commit -m "..."

# 推送到 GitHub（gh CLI 已认证时）
gh auth setup-git              # 让 git 用 gh 的凭证（避免默认凭证账号不对）
# 仅当 HTTPS 直连不通、且本机配置了代理时才需要设置（把 <代理地址> 换成你的，如 http://127.0.0.1:7897）；无代理环境跳过这两行
$env:HTTP_PROXY='<代理地址>'; $env:HTTPS_PROXY='<代理地址>'
git branch -M main && git push -u origin main

# 建仓库 + Release
gh repo create <name> --public --source . --remote origin --push
gh release create vX.Y.Z '<你的>.vsix' --title '...' --notes '...'
gh release upload vX.Y.Z '<你的>.vsix' --clobber   # 更新附件
```

- **README 双文件**：`README.md`（英文）+ `README.zh-CN.md`（中文），顶部 `[English](README.md) | [中文](README.zh-CN.md)` 切换
- **Release notes 中英成对**：每个区块都写「**中文**：… / **English**: …」
- **README 中的操作步骤必须实测确认**（如"扩展→管理扩展→从文件安装扩展"按钮是 VS2022 才有，VS2019 没有——写错会被用户指出）

## 九、避坑清单（全部为实测踩坑记录）

### B1. VSIXInstaller 17.14（VS2022 安装器）对 VS2019 的兼容问题
- 本机 `%VS2019路径%\Common7\IDE\VSIXInstaller.exe` 实际转发到 VS Installer 的 17.14 版本
- **带 `<Prerequisites>` 的包** → 报"不是有效的 VSIX 包"（状态相关，时好时坏）→ **去掉 Prerequisites**
- **pkgdef 不自动应用**（VS 启动也不扫）→ Package/选项页注册失败；手动写 HKLM/HKCU 注册表（Packages 键 + Options 分类标记）**也无法被 VS 识别** → **VS2019 扩展选项页不可行，用配置文件（%APPDATA%）替代**
- 双击安装时弹"扩展与所选版本不兼容"警告 → **误判，点"是"继续**（命令行安装无警告）
- 卸载扩展时可能把目录写进 `%LOCALAPPDATA%\Microsoft\VisualStudio\16.0_<id>\ExcludedDirectories.lst` → **扩展"装上了却不加载"** → 清理该列表 + `devenv /updateconfiguration` 重建缓存
- 卸载/安装写注册表 hive 需要管理员权限（0x80070005 Access Denied）

### B2. 编辑器/API 相关
- `ITextViewLine` 没有 `LineNumber` 属性 → 用 `line.Start.GetContainingLine().LineNumber`
- `ITextView` 没有 `ViewportTopChanged` → `LayoutChanged` + `NewViewState.ViewportTop`
- `IOutliningManager` 在 Text.UI.dll（不在 Text.Logic）；`GetAllRegions(SnapshotSpan)` 返回 IEnumerable
- `IEditorFormatMap` 无 `GetBrush`（那是扩展方法概念），用 `GetProperties("Plain Text")` 的 ResourceDictionary；事件叫 `FormatMappingChanged`
- `IClassifierAggregatorService.GetClassifier(ITextBuffer)`（参数是 buffer 不是 snapshot）
- 多数据源合并（如 outlining 深度 vs 扫描器括号深度）时深度体系不同，需统一（独有行深度按"下一个候选深度"推断）
- `Microsoft.VisualStudio.Text.Editor.DefaultOptions.TabSizeOptionId` 在 Text.Logic.dll；`PredefinedMarginNames.LineNumber = "LineNumber"`

### B3. 离线构建（无 HTTPS / 无 VSSDK 包）
- 从 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319` 拷贝运行时程序集到本地 ref 目录 + 写 `RedistList\FrameworkList.xml` → `TargetFrameworkRootPath` 指向它，即可 target v4.7.2
- 运行时程序集版本号与引用程序集一致（4.0.0.0），可当引用编译
- 编译器用 csc 3.11（C# 8）：`LangVersion=latest`；注意低目标框架下 ValueTuple 等新 BCL 类型不可用
- `dotnet` CLI 无 SDK 不影响（.NET Framework 项目用 MSBuild 即可）

### B4. 权限/目录
- 机器级扩展目录（`%VS2019路径%\Common7\IDE\Extensions\<随机名>`）默认拒绝普通用户写 → icacls 授权（提权）
- devenv 单实例：先杀旧实例再启动新实例，否则命令行参数会被转发给旧实例后新进程退出
- 测试残留：反复安装测试包会在 Extensions 下积累多个同 Id 目录 → 同名 MEF provider 冲突（VS 加载到旧 DLL）→ 全部清理只留最新

### B5. 其他
- PowerShell 5.1 无 BOM UTF-8 中文脚本解析失败 → 脚本纯 ASCII 或加 BOM
- `SendKeys` 不支持滚轮（Ctrl+滚轮缩放模拟用 keybd_event+mouse_event 或 PostMessage WM_MOUSEWHEEL+MK_CONTROL，但两者在 VS 上未必生效，缩放测试优先手动）
- Windows DPI：GetWindowRect/截图像素测量前先 SetProcessDPIAware

## 十、本技能适用场景速查

- 要开发 VS2019（或 VS2017/2022 兼容）编辑器扩展 → 按"二、开发流程"执行
- 要打包/安装/排错 vsix → 看"三、六、七"
- 遇到"装不上/不加载/不生效" → 看 B1（ExcludedDirectories、MEF 缓存、多目录残留）
- 要发布开源扩展 → 看"八、发布流程"
