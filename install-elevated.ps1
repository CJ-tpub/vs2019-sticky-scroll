# Elevated batch: uninstall test extensions + install StickyScroll
# Run via UAC: powershell -ExecutionPolicy Bypass -File .\install-elevated.ps1
$ErrorActionPreference = "Continue"
$vsixInstaller = 'D:\vs2019\Common7\IDE\VSIXInstaller.exe'
$root = $PSScriptRoot

$testIds = @(
    'StickyScroll.4A7F2E31-8C5B-4D9E-9B1A-3F6E2D5C8A01',
    'StickyScrollTest.GUID',
    'TestTags.1',
    'TestGuid.4A7F2E31-8C5B-4D9E-9B1A-3F6E2D5C8A01',
    'R1Plain.1',
    'R4LongSpaceNoAssets.1',
    'R5LongSpaceAssets.1',
    'TLongNoSpace.1',
    'TShortWithSpace.1',
    'TPreReq17.1',
    'TPreReqExact.1',
    'TPreReqNoDisplay.1',
    'FreshPlain211619386'
)

foreach ($id in $testIds) {
    Write-Host "Uninstalling $id ..."
    $log = Join-Path $env:TEMP "un-$id.log"
    & $vsixInstaller /q "/uninstall:$id" "/logFile:$log" | Out-Null
    Start-Sleep -Milliseconds 800
}

Write-Host "Installing StickyScroll.vsix ..."
$log = Join-Path $env:TEMP 'install-sticky.log'
& $vsixInstaller /q /admin (Join-Path $root 'StickyScroll.vsix') "/logFile:$log" | Out-Null

Write-Host "Done. exit codes above; check $log"
