$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$dialogs = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.UI\Dialogs.cs') -Raw
$textCommands = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit\TextCommands.cs') -Raw

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

function Assert-NumberAtMost($name, $actual, $maximum) {
    if ([int]$actual -gt [int]$maximum) {
        throw "$name expected at most $maximum but got $actual"
    }
    Write-Host "PASS $name"
}

$dialogMatch = [regex]::Match($dialogs, 'public class TextNumberDialog : Form(?<body>[\s\S]*?)public class ManageCommandsDialog')
if (!$dialogMatch.Success) { throw 'TextNumberDialog source block not found' }
$body = $dialogMatch.Groups['body'].Value

$sizeMatch = [regex]::Match($body, 'ClientSize\s*=\s*new Size\((?<width>\d+),\s*(?<height>\d+)\)')
if (!$sizeMatch.Success) { throw 'TextNumberDialog ClientSize not found' }
Assert-NumberAtLeast 'text number dialog width leaves room for Chinese labels' $sizeMatch.Groups['width'].Value 320
Assert-NumberAtMost 'text number dialog avoids old wide empty layout' $sizeMatch.Groups['width'].Value 360
Assert-NumberAtMost 'text number dialog avoids tall empty layout' $sizeMatch.Groups['height'].Value 120

Assert-Match 'text number dialog exposes numbering mode enum' $dialogs 'enum\s+TextNumberMode'
Assert-Match 'text number dialog has prefix option' $body 'rbPrefix\.Text\s*=\s*"\\u524D\\u7F00"'
Assert-Match 'text number dialog has suffix option' $body 'rbSuffix\.Text\s*=\s*"\\u540E\\u7F00"'
Assert-Match 'text number dialog has replace option' $body 'rbReplace\.Text\s*=\s*"\\u66FF\\u6362"'
Assert-Match 'text number dialog defaults suffix option checked' $body 'rbSuffix\.Checked\s*=\s*true'
Assert-NotMatch 'text number dialog no longer uses prefix text input' $body 'new\s+TextBox\(\);\s*t1\.Left'
Assert-NotMatch 'text number dialog no longer uses suffix text input' $body 'new\s+TextBox\(\);\s*t2\.Left'
Assert-Match 'text number dialog defaults focus to start number' $body 'Shown\s*\+=\s*delegate\s*\{\s*t3\.Focus\(\);\s*t3\.SelectAll\(\);\s*\};'
Assert-NotMatch 'text number dialog no longer defaults focus to prefix' $body 'Shown\s*\+=\s*delegate\s*\{\s*t1\.Focus\(\);\s*\};'
Assert-Match 'text number command reads numbering mode' $textCommands 'mode\s*=\s*dlg\.Mode'
Assert-Match 'text number command can place number before original text' $textCommands 'TextNumberMode\.Prefix'
Assert-Match 'text number command can place number after original text' $textCommands 'TextNumberMode\.Suffix'
Assert-Match 'text number command can replace original text' $textCommands 'TextNumberMode\.Replace'
