@echo off
chcp 65001 >nul
echo ========================================
echo   CadToolkit Build
echo ========================================

set "MSBUILD=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
set "BASE=%~dp0"
set "DEPLOY=C:\CadToolkit"

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
copy /Y "%BASE%autoload.lsp" "%DEPLOY%\"
copy /Y "%BASE%autoload.lsp" "%DEPLOY%\acad\"
copy /Y "%BASE%autoload.lsp" "%DEPLOY%\zwcad\"
copy /Y "%BASE%autoload.lsp" "%DEPLOY%\gcad\"
copy /Y "%BASE%CadToolkit.ini" "%DEPLOY%\"
copy /Y "%BASE%CadToolkit.ini" "%DEPLOY%\acad\"
copy /Y "%BASE%CadToolkit.ini" "%DEPLOY%\zwcad\"
copy /Y "%BASE%CadToolkit.ini" "%DEPLOY%\gcad\"

echo ========================================
echo   Done! Output: %DEPLOY%
echo ========================================
pause