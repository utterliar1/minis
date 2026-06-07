$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$buildAll = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\build-all.bat') -Raw
$workflow = Get-Content -Encoding UTF8 (Join-Path $repo '.github\workflows\cadtoolkit.yml') -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'local deploy publishes default config template' $buildAll 'CadToolkit\.default\.ini'
Assert-Contains 'local deploy only creates user config when missing' $buildAll 'if not exist "%DEPLOY%\\CadToolkit\.ini"'
Assert-NotContains 'local deploy does not overwrite user config' $buildAll 'copy /Y "%BASE%CadToolkit\.ini" "%DEPLOY%\\?"'

Assert-Contains 'release package includes default config template' $workflow 'CadToolkit\.default\.ini'
Assert-NotContains 'release package does not include user config name' $workflow 'Copy-Item "\$\{\{ github\.workspace \}\}\\CadToolkit\\CadToolkit\.ini" "\$pkg\\?"'
