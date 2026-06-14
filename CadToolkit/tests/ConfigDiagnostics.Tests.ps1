$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$coreProject = Join-Path $repo 'CadToolkit\src\CadToolkit.Core\CadToolkit.Core.csproj'
$coreDll = Join-Path $repo 'CadToolkit\src\CadToolkit.Core\bin\Release\CadToolkit.Core.dll'
$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'

function Assert-NotNull($name, $value) {
    if ($null -eq $value) { throw "$name was null" }
    Write-Host "PASS $name"
}

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

& $msbuild $coreProject /p:Configuration=Release /p:Platform=x64 /t:Rebuild /v:minimal | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'CadToolkit.Core build failed' }

$assembly = [Reflection.Assembly]::LoadFrom($coreDll)
$diagnosticsType = $assembly.GetType('CadToolkit.Core.ConfigDiagnostics', $true)
$severityType = $assembly.GetType('CadToolkit.Core.ConfigDiagnosticSeverity', $true)
$issueType = $assembly.GetType('CadToolkit.Core.ConfigDiagnosticIssue', $true)
$resultType = $assembly.GetType('CadToolkit.Core.ConfigDiagnosticResult', $true)

Assert-NotNull 'config diagnostics type exists' $diagnosticsType
Assert-NotNull 'config diagnostic severity type exists' $severityType
Assert-NotNull 'config diagnostic issue type exists' $issueType
Assert-NotNull 'config diagnostic result type exists' $resultType
Assert-NotNull 'config diagnostics analyze method exists' ($diagnosticsType.GetMethod('Analyze', [Reflection.BindingFlags]'Public, Static'))
Assert-NotNull 'config diagnostics repair method exists' ($diagnosticsType.GetMethod('Repair', [Reflection.BindingFlags]'Public, Static'))
Assert-NotNull 'config diagnostics analyze file method exists' ($diagnosticsType.GetMethod('AnalyzeFile', [Reflection.BindingFlags]'Public, Static'))
Assert-NotNull 'config diagnostics repair file method exists' ($diagnosticsType.GetMethod('RepairFile', [Reflection.BindingFlags]'Public, Static'))

$coreProjectText = Get-Content -Encoding UTF8 $coreProject -Raw
Assert-Contains 'core project compiles config diagnostics' $coreProjectText 'Compile Include="ConfigDiagnostics\.cs"'
