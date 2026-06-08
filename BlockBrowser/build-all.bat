@echo off
chcp 65001 >nul
setlocal
echo ========================================
echo   Block Browser - Local Build
echo ========================================
echo.

set "BASE=%~dp0"
set "GCAD_DIR=C:\Program Files\浩辰软件\浩辰CAD2022"
set "ACAD_DIR=C:\Program Files\Autodesk\AutoCAD 2020"
set "ZWCAD_DIR=C:\Program Files\ZWSOFT\ZWCAD 2020"
set "MSBUILD=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
set "OUTPUT=C:\BlockBrowser"
set "BUILD_FAILED=0"

if not exist "%OUTPUT%" mkdir "%OUTPUT%"
if not exist "%OUTPUT%\gcad" mkdir "%OUTPUT%\gcad"
if not exist "%OUTPUT%\zwcad" mkdir "%OUTPUT%\zwcad"

REM === GstarCAD ===
echo [1/3] Building for GstarCAD 2022...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$g=(Get-ChildItem $env:ProgramFiles -Recurse -Filter gmdb.dll -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like '*CAD2022*' } | Select-Object -First 1); if(-not $g){ exit 2 }; $d=$g.DirectoryName; & '%MSBUILD%' '%BASE%BlockBrowser.csproj' /p:Configuration=Release /p:Platform=x64 \"/p:GstarCADDir=$d\" /p:OutputPath='%OUTPUT%\gcad' /t:Rebuild /v:quiet; exit $LASTEXITCODE"
if %ERRORLEVEL% equ 0 (
    echo   OK: %OUTPUT%\gcad\BlockBrowser.dll
) else (
    echo   FAILED
    set "BUILD_FAILED=1"
)

REM === AutoCAD ===
echo [2/3] Building for AutoCAD 2020...
"%MSBUILD%" "%BASE%BlockBrowser.AutoCAD.csproj" /p:Configuration=Release /p:Platform=x64 "/p:AutoCADDir=%ACAD_DIR%" /p:OutputPath="%OUTPUT%\acad" /t:Rebuild /v:quiet
if %ERRORLEVEL% equ 0 (
    echo   OK: %OUTPUT%\acad\BlockBrowser.dll
) else (
    echo   FAILED
    set "BUILD_FAILED=1"
)

REM === ZWCAD ===
echo [3/3] Building for ZWCAD 2020...
"%MSBUILD%" "%BASE%BlockBrowser.ZWCAD.csproj" /p:Configuration=Release /p:Platform=x64 "/p:ZWCADDir=%ZWCAD_DIR%" /p:OutputPath="%OUTPUT%\zwcad" /t:Rebuild /v:quiet
if %ERRORLEVEL% equ 0 (
    echo   OK: %OUTPUT%\zwcad\BlockBrowser.dll
) else (
    echo   FAILED
    set "BUILD_FAILED=1"
)

echo.
copy /Y "%BASE%autoload.lsp" "%OUTPUT%\autoload.lsp" >nul
copy /Y "%BASE%BlockBrowser.default.ini" "%OUTPUT%\BlockBrowser.default.ini" >nul
if not exist "%OUTPUT%\config.ini" (
    copy /Y "%BASE%BlockBrowser.default.ini" "%OUTPUT%\config.ini" >nul
    echo   Config: created config.ini from default template
) else (
    echo   Config: existing config.ini preserved
)
if exist "%OUTPUT%\gcad\config.ini" del /q "%OUTPUT%\gcad\config.ini"
if exist "%OUTPUT%\acad\config.ini" del /q "%OUTPUT%\acad\config.ini"
if exist "%OUTPUT%\zwcad\config.ini" del /q "%OUTPUT%\zwcad\config.ini"

echo.
echo ========================================
echo   Output: %OUTPUT%
echo   - gcad\BlockBrowser.dll  (GstarCAD)
echo   - acad\BlockBrowser.dll  (AutoCAD)
echo   - zwcad\BlockBrowser.dll (ZWCAD)
echo   - autoload.lsp           (auto load)
echo   - BlockBrowser.default.ini (default config template)
echo ========================================
if "%BUILD_FAILED%"=="1" exit /b 1
if "%BLOCKBROWSER_PAUSE%"=="1" pause
