@echo off
chcp 65001 >nul
setlocal
echo ========================================
echo   CadToolkit Build
echo ========================================

set "BASE=%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%BASE%deploy-local.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%EXIT_CODE%"=="0" echo Build failed with exit code %EXIT_CODE%
pause
exit /b %EXIT_CODE%
