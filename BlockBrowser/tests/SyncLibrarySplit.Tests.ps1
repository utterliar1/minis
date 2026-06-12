$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repo 'BlockBrowser'
$syncRootPath = Join-Path $project 'Library\BlockLibrary.Sync.cs'
$syncRootSource = Get-Content -Encoding UTF8 $syncRootPath -Raw
$csprojSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.csproj') -Raw
$acadSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.AutoCAD.csproj') -Raw
$zwcadSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.ZWCAD.csproj') -Raw

function Assert-True($name, $actual) {
    if (-not $actual) { throw "$name failed. Expected true." }
    Write-Host "PASS $name"
}

function Assert-False($name, $actual) {
    if ($actual) { throw "$name failed. Expected false." }
    Write-Host "PASS $name"
}

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

$splitFiles = @(
    'Library\BlockLibrary.LocalChanges.cs',
    'Library\BlockLibrary.Mirror.cs',
    'Library\BlockLibrary.SyncPlanning.cs',
    'Library\BlockLibrary.NasUpload.cs'
)

foreach ($file in $splitFiles) {
    $path = Join-Path $project $file
    Assert-True ("sync split file exists " + $file) (Test-Path $path)
    $source = Get-Content -Encoding UTF8 $path -Raw
    Assert-Contains ("sync split file declares partial BlockLibrary " + $file) $source 'public\s+static\s+partial\s+class\s+BlockLibrary'
    Assert-Contains ("main project references " + $file) $csprojSource ('Compile Include="' + [regex]::Escape($file) + '"')
    Assert-Contains ("AutoCAD project references " + $file) $acadSource ('Compile Include="' + [regex]::Escape($file) + '"')
    Assert-Contains ("ZWCAD project references " + $file) $zwcadSource ('Compile Include="' + [regex]::Escape($file) + '"')
}

$localChangesSource = Get-Content -Encoding UTF8 (Join-Path $project 'Library\BlockLibrary.LocalChanges.cs') -Raw
$mirrorSource = Get-Content -Encoding UTF8 (Join-Path $project 'Library\BlockLibrary.Mirror.cs') -Raw
$planningSource = Get-Content -Encoding UTF8 (Join-Path $project 'Library\BlockLibrary.SyncPlanning.cs') -Raw
$uploadSource = Get-Content -Encoding UTF8 (Join-Path $project 'Library\BlockLibrary.NasUpload.cs') -Raw

Assert-Contains 'local changes file owns RecordLocalChange' $localChangesSource 'public\s+static\s+void\s+RecordLocalChange\('
Assert-Contains 'local changes file owns protected path helper' $localChangesSource 'GetProtectedLocalPaths\('
Assert-Contains 'mirror file owns local mirror preview' $mirrorSource 'public\s+static\s+MirrorDirectoryResult\s+PreviewLocalMirrorFromNas\(\)'
Assert-Contains 'mirror file owns local mirror update' $mirrorSource 'public\s+static\s+MirrorDirectoryResult\s+UpdateLocalMirrorFromNas\(\)'
Assert-Contains 'mirror file applies mirror preview' $mirrorSource 'ApplyMirrorDirectoryResult\('
Assert-Contains 'planning file owns BuildSnapshots' $planningSource 'public\s+static\s+List<SyncFileSnapshot>\s+BuildSnapshots\('
Assert-Contains 'planning file owns PreviewLocalSync' $planningSource 'public\s+static\s+SyncPlan\s+PreviewLocalSync\(\)'
Assert-Contains 'planning file discovers local-only files' $planningSource 'LocalOnlySyncDiscovery\.Discover\('
Assert-Contains 'upload file owns SyncSafeUploadsToNas' $uploadSource 'public\s+static\s+SyncPlan\s+SyncSafeUploadsToNas\(\)'
Assert-Contains 'upload file owns NAS permission guard' $uploadSource 'EnsureNasSyncAllowed\(\)'
Assert-Contains 'upload file copies upload candidates' $uploadSource 'File\.Copy\(src,\s*dst,\s*false\)'

Assert-Contains 'sync root stays partial BlockLibrary' $syncRootSource 'public\s+static\s+partial\s+class\s+BlockLibrary'
Assert-False 'sync root no direct RecordLocalChange body' ($syncRootSource -match 'public\s+static\s+void\s+RecordLocalChange\(')
Assert-False 'sync root no direct mirror body' ($syncRootSource -match 'PreviewLocalMirrorFromNas\(|UpdateLocalMirrorFromNas\(')
Assert-False 'sync root no direct planning body' ($syncRootSource -match 'BuildSnapshots\(|PreviewLocalSync\(')
Assert-False 'sync root no direct upload body' ($syncRootSource -match 'SyncSafeUploadsToNas\(|EnsureNasSyncAllowed\(')

Write-Host 'SyncLibrarySplit.Tests.ps1 passed'
