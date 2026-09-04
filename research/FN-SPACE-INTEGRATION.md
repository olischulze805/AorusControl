# Fn+Space: Ereignismodul

## Nachtrag: Wiederverbindung und abgebrochenes Beenden

Die App verwendet jetzt `KeyboardNotificationReconnect`: Nach Ende/Fehler des bisherigen Lesers erfolgt ein neuer Versuch mit Pausen 1/2/4/8/16/30 Sekunden, danach maximal alle 30 Sekunden. Erst nach vollständig beendeter Quell-Task wird wieder geöffnet. Ein empfangenes Helligkeitsereignis setzt die Fehlerfolge zurück. Cancellation beendet auch die Wartephase. Fehler und nächste Wartezeit werden angezeigt, ohne eine erfolgreiche Verbindung vorzutäuschen.

Scheitert das explizite App-Beenden an einer späteren Hardware-Rückstellung, startet der bereits gestoppte Leser mit neuem Cancellation-Zeitraum wieder. Noch in der UI-Warteschlange liegende Ereignisse/Retrymeldungen des alten Zeitraums werden anhand seines Tokens verworfen.

Simulierte Tests bestätigen Backoff-Obergrenze, Rücksetzen nach Ereignis, Wiederholung nach unerwartetem normalen Quellende und Abbruch während der Pause ohne erneutes Öffnen. Kein physischer Geräteverlust-/Standbytest. Ein HID-Handle, das nach Resume offen bleibt, aber keine Ereignisse mehr liefert, wird dadurch nicht erkannt; vollständige Suspend-/Resume-Anbindung und RGB-Zustandsabgleich bleiben offen. Frühere Hinweise auf dauerhaft nötigen Neustart nach Listenerfehler sind überholt.

## Nachtrag: erster Live-Test des neuen Lesers

Der explizit lesende Test `tests/AorusControl.HardwareChecks --brightness-read-only` öffnete den tatsächlichen freigegebenen Kanal und beendete ihn nach Cancellation ohne Fehler. Lauf vom 2026-09-03 18:23:58 -03:00: 8,270 Sekunden, 109,4 ms Prozess-CPU-Zeit einschließlich Initialisierung, null empfangene Ereignisse. Bericht: `research/runs/brightness-listener-live-20260903-182406-ba190ae4818e4db1bd3ddf22e7bbd1c0.md`. Das bestätigt Auswahl/Öffnen/Warten/Schließen, nicht die Reaktion auf einen tatsächlich gedrückten Fn+Space-Hotkey und nicht den langfristigen Ressourcenverbrauch. Keine Hardwarewerte geschrieben.

App ergänzt: identische Helligkeitsmeldungen zum bereits aktuellen UI-Zustand werden ohne Session-Write oder Dateispeicherung quittiert. Simulierter Test sendet 20 gleiche Meldungen und bestätigt keine zusätzlichen Transportwrites oder Effektstarts. Eigene Hardwarewrites als mögliche Ereignisquelle sind weiterhin physisch zu untersuchen.

Erhöhter WMI-Test Session 56847 nochmals geprüft: weiterhin laufender Aufruf ohne Ausgabe; kein neuer Start ausgelöst.

## Nachtrag: App-Anbindung

Die Produktions-App startet den Listener nach erfolgreicher RGB-Initialisierung. Callback stellt ausschließlich die Verarbeitung auf den UI-Dispatcher zu. Während einer laufenden RGB-Operation bleibt die neueste Helligkeit vorgemerkt; Zwischenwerte werden zusammengefasst statt verworfen oder unbegrenzt aufgestaut. Nur bei ausstehendem Ereignis wird kurz auf die RGB-Operation gewartet, kein zusätzlicher Leerlauftimer.

Die Übernahme läuft durch die gemeinsame RGB-Session und denselben Speicherpfad wie UI-Änderungen. Off behält die Auswahl; erneutes Einschalten per Ereignis setzt den ausgewählten Effekt fort. Eigenes Statusfeld unterscheidet Startversuch, Ereignisverarbeitung und Listenerfehler. Beim expliziten Beenden wird der Listener abgebrochen und abgewartet, anschließend die ausstehende Verarbeitung. Tray-Verbergen beendet ihn nicht.

