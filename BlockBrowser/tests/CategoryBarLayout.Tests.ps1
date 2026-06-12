$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$formSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\Forms\BlockBrowserForm.cs') -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-True($name, $actual) {
    if (-not $actual) { throw "$name failed. Expected true." }
    Write-Host "PASS $name"
}

Assert-Contains 'form declares category host panel' $formSource 'private\s+Panel\s+_catHost;'
Assert-Contains 'form declares category table layout' $formSource 'private\s+TableLayoutPanel\s+_catLayout;'
Assert-Contains 'form declares category viewport panel' $formSource 'private\s+Panel\s+_catViewport;'
Assert-Contains 'form declares create category panel' $formSource 'private\s+Panel\s+_catActionPanel;'
Assert-NotContains 'form does not declare left category scroll button' $formSource 'private\s+Button\s+_btnCatScrollLeft;'
Assert-NotContains 'form does not declare right category scroll button' $formSource 'private\s+Button\s+_btnCatScrollRight;'
Assert-Contains 'form declares create category button' $formSource 'private\s+Button\s+_btnCreateCategory;'
Assert-Contains 'form declares native category scrollbar' $formSource 'private\s+HScrollBar\s+_catScrollBar;'
Assert-Contains 'category host docks top' $formSource '_catHost\s*=\s*new\s+Panel[\s\S]*?Dock\s*=\s*DockStyle\.Top'
Assert-Contains 'category host starts tall enough for native category buttons' $formSource '_catHost\s*=\s*new\s+Panel[\s\S]*?Height\s*=\s*44'
Assert-Contains 'category layout rows start collapsed except category row' $formSource '_catLayout\.RowStyles\.Add\(new\s+RowStyle\(SizeType\.Absolute,\s*44\)\)[\s\S]*?_catLayout\.RowStyles\.Add\(new\s+RowStyle\(SizeType\.Absolute,\s*0\)\)[\s\S]*?_catLayout\.RowStyles\.Add\(new\s+RowStyle\(SizeType\.Absolute,\s*0\)\)'
Assert-Contains 'category host expands only when scrollbar is needed' $formSource '_catHost\.Height\s*=\s*needsScroll\s*\?\s*44\s*\+\s*8\s*\+\s*SystemInformation\.HorizontalScrollBarHeight\s*:\s*44'
Assert-Contains 'category gap row expands only when scrollbar is needed' $formSource '_catLayout\.RowStyles\[1\]\.Height\s*=\s*needsScroll\s*\?\s*8\s*:\s*0'
Assert-Contains 'category scrollbar row expands only when scrollbar is needed' $formSource '_catLayout\.RowStyles\[2\]\.Height\s*=\s*needsScroll\s*\?\s*SystemInformation\.HorizontalScrollBarHeight\s*:\s*0'
Assert-Contains 'category layout has category and action columns' $formSource '_catLayout\.ColumnStyles\.Add\(new\s+ColumnStyle\(SizeType\.Percent,\s*100f\)\)[\s\S]*?_catLayout\.ColumnStyles\.Add\(new\s+ColumnStyle\(SizeType\.Absolute,\s*36\)\)'
Assert-Contains 'category viewport fills host' $formSource '_catViewport\s*=\s*new\s+Panel[\s\S]*?Dock\s*=\s*DockStyle\.Fill'
Assert-Contains 'category bar disables internal autoscroll' $formSource '_catBar\s*=\s*new\s+FlowLayoutPanel[\s\S]*?AutoScroll\s*=\s*false'
Assert-Contains 'category bar leaves extra bottom edge breathing room' $formSource '_catBar\s*=\s*new\s+FlowLayoutPanel[\s\S]*?Padding\s*=\s*new\s+Padding\(10,\s*4,\s*10,\s*8\)'
Assert-Contains 'category buttons use native autosize style' $formSource 'Text\s*=\s*cat[\s\S]*?AutoSize\s*=\s*true'
Assert-Contains 'category buttons keep native minimum height' $formSource 'Text\s*=\s*cat[\s\S]*?MinimumSize\s*=\s*new\s+Size\(0,\s*24\)'
Assert-Contains 'category buttons use previous native padding' $formSource 'Text\s*=\s*cat[\s\S]*?Padding\s*=\s*new\s+Padding\(8,\s*1,\s*8,\s*1\)'
Assert-Contains 'category buttons use previous native margin' $formSource 'Text\s*=\s*cat[\s\S]*?Margin\s*=\s*new\s+Padding\(2,\s*0,\s*2,\s*0\)'
Assert-NotContains 'category buttons do not force fixed width' $formSource 'Size\s*=\s*new\s+Size\(catWidth,\s*28\)'
Assert-Contains 'category native scrollbar fills own row' $formSource '_catScrollBar\s*=\s*new\s+HScrollBar[\s\S]*?Dock\s*=\s*DockStyle\.Fill'
Assert-Contains 'category native scrollbar moves category bar' $formSource '_catScrollBar\.ValueChanged\s*\+=\s*\(s,\s*e\)\s*=>\s*\{\s*_catBar\.Left\s*=\s*-_catScrollBar\.Value;\s*\}'
Assert-Contains 'create category button aligns with taller category row' $formSource '_btnCreateCategory\s*=\s*new\s+Button[\s\S]*?Dock\s*=\s*DockStyle\.None[\s\S]*?Size\s*=\s*new\s+Size\(28,\s*24\)[\s\S]*?Location\s*=\s*new\s+Point\(4,\s*10\)'
Assert-Contains 'create category button uses flat style' $formSource '_btnCreateCategory\s*=\s*new\s+Button[\s\S]*?FlatStyle\s*=\s*FlatStyle\.Flat'
Assert-Contains 'create category button has no heavy border' $formSource '_btnCreateCategory\.FlatAppearance\.BorderSize\s*=\s*0'
Assert-Contains 'plus button triggers create category' $formSource '_btnCreateCategory\.Click\s*\+=\s*BtnCreateCategory_Click'
Assert-Contains 'category viewport contains category bar' $formSource '_catViewport\.Controls\.Add\(_catBar\)'
Assert-Contains 'category action panel contains plus button' $formSource '_catActionPanel\.Controls\.Add\(_btnCreateCategory\)'
Assert-Contains 'category layout places viewport top left' $formSource '_catLayout\.Controls\.Add\(_catViewport,\s*0,\s*0\)'
Assert-Contains 'category layout places gap below category row' $formSource '_catLayout\.Controls\.Add\(scrollGap,\s*0,\s*1\)'
Assert-Contains 'category layout places native scrollbar below gap' $formSource '_catLayout\.Controls\.Add\(_catScrollBar,\s*0,\s*2\)'
Assert-Contains 'category layout places action panel top right' $formSource '_catLayout\.Controls\.Add\(_catActionPanel,\s*1,\s*0\)'
Assert-Contains 'category layout leaves right gap blank' $formSource '_catLayout\.Controls\.Add\(actionGap,\s*1,\s*1\)'
Assert-Contains 'category layout leaves bottom right blank' $formSource '_catLayout\.Controls\.Add\(scrollSpacer,\s*1,\s*2\)'
Assert-Contains 'category host contains category layout' $formSource '_catHost\.Controls\.Add\(_catLayout\)'
Assert-NotContains 'main form no direct category bar add' $formSource '(?m)^\s*Controls\.Add\(_catBar\);'

$createCategoryMatch = [regex]::Match($formSource, 'private\s+void\s+BtnCreateCategory_Click\(object\s+sender,\s+EventArgs\s+e\)(?<body>[\s\S]*?)private\s+void\s+BtnAddToLib_Click')
Assert-True 'create category handler found' $createCategoryMatch.Success
$createCategoryBody = $createCategoryMatch.Groups['body'].Value
Assert-NotContains 'create category does not trigger full data reload' $createCategoryBody 'LoadData\s*\('
Assert-Contains 'create category refreshes only category strip' $createCategoryBody 'RefreshCategories\s*\('

Write-Host 'CategoryBarLayout.Tests.ps1 passed'
