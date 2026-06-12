$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$dialogPath = Join-Path $repo 'BlockBrowser\Forms\TextPromptDialog.cs'
$formPath = Join-Path $repo 'BlockBrowser\Forms\BlockBrowserForm.cs'

if (-not (Test-Path $dialogPath)) {
    throw "Missing dialog source file: $dialogPath"
}

$dialogSource = Get-Content -Encoding UTF8 $dialogPath -Raw
$formSource = Get-Content -Encoding UTF8 $formPath -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'prompt dialog class exists' $dialogSource 'class\s+TextPromptDialog\s*:\s*Form'
Assert-Contains 'prompt dialog uses dpi scaling' $dialogSource 'AutoScaleMode\s*=\s*AutoScaleMode\.Dpi'
Assert-Contains 'prompt dialog uses table layout' $dialogSource 'new\s+TableLayoutPanel'
Assert-Contains 'prompt dialog supports text input' $dialogSource 'ForTextInput'
Assert-Contains 'prompt dialog supports combo input' $dialogSource 'ForComboInput'
Assert-Contains 'prompt dialog exposes value' $dialogSource 'string\s+Value'
Assert-Contains 'form opens text prompt dialog' $formSource 'TextPromptDialog\.ForTextInput'
Assert-Contains 'form opens combo prompt dialog' $formSource 'TextPromptDialog\.ForComboInput'
Assert-NotContains 'form no fixed input dialog size' $formSource 'form\.Size\s*=\s*new\s+Size\(350,\s*150\)'
Assert-NotContains 'form no fixed category dialog size' $formSource 'form\.Text\s*=\s*title;\s*form\.Size\s*=\s*new\s+Size\(350,\s*180\)'

Write-Host 'TextPromptDialog.Tests.ps1 passed'
