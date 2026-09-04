@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-WorkerCrashSafety.ps1"
pause
