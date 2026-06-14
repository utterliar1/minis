param(
    [string]$Path = 'C:\CadToolkit\CadToolkit.ini',
    [switch]$Fix
)

$ErrorActionPreference = 'Stop'

$base = Resolve-Path (Join-Path $PSScriptRoot '..')
$coreDll = Join-Path $base 'src\CadToolkit.Core\bin\Release\CadToolkit.Core.dll'
$coreProject = Join-Path $base 'src\CadToolkit.Core\CadToolkit.Core.csproj'
$msbuild = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"

if (-not (Test-Path -LiteralPath $coreDll)) {
    if (-not (Test-Path -LiteralPath $coreProject)) {
        $deployCoreDll = Join-Path $base 'acad\CadToolkit.Core.dll'
        if (-not (Test-Path -LiteralPath $deployCoreDll)) {
            $deployCoreDll = Join-Path $base 'CadToolkit.Core.dll'
        }
        $coreDll = $deployCoreDll
    }
    else {
        if (-not (Test-Path -LiteralPath $msbuild)) {
            throw "MSBuild not found: $msbuild"
        }

        & $msbuild $coreProject /p:Configuration=Release /p:Platform=x64 /t:Build /v:minimal | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw 'CadToolkit.Core build failed'
        }
    }
}

if (-not (Test-Path -LiteralPath $coreDll)) {
    throw "CadToolkit.Core.dll not found: $coreDll"
}

$assembly = [Reflection.Assembly]::LoadFrom($coreDll)
$diagnosticsType = $assembly.GetType('CadToolkit.Core.ConfigDiagnostics', $true)

if ($Fix) {
    $result = $diagnosticsType.GetMethod('RepairFile').Invoke($null, [object[]]@($Path))
}
else {
    $result = $diagnosticsType.GetMethod('AnalyzeFile').Invoke($null, [object[]]@($Path))
}

$report = [string]$diagnosticsType.GetMethod('FormatReport').Invoke($null, [object[]]@($result))
Write-Host $report

if ($result.HasErrors) {
    exit 1
}

exit 0