Tests mit simuliertem Transport bestätigen: mehrere Ereignisse bei beschäftigter UI enden mit der jüngsten Stufe, Off-Rückstellung verwendet Off, ein folgendes Low-Ereignis setzt den Effekt fort, und Beenden wartet auf den injizierten Listener. Gesamtlösung und Tests erfolgreich. Noch kein physischer Durchlauf des neuen Listeners, keine echte Fn+Space-Prüfung während Effekten und keine Prüfung, ob eigene Hardwarewrites ebenfalls Meldungen erzeugen. Deskriptorauswahl, Latenz und eventuelle Rückkopplung müssen live geprüft werden.

Keine automatische Neuverbindung nach Geräteverlust/Resume. Falls Beenden an einer anderen Hardware-Rückstellung scheitert, bleibt der Ereignisleser beendet; Status verlangt einen App-Neustart. Dies ist eine sichtbare Zwischenstand-Grenze, keine vollständige Lebenszykluslösung. Frühere Abschnitte, die von noch fehlender App-Anbindung sprechen, sind damit historisch.

## Nachtrag: laufende Effekt-Helligkeit

Der echte HID-Renderer implementiert jetzt `ILiveEffectBrightness`. Bei ausschließlich geänderter Helligkeit übernimmt die RGB-Session den neuen Wert ohne Renderer-Neustart. Der Renderloop liest den Wert vor dem nächsten Frame; die spätere Rückstellung benutzt ebenfalls diesen Wert. Bei Aus wird vor dem Stoppen Off als Rückstellhelligkeit gesetzt. Farben/Tempo/Moduswechsel nutzen weiterhin den bisherigen vollständigen Übergang.

Dies wirkt bereits bei Helligkeitsänderungen über die App. Simulierter Session-Test bestätigt unveränderte Worker-Startzahl und Off-Rückstellung statt alter Helligkeit; vorhandene Tests und Build erfolgreich. Kein physischer Effekt-/Fn+Space-Test. Die Übernahme ist eine Renderanforderung, kein synchroner Hardware-Readback; spätere Rendererfehler laufen über den vorhandenen Effektfehlerkanal. Ein bereits begonnener Frame kann noch den bisherigen Wert enthalten. Der Ereignislistener ist noch nicht an die App angeschlossen; geordnete Ereignisübernahme und Lebenszyklus bleiben offen.

Stand 2026-09-03. Grundlage: dokumentierter Lauf `keyboard-brightness-events-20260903-123822.md` und KEYBOARD-BRIGHTNESS.md, Abschnitt mit vollständig beobachteten Ereignissen 04 01 00/18/20/32 00.

## Implementiert

`KeyboardBrightnessNotifications` akzeptiert ausschließlich die vier belegten 4-Byte-Reports, keine vermuteten Nachbarwerte und keine anderen Ereignistypen. Es sucht nur VID 1044/PID 7A41, MI_02/COL_04, Inputlänge 4 und keine Output-/Feature-Reports. Deskriptoren mit Tastatur-Usages und Keyboard-Gerätepfade werden abgewiesen. Bei mehreren oder keinem passenden Gerät wird nicht geöffnet. Keine Erfassung normaler Tastendrücke, kein globaler Tastaturhook, keine Hardwarewrites.

Der Listener wartet auf Input, mit einer Sekunde Read-Timeout für Beenden. Geräteverlust/Lesefehler werden als Task-Fehler weitergereicht; keine automatische Rekonnektion. Callback läuft auf dem Listener-Thread und muss kurz bleiben. Dieses Modul wird von der App noch nicht automatisch gestartet.

## Prüfung / nächste Integration

Parser-Tests für alle vier gültigen Reports und falsche Länge/Report-ID/Ereignistyp/Endbyte/unbekannte Stufe bestanden; Gesamtbuild und gesamte simulierte Suite ebenfalls. Der neue Listener selbst wurde noch nicht live am Gerät geöffnet. Strenge Deskriptor-Gates und sauberes Stoppen müssen live bestätigt werden.

Noch erforderlich: Listener-Lebenszyklus, neueste Ereignisse geordnet in die RGB-Session übernehmen, UI/Persistenz aktualisieren, Renderer ohne Rückschreiben alter Helligkeit reagieren lassen und Fn+Space während Effekten physisch prüfen. Der vorhandene Renderer restauriert beim Stoppen seinen Ausgangssnapshot; bloßes Anschließen an einen UI-Setter würde daher noch einen sichtbaren alten Helligkeitszustand zurückschreiben. Keine Behauptung einer fertigen Fn+Space-Synchronisierung.
