$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Encoding UTF8 (Join-Path $root 'Forms\BlockThumbnailCard.cs') -Raw
$thumbnailFailedText = -join ([char[]](0x7F29, 0x7565, 0x56FE, 0x751F, 0x6210, 0x5931, 0x8D25))

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

Assert-NotContains 'card no longer uses tight fixed 22px label height' $source 'private\s+const\s+int\s+LabelHeight\s*=\s*22'
Assert-Contains 'card defines minimum label height' $source 'LabelMinHeight'
Assert-Contains 'card calculates label height from font' $source 'GetLabelHeight\s*\(\s*Font\s+font\s*\)'
Assert-Contains 'card uses image frame for quiet border' $source '_imageFrame'
Assert-Contains 'card has thumbnail failure flag' $source '_thumbnailFailed'
Assert-Contains 'card has thumbnail failure badge' $source '_statusBadge'
Assert-Contains 'card exposes thumbnail failure state setter' $source 'public\s+void\s+SetThumbnailFailed\s*\(\s*bool\s+failed\s*\)'
Assert-Contains 'picture box has no heavy native border' $source 'BorderStyle\s*=\s*BorderStyle\.None'
Assert-Contains 'label is docked at bottom' $source 'Dock\s*=\s*DockStyle\.Bottom'
Assert-Contains 'label uses calculated height' $source 'Height\s*=\s*_labelHeight'
Assert-Contains 'label uses ellipsis for long block names' $source 'AutoEllipsis\s*=\s*true'
Assert-Contains 'label disables mnemonic parsing' $source 'UseMnemonic\s*=\s*false'
Assert-Contains 'tooltip is centralized' $source 'ApplyToolTip'
Assert-Contains 'tooltip includes file path' $source 'block\.FilePath'
Assert-Contains 'tooltip includes thumbnail failure message' $source ([regex]::Escape($thumbnailFailedText))
Assert-Contains 'selection state updates label color' $source '_lbl\.ForeColor'
Assert-Contains 'selection state updates label background' $source '_lbl\.BackColor'
Assert-Contains 'inner surface is clickable' $source '_panel\.Click\s*\+=\s*onClick'
Assert-Contains 'image frame is clickable' $source '_imageFrame\.Click\s*\+=\s*onClick'
Assert-Contains 'placeholder resizes image frame' $source '_imageFrame\.Height\s*=\s*thumbSize\s*\+\s*ImageFrameExtra'
Assert-Contains 'placeholder recalculates label height' $source 'SetPlaceholder\(int\s+thumbSize\)[\s\S]*?_labelHeight\s*=\s*GetLabelHeight\(_lbl\.Font\)'
Assert-Contains 'placeholder clears failed thumbnail state' $source 'SetPlaceholder\(int\s+thumbSize\)[\s\S]*?SetThumbnailFailed\(false\)'
Assert-Contains 'successful thumbnail load clears failed thumbnail state' $source 'LoadThumbnail\(Image\s+img\)[\s\S]*?SetThumbnailFailed\(false\)'
Assert-Contains 'hover events are wired recursively' $source 'WireHoverEvents\(this\)'
Assert-Contains 'hover wiring visits child controls' $source 'foreach\s*\(Control\s+child\s+in\s+control\.Controls\)'
Assert-Contains 'hover enter is shared by all card children' $source 'control\.MouseEnter\s*\+=\s*CardMouseEnter'
Assert-Contains 'hover leave is shared by all card children' $source 'control\.MouseLeave\s*\+=\s*CardMouseLeave'
Assert-Contains 'hover leave checks actual mouse position' $source 'UpdateHoverFromMousePosition'
Assert-Contains 'hover detection uses card client rectangle' $source 'ClientRectangle\.Contains\(PointToClient\(Control\.MousePosition\)\)'

Write-Host 'BlockThumbnailCardVisual.Tests.ps1 passed'
