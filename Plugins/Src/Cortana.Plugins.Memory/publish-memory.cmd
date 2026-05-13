@echo off
chcp 65001 >nul
where pwsh >nul 2>nul
if %ERRORLEVEL%==0 (
	pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-memory.ps1" %*
) else (
	powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-memory.ps1" %*
)
