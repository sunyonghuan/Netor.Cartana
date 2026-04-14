@echo off
chcp 65001 >nul 2>&1
title Cortana 插件打包推送
echo.
echo ══════════════════════════════════════════════
echo   Cortana 插件 NuGet 打包 ^& 推送
echo ══════════════════════════════════════════════
echo.
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0plugin.publish.ps1" %*
echo.
pause