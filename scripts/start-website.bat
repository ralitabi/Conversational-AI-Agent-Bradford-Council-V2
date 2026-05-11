@echo off
rem Serve frontend on localhost:8080
PowerShell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-website.ps1"
pause
