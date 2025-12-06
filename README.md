# TaskPilot
![Alt text](Images/APP.png)
Eine Systemtray-Anwendung zur Überwachung von Programmen.
Die Programme können über StartCommand Automatisch gestartet werden

## Features

- **Systemtray-Integration**: Läuft im Hintergrund und zeigt ein Icon im Systemtray
- **Programmüberwachung**: Überwacht konfigurierte Programme und zeigt deren Status (Aktiv/Inaktiv)
- **INI-Konfiguration**: Einfache Konfiguration über eine `programs.ini` Datei
- **Konfigurations GUI**: Einfache Konfiguration über Konfig GUI mit Automatischer Anpassung der 'programs.ini' Datei
- **Echtzeit-Updates**: Automatische Aktualisierung alle 5 Sekunden
- **Übersichtliche GUI**: GUI-Oberfläche mit Status-Anzeige

## Konfiguration

Die Datei `programs.ini` enthält die zu überwachenden Programme:

```ini
[Program]
ProcessName=Programname
DisplayName=Program Display Name
Description=Program Beschreibung
StartCommand=c:\Programpfad\Programm.exe
AutoRestart=true
```

### Felder:

- **ProcessName**: Der Prozessname ohne `.exe` Erweiterung
- **DisplayName**: Anzeigename in der Oberfläche
- **Description**: Optionale Beschreibung
- **StartCommand**: Befehl zum erneuten starten des Prozesses mit Optionen
- **AutoRestart**: Program Autstart ?


### Lizenz & Kontakt
----------------
Siehe `LICENSE` im Repository. Für Fragen zum Code bitte Issues/PRs im Repo verwenden.

https://buymeacoffee.com/pedrotepe
