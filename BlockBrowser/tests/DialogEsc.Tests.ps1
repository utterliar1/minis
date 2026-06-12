$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repo 'BlockBrowser'

function Assert-True($name, $actual) {
    if (-not $actual) { throw "$name failed. Expected true." }
    Write-Host "PASS $name"
}

function Assert-DialogHasEscapeCancel($file) {
    $path = Join-Path $project $file
    Assert-True ("dialog file exists " + $file) (Test-Path $path)
    $source = Get-Content -Encoding UTF8 $path -Raw
    $hasCancelButton = $source -match 'CancelButton\s*='
    $hasEscHandler = ($source -match 'KeyPreview\s*=\s*true') -and ($source -match 'Keys\.Escape')
    Assert-True ("dialog supports ESC cancel " + $file) ($hasCancelButton -or $hasEscHandler)
}

$dialogs = @(
    'Forms\ExportBlocksDialog.cs',
    'Forms\InsertSettingsDialog.cs',
    'Forms\MirrorPreviewDialog.cs',
    'Forms\SettingsDialog.cs',
    'Forms\StatusDiagnosticsDialog.cs',
    'Forms\SyncCenterDialog.cs',
    'Forms\TextPromptDialog.cs'
)

foreach ($dialog in $dialogs) {
    Assert-DialogHasEscapeCancel $dialog
}

Write-Host 'DialogEsc.Tests.ps1 passed'
