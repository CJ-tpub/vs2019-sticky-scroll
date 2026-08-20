# Sticky Scroll for Visual Studio 2019

**[English](README.md) | [中文](README.zh-CN.md)**

A VSIX extension that brings **sticky scroll** (like VS2022 17.5+ / VSCode) to Visual Studio 2019: while scrolling, the nested scope headers of the current code (e.g. `namespace → class → method`, **including statement blocks** like `if/for/while/try`) are **pinned at the top of the editor**. Click to jump.

## Features

- **Sticky scope chain** — declarations (namespace/class/method) **and** statement blocks (if/for/while/try/else...) stay pinned at the top while scrolling
- **Real-time** — updates instantly on scroll, edit, zoom and theme change
- **Click to jump** — click any sticky row; the target line lands exactly as the first line under the bar
- **Visual parity** — line-number column, syntax highlighting, font/line-height/indentation/background match the editor exactly at **any zoom level**; hover highlight; ellipsis + ToolTip for long lines
- **Auto-hide** — the bar disappears at the top of the file, taking no space
- **Multi-language** — works with any language that has outlining/folding (C#/C++/JS/TS/VB/Java...); plain files (e.g. .txt) are ignored
- **Zero dependency** — pure editor API, fully offline build (no NuGet/VSSDK)

## Install

> VS2019 installs VSIX extensions in two official ways: **double-click the .vsix** or **VSIXInstaller CLI**.
> (The "Extensions → Manage Extensions → Install from file…" button exists in VS2022 only; VS2019's Manage Extensions window has no such entry.)

### Method 1: Double-click the .vsix (easiest) ⭐

Download `StickyScroll.vsix` from [Releases](https://github.com/CJ-tpub/vs2019-sticky-scroll/releases) and **double-click** it:

1. VSLauncher opens the **VSIX Installer** wizard
2. Confirm the target VS version (Visual Studio Professional 2019)
3. Click **Install** (UAC confirm) → **Close** → restart VS

> If an older version is already installed: double-clicking a **newer** vsix **upgrades in place** (no need to uninstall first). The same version will prompt "already installed" — uninstall first or use `/force`.

### Method 2: Command line

```bat
<VS2019Dir>\Common7\IDE\VSIXInstaller.exe /q /admin StickyScroll.vsix
```

> Replace `<VS2019Dir>` with your VS2019 install path (common: `C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional`). Admin rights required (UAC). Installs to the machine-level extension directory.

## Uninstall

```bat
<VS2019Dir>\Common7\IDE\VSIXInstaller.exe /q /uninstall:StickyScroll.v1
```

## Usage

1. Open any C# (or other language) source file
2. Scroll — the scope chain appears at the top of the editor
3. Click a sticky row to jump to that line
4. Scroll back to the top — the bar hides automatically

## Settings (config file)

Edit `%APPDATA%\StickyScroll\settings.ini` (template auto-created on first run). Changes take effect on the next scroll:

```ini
# StickyScroll settings
MaxLines=3      # max sticky lines (1-10)
Enabled=true    # enable or disable (true/false)
```

> Note: the VS Options page (Tools→Options) depends on the Package/pkgdef registration chain, which some VSIXInstaller versions fail to apply for VS2019 (the extension itself registers fine; option pages do not). Hence the simple config-file approach.

## Build (offline, no NuGet/VSSDK)

Fully offline build — no NuGet/VSSDK/network required:

```bat
powershell -ExecutionPolicy Bypass -File .\setup-ref.ps1   rem one-time: generate v4.7.2 reference assemblies from .NET 4.8 runtime
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release
```

- Produces `StickyScroll.vsix`
- VS path is configurable (default `D:\vs2019`):

```bat
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release -VsIdePath "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional"
```

**Fast dev iteration** (no reinstall): find the extension directory (`<VS2019Dir>\Common7\IDE\Extensions\` — the one containing `extension.vsixmanifest` with Id `StickyScroll.v1`), overwrite the DLL and restart VS:

```bat
copy StickyScroll\bin\Release\StickyScroll.dll <ext-dir>\
```

## Project Layout

```
StickyScroll.csproj             .NET Framework 4.7.2 (locally generated reference assemblies), C# 8
source.extension.vsixmanifest   targets VS2019 (16.0-17.0), MefComponent asset
StickyScrollMarginProvider.cs   MEF export: IWpfTextViewMarginProvider (top margin container)
StickyScrollMargin.cs           margin: scroll sync (LayoutChanged) + rendering + click interaction
StickyLineProvider.cs           sticky-line detection: IOutliningManager regions + brace scanner (statement blocks)
StickyScrollSettings.cs         config-file settings (MaxLines / Enabled)
build.ps1                       MSBuild + manual OPC packaging ([Content_Types].xml must be the first zip entry)
setup-ref.ps1                   generates offline .NET Framework 4.7.2 reference assemblies
test-files/TestSticky.cs        C# test file for verification
```

## Technical Notes

| Topic | Detail |
|---|---|
| Top margin | `IWpfTextViewMarginProvider` + `[MarginContainer(PredefinedMarginNames.Top)]` — VS's official "fixed area at the top of the viewport" mechanism |
| Scroll listening | VS2019 has **no** `ViewportTopChanged` event; use `ITextView.LayoutChanged` + `TextViewLayoutChangedEventArgs.NewViewState.ViewportTop` |
| Sticky-line detection | `IOutliningManager.GetAllRegions()` (declarations) merged with a whitelist-filtered brace scanner (if/for/while/try/else blocks), de-duplicated |
| Click to jump | `IViewScroller.ScrollViewportVerticallyByPixels()` (sign corrected; target line lands as the first line under the bar) |
| Theme/Font | `IEditorFormatMap("Plain Text")` + `FormattedLineSource.DefaultTextProperties` (exact editor match, zoom-aware) |
| Packaging | VSIX = OPC zip: `[Content_Types].xml` **must be the first entry**, otherwise VSIXInstaller reports "not a valid VSIX package" |

## Known Limitations

- With word wrap enabled, click-to-jump uses "line number × line height" estimation; wrapped long lines may land slightly off
- The manifest omits `<Prerequisites>` (some VSIXInstaller versions have a state-related bug parsing Prerequisites for VS2019; VS2019 always ships CoreEditor, so no practical impact)

## License

MIT License — see [LICENSE](LICENSE).
