@echo off
chcp 65001 >nul
echo ========================================
echo   Block Browser - Multi-CAD Build
echo ========================================
echo.

set "GCAD_DIR=C:\Program Files\浩辰软件\浩辰CAD2022"
set "ACAD_DIR=C:\Program Files\Autodesk\AutoCAD 2026"
set "ZWCAD_DIR=C:\Program Files\ZWSOFT\ZWCAD 2022"
set "MSBUILD=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
set "OUTPUT=D:\Documents\cad插件\BlockBrowser\bin\Release"

if not exist "%OUTPUT%" mkdir "%OUTPUT%"

REM === GstarCAD ===
echo [1/3] Building for GstarCAD 2022...
"%MSBUILD%" BlockBrowser.csproj /p:Configuration=Release /p:Platform=x64 "/p:GstarCADDir=%GCAD_DIR%" /p:OutputPath="%OUTPUT%\gcad\" /t:Rebuild /v:quiet
if %ERRORLEVEL% equ 0 (
    echo   OK: %OUTPUT%\gcad\BlockBrowser.dll
) else (
    echo   FAILED
)

REM === AutoCAD ===
echo [2/3] Building for AutoCAD 2026...
dotnet build BlockBrowser.AutoCAD8.csproj -c Release -o "%OUTPUT%\acad\" --nologo -v quiet 2>nul
if %ERRORLEVEL% equ 0 (
    echo   OK: %OUTPUT%\acad\BlockBrowser.dll
) else (
    echo   FAILED
)

REM === ZWCAD ===
echo [3/3] Building for ZWCAD 2022...
"%MSBUILD%" BlockBrowser.ZWCAD.csproj /p:Configuration=Release /p:Platform=x64 "/p:ZWCADDir=%ZWCAD_DIR%" /p:OutputPath="%OUTPUT%\zwcad\" /t:Rebuild /v:quiet
if %ERRORLEVEL% equ 0 (
    echo   OK: %OUTPUT%\zwcad\BlockBrowser.dll
) else (
    echo   FAILED
)

echo.
echo ========================================
echo   Output: %OUTPUT%
echo   - gcad\BlockBrowser.dll  (浩辰CAD)
echo   - acad\BlockBrowser.dll  (AutoCAD)
echo   - zwcad\BlockBrowser.dll (中望CAD)
echo   - autoload.lsp           (自动加载)
echo ========================================
pause