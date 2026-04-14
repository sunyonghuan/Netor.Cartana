@echo off
setlocal

cd /d "%~dp0"

where pwsh >nul 2>nul
if %errorlevel% equ 0 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0avaloniaui.publish.ps1"
) else (
    echo [WARN] 未找到 pwsh.exe，正在回退到 Windows PowerShell。
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0avaloniaui.publish.ps1"
)

set "EXITCODE=%ERRORLEVEL%"
pause
exit /b %EXITCODE%