@echo off
rem Schaltet abgeschaltete USB-Geraete wieder ein. Fuer den Fall, dass Maus und Tastatur weg sind.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Enable-DisabledUsb.ps1"
