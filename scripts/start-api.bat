@echo off
rem Restart API only
PowerShell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-api.ps1"
pause
