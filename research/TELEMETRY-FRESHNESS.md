# Frische Messwerte für Lüfterschutz und Profilauswahl

Stand 2026-09-03.

## Behobene Schwachstellen

- Die vorhandene UI-Fixed-Steuerung prüfte bisher nur Temperaturen ab 65 °C, nicht Alter oder Nullwerte. Sie nutzt jetzt dieselbe `FixedFanSafety`-Prüfung wie der unabhängige Supervisor: 1–64 °C, höchstens 5 Sekunden alt, maximal 1 Sekunde in der Zukunft. Vor Fixed wird abgewiesen; während Fixed führt eine Verletzung über den bestehenden Fehlerpfad zur Normal-Rückstellung mit Wiederholungsmöglichkeit.
- Der WMI-Reader datierte die Messung erst nach allen sechs Lesemethoden. Ein langsamer späterer RPM-/Duty-Aufruf ließ zuvor gelesene Temperaturen dadurch zu frisch erscheinen. Zeitstempel wird jetzt unmittelbar vor dem ersten Temperatur-Getter genommen. UI zeigt weiterhin lokale Uhrzeit. Eine hängende native Operation wird dadurch nicht unterbrochen; externer Wächter bleibt erforderlich.
- Profilauswahl verwirft eine Kandidatur nach mehr als 5 Sekunden Beobachtungspause selbstständig. Danach beginnt die 2-Sekunden-Stabilitätsphase neu. Explizites Reset bei Resume bleibt sinnvoll. Die Auswahl benötigt fortlaufende Beobachtungen, nicht nur ein einzelnes Netzwechsel-Ereignis.

## Verifikation

Build und gesamte simulierte Suite erfolgreich. Zusätzliche Tests: UI verweigert Fixed bei Nulltemperatur und altem kühlem Sample; ein altes Sample während Fixed löst Normal aus; Profilauswahl verlangt nach langer Beobachtungspause neue Stabilität. Keine Hardwareänderungen, kein echter WMI-Hang provoziert. Codeprüfung des vorgezogenen Erfassungszeitpunkts ist kein physischer Timingtest.
