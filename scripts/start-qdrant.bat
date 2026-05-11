@echo off
rem Start Qdrant database only
PowerShell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-qdrant.ps1"
pause
