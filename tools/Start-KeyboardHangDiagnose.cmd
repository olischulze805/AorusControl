@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-KeyboardHangDiagnose.ps1"
rem Kein pause: wer das hier braucht, hat womoeglich keine funktionierende Tastatur.
timeout /t 90 /nobreak > nul
