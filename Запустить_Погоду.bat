@echo off
chcp 65001 >nul
cd /d "%~dp0"
start "" "%~dp0bin\Release\net9.0-windows\DesktopWeather.exe"
exit
