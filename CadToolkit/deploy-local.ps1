[CmdletBinding()]
param(
    [string]$DeployRoot = 'C:\CadToolkit',
    [string]$MSBuildPath = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe",
    [string]$AutoCADDir = 'C:\Program Files\Autodesk\AutoCAD 2020',
    [string]$ZWCADDir = 'C:\Program Files\ZWSOFT\ZWCAD 2020',
    [string]$GstarCADDir = '',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'

$Base = $PSScriptRoot
$SrcRoot = Join-Path $Base 'src'
$PlatformBin = Join-Path (Join-Path $SrcRoot 'CadToolkit\bin') $Configuration
$CoreDll = Join-Path $SrcRoot "CadToolkit.Core\bin\$Configuration\CadToolkit.Core.dll"
$UiDll = Join-Path $SrcRoot "CadToolkit.UI\bin\$Configuration\CadToolkit.UI.dll"
$UserConfigPath = Join-Path $DeployRoot 'CadToolkit.ini'
$DefaultConfigPath = Join-Path $Base 'CadToolkit.default.ini'
$GstarCADVendorName = -join ([char[]](0x6D69, 0x8FB0, 0x8F6F, 0x4EF6))
$GstarCADProductName = (-join ([char[]](0x6D69, 0x8FB0))) + 'CAD2022'
$ManualFileName = 'CadToolkit' + (-join ([char[]](0x4F7F, 0x7528, 0x624B, 0x518C))) + '.html'

if ([string]::IsNullOrWhiteSpace($GstarCADDir)) {
    $GstarCADDir = Join-Path (Join-Path ${env:ProgramFiles} $GstarCADVendorName) $GstarCADProductName
}

function Get-CadToolkitVersion {
    $assemblyInfo = Get-Content -Encoding UTF8 (Join-Path $SrcRoot 'CadToolkit.Core\Properties\AssemblyInfo.cs') -Raw
    if ($assemblyInfo -notmatch 'AssemblyVersion\("([0-9]+)\.([0-9]+)\.([0-9]+)\.[0-9]+"\)') {
        return 'v1.25'
    }

    $version = "v$($Matches[1]).$($Matches[2])"
    if ($Matches[3] -ne '0') {
        $version = "$version.$($Matches[3])"
    }
    return $version
}

function Assert-PathExists {
    param(
        [string]$Label,
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label not found: $Path"
    }
}

function Invoke-MSBuild {
    param(
        [string]$Project,
        [string]$SdkProperty,
        [string]$SdkDir
    )

    Assert-PathExists $SdkProperty $SdkDir
    Remove-Item -LiteralPath $PlatformBin -Recurse -Force -ErrorAction SilentlyContinue

    & $MSBuildPath $Project `
        "/p:Configuration=$Configuration" `
        "/p:Platform=$Platform" `
        "/p:$SdkProperty=$SdkDir" `
        /t:Rebuild `
        /v:minimal

    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed for $Project with exit code $LASTEXITCODE"
    }
}

function Copy-DeployItem {
    param(
        [string]$Source,
        [string]$Destination
    )

    try {
        Copy-Item -LiteralPath $Source -Destination $Destination -Force
    }
    catch [System.IO.IOException] {
        throw "Close running CAD and retry. Could not update '$Destination': $($_.Exception.Message)"
    }
}

function Copy-PlatformOutput {
    param(
        [string]$OutputName
    )

    $target = Join-Path $DeployRoot $OutputName
    New-Item -ItemType Directory -Path $target -Force | Out-Null

    Copy-DeployItem -Source (Join-Path $PlatformBin 'CadToolkit.dll') -Destination $target
    Copy-DeployItem -Source $CoreDll -Destination $target
    Copy-DeployItem -Source $UiDll -Destination $target
    Remove-Item -LiteralPath (Join-Path $target 'autoload.lsp') -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $target 'CadToolkit.ini') -Force -ErrorAction SilentlyContinue
}

Assert-PathExists 'MSBuild' $MSBuildPath
Assert-PathExists 'default config template' $DefaultConfigPath
New-Item -ItemType Directory -Path $DeployRoot -Force | Out-Null

$hadUserConfig = Test-Path -LiteralPath $UserConfigPath
$beforeUserConfigHash = $null
if ($hadUserConfig) {
    $beforeUserConfigHash = (Get-FileHash -LiteralPath $UserConfigPath -Algorithm SHA256).Hash
}

Write-Host '========================================'
Write-Host '  CadToolkit Local Deploy'
Write-Host '========================================'

Write-Host ''
Write-Host '[1/3] AutoCAD'
Invoke-MSBuild `
    -Project (Join-Path $SrcRoot 'CadToolkit\CadToolkit.AutoCAD.csproj') `
    -SdkProperty 'AutoCADDir' `
    -SdkDir $AutoCADDir
Copy-PlatformOutput 'acad'
Write-Host '  AutoCAD: OK'

Write-Host ''
Write-Host '[2/3] ZWCAD'
Invoke-MSBuild `
    -Project (Join-Path $SrcRoot 'CadToolkit\CadToolkit.ZWCAD.csproj') `
    -SdkProperty 'ZWCADDir' `
    -SdkDir $ZWCADDir
Copy-PlatformOutput 'zwcad'
Write-Host '  ZWCAD: OK'

Write-Host ''
Write-Host '[3/3] GstarCAD'
Invoke-MSBuild `
    -Project (Join-Path $SrcRoot 'CadToolkit\CadToolkit.GstarCAD.csproj') `
    -SdkProperty 'GstarCADDir' `
    -SdkDir $GstarCADDir
Copy-PlatformOutput 'gcad'
Write-Host '  GstarCAD: OK'

$version = Get-CadToolkitVersion
$autoload = Get-Content -Encoding UTF8 (Join-Path $Base 'autoload.lsp') -Raw
$autoload = $autoload -replace 'CadToolkit v[0-9.]+ ready', "CadToolkit $version ready"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $DeployRoot 'autoload.lsp'), $autoload, $utf8NoBom)

Copy-DeployItem -Source $DefaultConfigPath -Destination (Join-Path $DeployRoot 'CadToolkit.default.ini')
if (-not $hadUserConfig) {
    Copy-DeployItem -Source $DefaultConfigPath -Destination $UserConfigPath
    Write-Host '  Config: created CadToolkit.ini from default template'
}
else {
    $afterUserConfigHash = (Get-FileHash -LiteralPath $UserConfigPath -Algorithm SHA256).Hash
    if ($afterUserConfigHash -ne $beforeUserConfigHash) {
        throw 'CadToolkit.ini changed during deployment; aborting to protect user config'
    }
    Write-Host '  Config: existing CadToolkit.ini preserved'
}

$manualPath = Join-Path $Base $ManualFileName
if (Test-Path -LiteralPath $manualPath) {
    Copy-DeployItem -Source $manualPath -Destination (Join-Path $DeployRoot $ManualFileName)
}

$ToolsSource = Join-Path $Base 'tools'
$ToolsTarget = Join-Path $DeployRoot 'tools'
if (Test-Path -LiteralPath $ToolsSource) {
    New-Item -ItemType Directory -Path $ToolsTarget -Force | Out-Null
    Copy-DeployItem -Source (Join-Path $ToolsSource 'check-config.ps1') -Destination (Join-Path $ToolsTarget 'check-config.ps1')
}

Write-Host '========================================'
Write-Host "  Done! Output: $DeployRoot"
Write-Host '========================================'
