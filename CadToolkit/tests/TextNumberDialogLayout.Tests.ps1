$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$dialogs = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.UI\Dialogs.cs') -Raw

function Assert-Match($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name did not find pattern: $pattern"
    }
    Write-Host "PASS $name"
}

function Assert-NotMatch($name, $text, $pattern) {
    if ($text -match $pattern) {
        throw "$name found forbidden pattern: $pattern"
    }
    Write-Host "PASS $name"
}

function Assert-NumberAtLeast($name, $actual, $minimum) {
    if ([int]$actual -lt [int]$minimum) {
        throw "$name expected at least $minimum but got $actual"
    }
    Write-Host "PASS $name"
}

$dialogMatch = [regex]::Match($dialogs, 'public class TextNumberDialog : Form(?<body>[\s\S]*?)public class ManageCommandsDialog')
if (!$dialogMatch.Success) { throw 'TextNumberDialog source block not found' }
$body = $dialogMatch.Groups['body'].Value

$sizeMatch = [regex]::Match($body, 'ClientSize\s*=\s*new Size\((?<width>\d+),\s*(?<height>\d+)\)')
if (!$sizeMatch.Success) { throw 'TextNumberDialog ClientSize not found' }
Assert-NumberAtLeast 'text number dialog width leaves room for Chinese labels' $sizeMatch.Groups['width'].Value 420

$replaceMatch = [regex]::Match($body, 'chkReplace\.Width\s*=\s*(?<width>\d+)')
if (!$replaceMatch.Success) { throw 'TextNumberDialog replace checkbox width not found' }
Assert-NumberAtLeast 'text number replace checkbox avoids wrapping' $replaceMatch.Groups['width'].Value 220

Assert-Match 'text number dialog keeps replace text on one row' $body 'chkReplace\.Height\s*=\s*24'
Assert-Match 'text number dialog defaults focus to start number' $body 'Shown\s*\+=\s*delegate\s*\{\s*t3\.Focus\(\);\s*t3\.SelectAll\(\);\s*\};'
Assert-NotMatch 'text number dialog no longer defaults focus to prefix' $body 'Shown\s*\+=\s*delegate\s*\{\s*t1\.Focus\(\);\s*\};'
