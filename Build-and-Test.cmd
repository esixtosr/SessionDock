@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Build-Windows.ps1"
if errorlevel 1 (echo Build or test failed. See the error above.) else (echo Build completed. Read docs\WINDOWS-ACCEPTANCE.md for the interactive tests.)
pause
