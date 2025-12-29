# TaskPilot
![Alt text](Images/icon.png)

Eine Systemtray-Anwendung zur Überwachung von Programmen.
Die Programme können über StartCommand Automatisch gestartet werden
Anwendung kann die überwachen Programme darstellen und mehr

## Features

- **Systemtray-Integration**: Läuft im Hintergrund und zeigt ein Icon im Systemtray
- **Programmüberwachung**: Überwacht konfigurierte Programme und zeigt deren Status (Aktiv/Inaktiv)
- **Context-Menü**: Rechtsklick auf Prozess für schnelle Kontrolle (Start/Stop/Minimieren/Maximieren)
- **Auto-Restart**: Automatisches Neustarten von abgestürzten Programmen basierend auf Konfiguration
- **INI-Konfiguration**: Einfache Konfiguration über eine `programs.ini` Datei
- **Konfigurations GUI**: Einfache Konfiguration über Konfig GUI mit Automatischer Anpassung der 'programs.ini' Datei
- **Echtzeit-Updates**: Automatische Aktualisierung alle 5 Sekunden
- **Übersichtliche GUI**: GUI-Oberfläche mit Status-Anzeige und Prozess-Informationen

## Funktionsübersicht

### Hauptfenster
- **Prozessliste**: Zeigt alle überwachten Programme mit Status (aktiv/inaktiv), PID und Startzeit
- **Context-Menü**: Rechtsklick auf Status-Spalte ermöglicht:
  - Prozess stoppen
  - Prozess starten
  - Prozess minimieren
  - Prozess in den Vordergrund bringen
- **Alle minimieren/maximieren**: Schnelle Kontrolle für alle Prozesse
- **AutoStart-Checkbox**: Global AutoRestart aktivieren/deaktivieren
- **Letzte Aktualisierung**: Zeigt Datum und Uhrzeit der letzten Status-Prüfung (DD.MM.YYYY HH:MM:SS)

### Konfigurationsfenster
- **Prozess-Filter**: Schnelle Suche mit Wildcard-Unterstützung (* und ?)
- **Schnell-Buttons**:
  - **Neuer Prozess**: Manuell einen Prozess hinzufügen
  - **Alle Überwachen**: Alle Prozesse zur Überwachung aktivieren
  - **Überwachen aus**: Alle Prozesse deaktivieren
  - **Alle AutoStart**: AutoRestart für alle Prozesse aktivieren (erfordert Startbefehl)
  - **AutoStart aus**: AutoRestart für alle Prozesse deaktivieren
  - **Hilfe**: Öffnet Hilfefenster
- **Prozess-Bearbeitung**: Inline-Bearbeitung aller Felder in der Tabelle
- **Bearbeit/Löschen-Buttons**: Symbol-Buttons (✎ Bearbeiten, 🗑 Löschen) für kompakte Darstellung
- **Übernehmen**: Speichert alle Änderungen in der INI-Datei

## Konfiguration

Die Datei `programs.ini` wird automatisch im Benutzer-AppData-Verzeichnis gespeichert:
- **Windows**: `%APPDATA%\TaskPilot\programs.ini`
- **Beispiel**: `C:\Users\[Benutzername]\AppData\Roaming\TaskPilot\programs.ini`

Das Verzeichnis wird beim ersten Start automatisch erstellt.

Die `programs.ini` enthält die zu überwachenden Programme:

```ini
[Program]
ProcessName=Programname
DisplayName=Program Display Name
Description=Program Beschreibung
StartCommand=c:\Programpfad\Programm.exe
AutoRestart=true
IsSelected=true
```

### Felder:

- **ProcessName**: Der Prozessname ohne `.exe` Erweiterung
- **DisplayName**: Anzeigename in der Oberfläche
- **Description**: Optionale Beschreibung
- **StartCommand**: Befehl zum erneuten starten des Prozesses mit Optionen
- **AutoRestart**: Automatisches Neustarten bei Absturz (true/false)
- **IsSelected**: Prozess wird überwacht (true) oder ignoriert (false)

## Webserver & REST-API
- Aktivierung: Im Konfigurationsfenster unter "Webserver" den Schalter "Server aktiv" setzen und Port festlegen.
- HTTPS: Im gleichen Dialog "HTTPS" aktivieren und ein Zertifikat aus dem Windows-Zertifikatsspeicher auswählen. Erforderlich ist ein Zertifikat mit **privatem Schlüssel** im Store **CurrentUser\\My** (exportierbar). Liegt kein nutzbarer Schlüssel vor, fällt die App automatisch auf HTTP zurück.
- Passwortschutz
- Änderungen übernehmen: Nach dem Speichern der Server-Einstellungen in der Konfiguration wird der Webserver automatisch mit den neuen Werten neu gestartet.


## Installation & Berechtigungen

TaskPilot kann sicher in `Program Files` installiert werden:
- ✅ Die Konfigurationsdatei wird im `%APPDATA%` Verzeichnis gespeichert
- ✅ Schreibzugriff ist nicht für das Installationsverzeichnis erforderlich
- ✅ Mehrere Benutzer können TaskPilot auf demselben System installieren
- ✅ Jeder Benutzer hat seine eigene `programs.ini` Konfiguration

### Lizenz & Kontakt
----------------
Siehe `LICENSE` im Repository. Für Fragen zum Code bitte Issues/PRs im Repo verwenden.

https://buymeacoffee.com/pedrotepe
