$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$scriptPath = Join-Path $root 'build-all.bat'
$script = Get-Content -Encoding UTF8 $scriptPath -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'build script can find MSBuild through vswhere' $script 'vswhere\.exe'
Assert-Contains 'build script searches common Visual Studio MSBuild paths' $script 'Microsoft Visual Studio\\2022'
Assert-Contains 'build script falls back to framework MSBuild' $script 'Framework64\\v4\.0\.30319\\MSBuild\.exe'
Assert-Contains 'build script reports selected MSBuild' $script 'MSBuild:'
Assert-Contains 'build script warns when net48 reference assemblies are missing' $script 'Reference Assemblies\\Microsoft\\Framework\\.NETFramework\\v4\.8'

Write-Host 'BuildScript.Tests.ps1 passed'
