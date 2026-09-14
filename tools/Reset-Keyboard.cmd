@echo off
rem Holt die interne Tastatur zurueck, ohne den Laptop hart auszuschalten.
rem Fordert selbst Administratorrechte an. Dieses Fenster schliesst sich dann;
rem weiter geht es im neuen Fenster, das sich ebenfalls von selbst schliesst.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Reset-Keyboard.ps1"
