param(
  [string]$SourceSkillsPath = "",
  [string]$OutputPath = "",
  [string]$TranslationsPath = "",
  [string]$UpstreamArchiveUrl = "https://github.com/obra/superpowers/archive/refs/heads/main.zip"
)

$ErrorActionPreference = "Stop"

function Read-Utf8File {
  param([Parameter(Mandatory = $true)][string]$Path)

  $encoding = [System.Text.UTF8Encoding]::new($false)
  return [System.IO.File]::ReadAllText($Path, $encoding)
}

function Write-Utf8File {
  param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$Content
  )

  $encoding = [System.Text.UTF8Encoding]::new($false)
  [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function ConvertTo-YamlSingleQuoted {
  param([Parameter(Mandatory = $true)][string]$Value)

  return "'" + ($Value -replace "'", "''") + "'"
}

function Update-SkillDescription {
  param(
    [Parameter(Mandatory = $true)][string]$SkillFile,
    [Parameter(Mandatory = $true)][string]$Description
  )

  $content = Read-Utf8File -Path $SkillFile
  $frontmatterPattern = "(?s)\A---\r?\n(?<frontmatter>.*?)\r?\n---(?<body>.*)\z"
  $match = [regex]::Match($content, $frontmatterPattern)

  if (-not $match.Success) {
    Write-Host "::warning file=$SkillFile,title=Missing skill frontmatter::Cannot localize description because frontmatter was not found."
    return
  }

  $descriptionLine = "description: $(ConvertTo-YamlSingleQuoted -Value $Description)"
  $frontmatter = $match.Groups["frontmatter"].Value

  if ([regex]::IsMatch($frontmatter, "(?m)^description:\s*.*$")) {
    $frontmatter = [regex]::Replace($frontmatter, "(?m)^description:\s*.*$", $descriptionLine, 1)
  }
  else {
    $frontmatter = $frontmatter.TrimEnd() + "`n" + $descriptionLine
  }

  $newContent = "---`n$frontmatter`n---" + $match.Groups["body"].Value
  Write-Utf8File -Path $SkillFile -Content $newContent
}

function Copy-SkillsDirectory {
  param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Destination
  )

  if (-not (Test-Path -LiteralPath $Source)) {
    throw "Source skills directory not found: $Source"
  }

  if (Test-Path -LiteralPath $Destination) {
    Remove-Item -LiteralPath $Destination -Recurse -Force
  }

  New-Item -ItemType Directory -Path $Destination -Force | Out-Null
  Copy-Item -Path (Join-Path $Source "*") -Destination $Destination -Recurse -Force
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
  $OutputPath = Join-Path $scriptDir "skills"
}

if ([string]::IsNullOrWhiteSpace($TranslationsPath)) {
  $TranslationsPath = Join-Path $scriptDir "translations.json"
}

if (-not (Test-Path -LiteralPath $TranslationsPath)) {
  throw "Translations file not found: $TranslationsPath"
}

$translations = Read-Utf8File -Path $TranslationsPath | ConvertFrom-Json
$tempRoot = $null

try {
  if ([string]::IsNullOrWhiteSpace($SourceSkillsPath)) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("superpowers-skills-" + [System.Guid]::NewGuid().ToString("N"))
    $archivePath = Join-Path $tempRoot "superpowers.zip"
    $extractPath = Join-Path $tempRoot "extract"

    New-Item -ItemType Directory -Path $tempRoot, $extractPath -Force | Out-Null
    Invoke-WebRequest -Uri $UpstreamArchiveUrl -OutFile $archivePath
    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath -Force

    $SourceSkillsPath = Get-ChildItem -LiteralPath $extractPath -Directory |
      Select-Object -First 1 |
      ForEach-Object { Join-Path $_.FullName "skills" }
  }

  Copy-SkillsDirectory -Source $SourceSkillsPath -Destination $OutputPath

  $skillDirs = Get-ChildItem -LiteralPath $OutputPath -Directory | Sort-Object Name
  foreach ($skillDir in $skillDirs) {
    $skillFile = Join-Path $skillDir.FullName "SKILL.md"

    if (-not (Test-Path -LiteralPath $skillFile)) {
      Write-Host "::warning file=$($skillDir.FullName),title=Missing SKILL.md::$($skillDir.Name) has no SKILL.md file."
      continue
    }

    $translation = $translations.PSObject.Properties[$skillDir.Name]
    if ($null -eq $translation -or [string]::IsNullOrWhiteSpace($translation.Value.description)) {
      Write-Host "::warning file=$TranslationsPath,title=Missing skill translation::$($skillDir.Name) has no Chinese description."
      continue
    }

    Update-SkillDescription -SkillFile $skillFile -Description $translation.Value.description
  }

  Write-Host "Synced $($skillDirs.Count) skills to $OutputPath"
}
finally {
  if ($null -ne $tempRoot -and (Test-Path -LiteralPath $tempRoot)) {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
  }
}
