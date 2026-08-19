# Generate .NET Framework 4.7.2 reference assemblies (offline, no targeting pack)
# Source: local .NET Framework 4.x runtime assemblies
# Usage: powershell -ExecutionPolicy Bypass -File .\setup-ref.ps1
$ErrorActionPreference = "Stop"

$runtime = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpfDir  = Join-Path $runtime 'WPF'
$target  = Join-Path $PSScriptRoot 'ref\Framework\.NETFramework\v4.7.2'
$redist  = Join-Path $target 'RedistList'

New-Item -ItemType Directory -Path $redist -Force | Out-Null

$files = @(
    'mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll',
    'System.Configuration.dll','System.Data.dll','System.Drawing.dll',
    'System.Numerics.dll','System.Runtime.Serialization.dll','System.Transactions.dll',
    'System.Xml.Linq.dll','System.ComponentModel.Composition.dll','System.Xaml.dll',
    'WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll',
    'System.ComponentModel.DataAnnotations.dll','System.Web.dll',
    'System.ServiceModel.dll','System.ServiceModel.Web.dll',
    'System.Runtime.Caching.dll','System.Web.Extensions.dll'
)
$missing = @()
foreach ($f in $files) {
    $src = Join-Path $runtime $f
    if (-not (Test-Path $src)) { $src = Join-Path $wpfDir $f }
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $target $f) -Force
    } else {
        $missing += $f
    }
}
if ($missing.Count -gt 0) {
    Write-Host "WARN missing from runtime: $($missing -join ', ')"
}

# RedistList/FrameworkList.xml - MSBuild uses this to identify framework assemblies
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void]$sb.AppendLine('<FileList Name=".NETFramework,Version=v4.7.2" TargetFrameworkIdentifier=".NETFramework" TargetFrameworkVersion="v4.7.2" FrameworkName=".NETFramework 4.7.2">')
foreach ($f in (Get-ChildItem $target -Filter '*.dll' | Sort-Object Name)) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
    [void]$sb.AppendLine("  <File AssemblyName=`"$name`" Version=`"4.0.0.0`" />")
}
[void]$sb.AppendLine('</FileList>')
[System.IO.File]::WriteAllText(
    (Join-Path $redist 'FrameworkList.xml'),
    $sb.ToString(),
    [System.Text.Encoding]::UTF8)

Write-Host "OK: reference assemblies at $target"
(Get-ChildItem $target -Filter '*.dll').Count
