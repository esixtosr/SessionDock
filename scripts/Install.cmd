@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Local.ps1"
if errorlevel 1 (echo Installation failed. See the error above.) else (echo SessionDock is installed for your Windows account.)
pause
