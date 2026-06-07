@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
echo ========================================
echo   CadToolkit Build
echo ========================================

set "MSBUILD=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
set "BASE=%~dp0"
set "DEPLOY=C:\CadToolkit"
set "CT_VERSION=v1.23.2"

for /f "tokens=2 delims=()" %%V in ('findstr /C:"AssemblyVersion" "%BASE%src\CadToolkit.Core\Properties\AssemblyInfo.cs"') do (
    set "ASM_VERSION=%%~V"
)
if defined ASM_VERSION (
    for /f "tokens=1,2,3 delims=." %%A in ("!ASM_VERSION!") do (
        set "CT_VERSION=v%%A.%%B"
        if not "%%C"=="0" if not "%%C"=="" set "CT_VERSION=v%%A.%%B.%%C"
    )
)
setlocal DisableDelayedExpansion
if not exist "%DEPLOY%\acad" mkdir "%DEPLOY%\acad"
if not exist "%DEPLOY%\zwcad" mkdir "%DEPLOY%\zwcad"
if not exist "%DEPLOY%\gcad" mkdir "%DEPLOY%\gcad"

echo.
echo [1/3] AutoCAD
if exist "%BASE%src\CadToolkit\bin\Release" rmdir /s /q "%BASE%src\CadToolkit\bin\Release"
%MSBUILD% "%BASE%src\CadToolkit\CadToolkit.AutoCAD.csproj" /p:Configuration=Release /p:Platform=x64 "/p:AutoCADDir=C:\Program Files\Autodesk\AutoCAD 2020" /t:Rebuild /v:minimal
copy /Y "%BASE%src\CadToolkit\bin\Release\CadToolkit.dll" "%DEPLOY%\acad\"
copy /Y "%BASE%src\CadToolkit.Core\bin\Release\CadToolkit.Core.dll" "%DEPLOY%\acad\"
copy /Y "%BASE%src\CadToolkit.UI\bin\Release\CadToolkit.UI.dll" "%DEPLOY%\acad\"
echo   AutoCAD: OK

echo.
echo [2/3] ZWCAD
if exist "%BASE%src\CadToolkit\bin\Release" rmdir /s /q "%BASE%src\CadToolkit\bin\Release"
%MSBUILD% "%BASE%src\CadToolkit\CadToolkit.ZWCAD.csproj" /p:Configuration=Release /p:Platform=x64 "/p:ZWCADDir=C:\Program Files\ZWSOFT\ZWCAD 2020" /t:Rebuild /v:minimal
copy /Y "%BASE%src\CadToolkit\bin\Release\CadToolkit.dll" "%DEPLOY%\zwcad\"
copy /Y "%BASE%src\CadToolkit.Core\bin\Release\CadToolkit.Core.dll" "%DEPLOY%\zwcad\"
copy /Y "%BASE%src\CadToolkit.UI\bin\Release\CadToolkit.UI.dll" "%DEPLOY%\zwcad\"
echo   ZWCAD: OK

echo.
echo [3/3] GstarCAD
if exist "%BASE%src\CadToolkit\bin\Release" rmdir /s /q "%BASE%src\CadToolkit\bin\Release"
%MSBUILD% "%BASE%src\CadToolkit\CadToolkit.GstarCAD.csproj" /p:Configuration=Release /p:Platform=x64 "/p:GstarCADDir=C:\Program Files\浩辰软件\浩辰CAD2022" /t:Rebuild /v:minimal
copy /Y "%BASE%src\CadToolkit\bin\Release\CadToolkit.dll" "%DEPLOY%\gcad\"
copy /Y "%BASE%src\CadToolkit.Core\bin\Release\CadToolkit.Core.dll" "%DEPLOY%\gcad\"
copy /Y "%BASE%src\CadToolkit.UI\bin\Release\CadToolkit.UI.dll" "%DEPLOY%\gcad\"
echo   GstarCAD: OK

echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Content '%BASE%autoload.lsp' -Raw) -replace 'CadToolkit v[0-9.]+ ready', 'CadToolkit %CT_VERSION% ready' | Set-Content '%DEPLOY%\autoload.lsp' -Encoding UTF8"
if exist "%DEPLOY%\acad\autoload.lsp" del /q "%DEPLOY%\acad\autoload.lsp"
if exist "%DEPLOY%\zwcad\autoload.lsp" del /q "%DEPLOY%\zwcad\autoload.lsp"
if exist "%DEPLOY%\gcad\autoload.lsp" del /q "%DEPLOY%\gcad\autoload.lsp"
copy /Y "%BASE%CadToolkit.ini" "%DEPLOY%\"
if exist "%DEPLOY%\acad\CadToolkit.ini" del /q "%DEPLOY%\acad\CadToolkit.ini"
if exist "%DEPLOY%\zwcad\CadToolkit.ini" del /q "%DEPLOY%\zwcad\CadToolkit.ini"
if exist "%DEPLOY%\gcad\CadToolkit.ini" del /q "%DEPLOY%\gcad\CadToolkit.ini"

echo ========================================
echo   Done! Output: %DEPLOY%
echo ========================================
pause
