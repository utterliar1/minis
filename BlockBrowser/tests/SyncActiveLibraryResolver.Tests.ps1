$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$syncFiles = @(
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'Sync\ActiveLibraryResolver.cs'
)

foreach ($file in $syncFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing sync source file: $file"
    }
}

Add-Type -Path $syncFiles -ReferencedAssemblies @(
    'System.Runtime.Serialization.dll',
    'System.Xml.dll',
    'System.Core.dll'
)

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        throw "$name failed. Expected: [$expected], Actual: [$actual]"
    }
    Write-Host "PASS $name"
}

$settings = New-Object BlockBrowser.SyncSettings
$settings.LibraryPath = '\\NAS\CADBlocks\BlockBrowser'
$settings.LocalMirrorPath = 'D:\CADBlocks\BlockBrowser'
$settings.PreferLocalWhenNasUnavailable = $true
$settings.CurrentLibraryMode = [BlockBrowser.LibraryMode]::Auto
$settings.AllowNasSync = $true

$autoNas = [BlockBrowser.ActiveLibraryResolver]::Resolve($settings, $true, $true)
Assert-Equal 'auto mode uses NAS when available' ([BlockBrowser.ActiveLibraryKind]::Nas) $autoNas.Kind
Assert-Equal 'auto mode active path is NAS' '\\NAS\CADBlocks\BlockBrowser' $autoNas.ActivePath

$settings.AllowNasSync = $false
$readonlyAutoLocal = [BlockBrowser.ActiveLibraryResolver]::Resolve($settings, $true, $true)
Assert-Equal 'readonly auto mode prefers local mirror when NAS is available' ([BlockBrowser.ActiveLibraryKind]::LocalMirror) $readonlyAutoLocal.Kind
Assert-Equal 'readonly auto path is local mirror' 'D:\CADBlocks\BlockBrowser' $readonlyAutoLocal.ActivePath

$readonlyAutoNas = [BlockBrowser.ActiveLibraryResolver]::Resolve($settings, $true, $false)
Assert-Equal 'readonly auto can browse NAS when local mirror is missing' ([BlockBrowser.ActiveLibraryKind]::Nas) $readonlyAutoNas.Kind
Assert-Equal 'readonly auto browse path is NAS' '\\NAS\CADBlocks\BlockBrowser' $readonlyAutoNas.ActivePath

$settings.AllowNasSync = $true
$autoLocal = [BlockBrowser.ActiveLibraryResolver]::Resolve($settings, $false, $true)
Assert-Equal 'auto mode falls back to local mirror' ([BlockBrowser.ActiveLibraryKind]::LocalMirror) $autoLocal.Kind
Assert-Equal 'auto fallback path is local mirror' 'D:\CADBlocks\BlockBrowser' $autoLocal.ActivePath

Write-Host 'SyncActiveLibraryResolver.Tests.ps1 passed'
