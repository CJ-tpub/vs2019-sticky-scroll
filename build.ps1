# StickyScroll offline build + VSIX pack (no NuGet / no VSSDK BuildTools)
# Usage: powershell -ExecutionPolicy Bypass -File .\build.ps1 [-Configuration Release]
param(
    [string]$Configuration = "Release",
    [string]$VsIdePath = "D:\vs2019"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$msbuild = Join-Path $VsIdePath "MSBuild\Current\Bin\MSBuild.exe"

if (-not (Test-Path $msbuild)) {
    throw "MSBuild not found: $msbuild"
}

# ---- 1. Compile ----
Write-Host "[1/4] Compiling with $msbuild ..."
& $msbuild (Join-Path $root "StickyScroll\StickyScroll.csproj") `
    /t:Build /p:Configuration=$Configuration /v:m /nologo
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE"
}

# ---- 2. Stage VSIX content ----
Write-Host "[2/4] Staging VSIX content ..."
$staging = Join-Path $root "staging"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null

$binDir = Join-Path $root "StickyScroll\bin\$Configuration"
Copy-Item (Join-Path $binDir "StickyScroll.dll") $staging
Copy-Item (Join-Path $binDir "StickyScroll.pdb") $staging -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root "StickyScroll\source.extension.vsixmanifest") (Join-Path $staging "extension.vsixmanifest")
Copy-Item (Join-Path $root "StickyScroll\StickyScroll.pkgdef") $staging

# [Content_Types].xml (OPC requirement; MUST be the first zip entry)
$contentTypes = @'
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="vsixmanifest" ContentType="application/vsixmanifest"/>
  <Default Extension="dll" ContentType="application/octet-stream"/>
  <Default Extension="pdb" ContentType="application/octet-stream"/>
  <Default Extension="pkgdef" ContentType="text/plain"/>
</Types>
'@
[System.IO.File]::WriteAllText(
    (Join-Path $staging "[Content_Types].xml"),
    $contentTypes,
    (New-Object System.Text.UTF8Encoding($false)))

# ---- 3. Pack .vsix (manual entry order: [Content_Types].xml first) ----
Write-Host "[3/4] Packing VSIX ..."
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$vsix = Join-Path $root "StickyScroll.vsix"
if (Test-Path $vsix) { Remove-Item $vsix -Force }
$fs = [System.IO.File]::Open($vsix, [System.IO.FileMode]::Create)
$zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
foreach ($rel in @('[Content_Types].xml', 'extension.vsixmanifest', 'StickyScroll.dll', 'StickyScroll.pdb', 'StickyScroll.pkgdef')) {
    $src = Join-Path $staging $rel
    if (-not (Test-Path -LiteralPath $src)) { continue }   # MUST use -LiteralPath: [ is a wildcard, otherwise [Content_Types].xml is skipped
    $entry = $zip.CreateEntry($rel, [System.IO.Compression.CompressionLevel]::Optimal)
    $es = $entry.Open()
    $bytes = [System.IO.File]::ReadAllBytes($src)
    $es.Write($bytes, 0, $bytes.Length)
    $es.Dispose()
}
$zip.Dispose()
$fs.Dispose()

# ---- 4. Cleanup ----
Write-Host "[4/4] Cleanup staging ..."
Remove-Item $staging -Recurse -Force

Write-Host ""
Write-Host "OK: $vsix"
Write-Host "Install : $VsIdePath\Common7\IDE\VSIXInstaller.exe /q /admin `"$vsix`""
Write-Host "Uninstall: $VsIdePath\Common7\IDE\VSIXInstaller.exe /uninstall:StickyScroll.4A7F2E31-8C5B-4D9E-9B1A-3F6E2D5C8A01"
