@echo off
rem Holt die interne Tastatur zurueck, ohne den Laptop hart auszuschalten.
rem Fordert selbst Administratorrechte an; die Schritte gehen von harmlos nach einschneidend.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Reset-Keyboard.ps1"
rem Kein pause: wer das hier braucht, hat womoeglich keine funktionierende Tastatur.
timeout /t 120 /nobreak > nul
