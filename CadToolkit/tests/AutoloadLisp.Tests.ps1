$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$autoload = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\autoload.lsp') -Raw
$deployLocal = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\deploy-local.ps1') -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name did not find pattern: $pattern"
    }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) {
        throw "$name found forbidden pattern: $pattern"
    }
    Write-Host "PASS $name"
}

function Assert-BalancedParens($text) {
    $balance = 0
    $lineNo = 0
    $inString = $false
    $escaped = $false
    foreach ($line in ($text -split "`r?`n")) {
        $lineNo++
        foreach ($ch in $line.ToCharArray()) {
            if ($inString) {
                if ($escaped) {
                    $escaped = $false
                } elseif ($ch -eq '\') {
                    $escaped = $true
                } elseif ($ch -eq '"') {
                    $inString = $false
                }
                continue
            }

            if ($ch -eq ';') { break }
            if ($ch -eq '"') {
                $inString = $true
            } elseif ($ch -eq '(') {
                $balance++
            } elseif ($ch -eq ')') {
                $balance--
                if ($balance -lt 0) {
                    throw "autoload.lsp has an extra closing paren at line $lineNo"
                }
            }
        }
    }

    if ($balance -ne 0) {
        throw "autoload.lsp has unbalanced parens: $balance"
    }
    Write-Host 'PASS autoload lisp parens are balanced outside strings'
}

Assert-BalancedParens $autoload
Assert-Contains 'local deploy writes autoload without UTF8 BOM' $deployLocal 'New-Object\s+System\.Text\.UTF8Encoding\(\$false\)'
Assert-Contains 'local deploy writes autoload through explicit encoder' $deployLocal '\[System\.IO\.File\]::WriteAllText'
Assert-Contains 'autoload keeps CC alias command' $autoload '\(defun\s+c:CC'
Assert-Contains 'autoload keeps CT alias command' $autoload '\(defun\s+c:CT'
Assert-Contains 'autoload announces current version' $autoload 'CadToolkit v1\.25 ready'
