# TaskPilot - Konfigurationsoptimierung

## Übersicht der Änderungen

Das Konfigurationssystem wurde vollständig überarbeitet und optimiert:

### Vorher
- INI-Datei musste manuell mit einem Editor geöffnet werden

### Nachher
- **Neues Konfigurationsfenster** mit benutzerfreundlicher Oberfläche
- **Automatische Erkennung** aller laufenden Prozesse
- **Filterung** der Prozessliste nach Programmname oder Prozessname
- **Checkboxen** zur Auswahl der zu überwachenden Programme
- **Automatisches Speichern** der Konfiguration in der INI-Datei
- **Status-Anzeige** - bereits überwachte Prozesse werden automatisch angekreuzt

## Neue Komponenten

### 1. **RunningProcessProvider.cs**
- Stellt alle derzeit laufenden Prozesse bereit
- Filtert Duplikate aus
- Versucht, aussagekräftige Anzeigenamen zu ermitteln

### 2. **ConfigurationWindowViewModel.cs**
- Konfigurationslogik
- Verwaltet die Prozessliste
- Implementiert die Filterung
- Generiert die neue Konfiguration basierend auf Benutzerauswahl

### 3. **ConfigurationWindow.xaml & ConfigurationWindow.xaml.cs**
- Moderne WPF-Benutzeroberfläche
- DataGrid mit Checkboxen für die Prozessauswahl
- Live-Filterung während der Eingabe
- Buttons zum Speichern oder Abbrechen
- "Aktualisieren"-Button zum Neuladen der Prozessliste

## Verwendung

### Konfiguration öffnen
1. Klicken Sie auf **"Konfiguration bearbeiten"** in der Hauptanwendung
2. Oder wählen Sie **"Konfiguration neu laden"** im Systemtray-Menü

### Prozess hinzufügen
1. Das Konfigurationsfenster öffnet und zeigt alle laufenden Prozesse
2. Prozesse, die bereits überwacht werden, sind automatisch angekreuzt
3. Aktivieren Sie die **Checkbox** neben dem Prozess, den Sie überwachen möchten
4. (Optional) Bearbeiten Sie den **Anzeigenamen** oder die **Beschreibung**

### Filterung
1. Geben Sie in das **Filterfeld** einen Namen ein
2. Die Liste wird automatisch gefiltert (nach Programmname oder Prozessname)
3. Löschen Sie den Filter, um alle Prozesse anzuzeigen

### Konfiguration speichern
1. Klicken Sie auf **"Übernehmen"**
2. Die INI-Datei wird automatisch geschrieben
3. Die Hauptanwendung lädt die neue Konfiguration
4. Die Überwachung startet sofort

---

## Änderungshistorie

### Version 2.0 - Code-Optimierung & Auto-Restart Verbesserungen

 - Code-Refactoring
 - Auto-Restart Feature
 - Prozessscan
 - Automatische .ini erzeugung
 - Filterfunktionen in Konfigurationsmenüs



### Version 1.0 - Initiales Release
- Grundlegende Prozessüberwachung
- INI-basierte Konfiguration
- System-Tray-Integration
- Statusanzeige
