# TaskPilot

Eine WPF-Systemtray-Anwendung zur Überwachung von Programmen.

## Features

- **Systemtray-Integration**: Läuft im Hintergrund und zeigt ein Icon im Systemtray
- **Programmüberwachung**: Überwacht konfigurierte Programme und zeigt deren Status (Aktiv/Inaktiv)
- **INI-Konfiguration**: Einfache Konfiguration über eine `programs.ini` Datei
- **Echtzeit-Updates**: Automatische Aktualisierung alle 5 Sekunden
- **Übersichtliche GUI**: Moderne WPF-Oberfläche mit Status-Anzeige

## Installation

1. Projekt in Visual Studio öffnen
2. NuGet-Pakete wiederherstellen
3. Projekt kompilieren (F5)

## Konfiguration

Die Datei `programs.ini` enthält die zu überwachenden Programme:

```ini
[Visual Studio Code]
ProcessName=Code
DisplayName=Visual Studio Code
Description=Code-Editor

[Google Chrome]
ProcessName=chrome
DisplayName=Google Chrome
Description=Webbrowser
```

### Felder:

- **ProcessName**: Der Prozessname ohne `.exe` Erweiterung
- **DisplayName**: Anzeigename in der Oberfläche
- **Description**: Optionale Beschreibung

## Verwendung

1. Anwendung starten
2. Fenster wird im Systemtray minimiert
3. Linksklick auf Systemtray-Icon öffnet das Hauptfenster
4. Rechtsklick zeigt Kontextmenü mit Optionen
5. "Konfiguration bearbeiten" öffnet `programs.ini` zum Bearbeiten
6. Nach Änderungen "Konfiguration neu laden" im Kontextmenü wählen

## Technische Details

- **.NET 8.0** mit Windows-Unterstützung
- **WPF** für die Benutzeroberfläche
- **Hardcodet.NotifyIcon.Wpf** für Systemtray-Funktionalität
- Automatische Prozessüberwachung mit `System.Diagnostics.Process`

## Anforderungen

- Windows 10/11
- .NET 8.0 Runtime

## Lizenz

Frei verwendbar für persönliche und kommerzielle Zwecke.
