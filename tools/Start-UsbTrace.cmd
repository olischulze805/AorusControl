@echo off
rem Schneidet den USB-Verkehr mit, bis die Tastatur abreisst. Nur lesend.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-UsbTrace.ps1"
