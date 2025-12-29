using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TaskPilot
{
    /// <summary>
    /// Liest und schreibt INI-Konfigurationsdateien
    /// </summary>
    public static class IniConfigReader
    {
        public class ServerSettings
        {
            public int Port { get; set; } = 5110;
            public bool Enabled { get; set; } = true;
            public string Password { get; set; } = "admin";
        }


        public static List<MonitoredProgram> ReadConfiguration(string filePath)
        {
            var programs = new List<MonitoredProgram>();

            if (!File.Exists(filePath))
            {
                // Erstelle Standard-Konfiguration
                CreateDefaultConfiguration(filePath);
            }

            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            MonitoredProgram? currentProgram = null;
            bool skipSection = false; // true wenn wir in [Server] sind

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Kommentare und leere Zeilen überspringen
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                    continue;

                // Sektion: [ProgramName]
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    // Vorherige Sektion abschließen, falls sie kein Server war
                    if (currentProgram != null && !skipSection)
                    {
                        programs.Add(currentProgram);
                    }

                    var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);

                    // Server-Sektion überspringen
                    if (sectionName.Equals("Server", StringComparison.OrdinalIgnoreCase))
                    {
                        skipSection = true;
                        currentProgram = null;
                        continue;
                    }

                    skipSection = false;
                    currentProgram = new MonitoredProgram
                    {
                        DisplayName = sectionName,
                        ProcessName = sectionName
                    };
                }
                // Key=Value Paare
                else if (trimmedLine.Contains("=") && currentProgram != null && !skipSection)
                {
                    var parts = trimmedLine.Split(new[] { '=' }, 2);
                    var key = parts[0].Trim().ToLowerInvariant();
                    var value = parts[1].Trim();

                    switch (key)
                    {
                        case "processname":
                            currentProgram.ProcessName = value;
                            break;
                        case "displayname":
                            currentProgram.DisplayName = value;
                            break;
                        case "description":
                            currentProgram.Description = value;
                            break;
                        case "startcommand":
                            currentProgram.StartCommand = value;
                            break;
                        case "autorestart":
                            currentProgram.AutoRestart = value.ToLowerInvariant() == "true";
                            break;
                        case "isselected":
                        case "überwachen":
                            currentProgram.IsSelected = value.ToLowerInvariant() == "true";
                            break;
                    }
                }
            }

            // Letztes Programm hinzufügen
            if (currentProgram != null && !skipSection)
            {
                programs.Add(currentProgram);
            }

            return programs;
        }

        public static ServerSettings ReadServerSettings(string filePath)
        {
            var settings = new ServerSettings();

            if (!File.Exists(filePath))
            {
                return settings;
            }

            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            bool inServerSection = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    inServerSection = trimmed.Equals("[Server]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inServerSection || !trimmed.Contains("="))
                    continue;

                var parts = trimmed.Split(new[] { '=' }, 2);
                var key = parts[0].Trim().ToLowerInvariant();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "port":
                        if (int.TryParse(value, out var port) && port > 0 && port <= 65535)
                            settings.Port = port;
                        break;
                    case "enabled":
                        settings.Enabled = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "password":
                        if (!string.IsNullOrWhiteSpace(value))
                            settings.Password = value;
                        break;
                }
            }

            return settings;
        }

        private static void CreateDefaultConfiguration(string filePath)
        {
            var defaultConfig = @"; TaskPilot Konfigurationsdatei
;
; Globale Server-Einstellungen
[Server]
Port=5110
Enabled=true
Password=admin

; Programme:
; Format:
; [Anzeigename]
; ProcessName=prozessname (ohne .exe)
; DisplayName=Anzeigename in der Oberfläche
; Description=Optionale Beschreibung
; StartCommand=Befehl um Prozess zu starten (optional)
; AutoRestart=true/false (Optional - Prozess automatisch neu starten wenn nicht laufend)
;
; Beispiele:

[Visual Studio Code]
ProcessName=Code
DisplayName=Visual Studio Code
Description=Code-Editor

[Google Chrome]
ProcessName=chrome
DisplayName=Google Chrome
Description=Webbrowser

[Microsoft Edge]
ProcessName=msedge
DisplayName=Microsoft Edge
Description=Webbrowser

[Notepad]
ProcessName=notepad
DisplayName=Editor
Description=Windows Notepad

[Calculator]
ProcessName=CalculatorApp
DisplayName=Rechner
Description=Windows Rechner
";

            File.WriteAllText(filePath, defaultConfig, Encoding.UTF8);
        }

        public static void SaveConfiguration(string filePath, List<MonitoredProgram> programs, ServerSettings? serverSettings = null, bool appendMode = false)
        {
            if (appendMode && File.Exists(filePath))
            {
                // Anhängmodus: Füge nur neue Programme an das Ende der Datei an
                var sb = new StringBuilder();
                sb.AppendLine();
                foreach (var program in programs)
                {
                    sb.AppendLine($"[{program.DisplayName}]");
                    sb.AppendLine($"ProcessName={program.ProcessName}");
                    sb.AppendLine($"DisplayName={program.DisplayName}");

                    if (!string.IsNullOrWhiteSpace(program.Description))
                    {
                        sb.AppendLine($"Description={program.Description}");
                    }

                    if (!string.IsNullOrWhiteSpace(program.StartCommand))
                    {
                        sb.AppendLine($"StartCommand={program.StartCommand}");
                    }

                    // Schreibe beide Checkbox-Werte immer, damit sie nicht auf Default zurückfallen
                    sb.AppendLine($"AutoRestart={(program.AutoRestart ? "true" : "false")}");
                    sb.AppendLine($"IsSelected={(program.IsSelected ? "true" : "false")}");

                    sb.AppendLine();
                }

                File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            else
            {
                // Normalmodus: Überschreibe die gesamte Datei
                var settingsToWrite = serverSettings ?? ReadServerSettings(filePath);
                var sb = new StringBuilder();
                sb.AppendLine("; TaskPilot Konfigurationsdatei");
                sb.AppendLine("; Generiert am: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine();

                // Globale Server-Einstellungen (inkl. Passwort)
                sb.AppendLine("[Server]");
                sb.AppendLine($"Port={settingsToWrite.Port}");
                sb.AppendLine($"Enabled={(settingsToWrite.Enabled ? "true" : "false")}");
                sb.AppendLine($"Password={settingsToWrite.Password}");
                sb.AppendLine();

                foreach (var program in programs)
                {
                    sb.AppendLine($"[{program.DisplayName}]");
                    sb.AppendLine($"ProcessName={program.ProcessName}");
                    sb.AppendLine($"DisplayName={program.DisplayName}");

                    if (!string.IsNullOrWhiteSpace(program.Description))
                    {
                        sb.AppendLine($"Description={program.Description}");
                    }

                    if (!string.IsNullOrWhiteSpace(program.StartCommand))
                    {
                        sb.AppendLine($"StartCommand={program.StartCommand}");
                    }

                    // Schreibe beide Checkbox-Werte immer, damit sie nicht auf Default zurückfallen
                    sb.AppendLine($"AutoRestart={(program.AutoRestart ? "true" : "false")}");
                    sb.AppendLine($"IsSelected={(program.IsSelected ? "true" : "false")}");

                    sb.AppendLine();
                }

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
        }
    }

    /// <summary>
    /// Repräsentiert ein zu überwachendes Programm
    /// </summary>
    public class MonitoredProgram
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string StartCommand { get; set; } = string.Empty;
        public bool AutoRestart { get; set; } = false;
        public int LastStartedPID { get; set; } = 0; // Speichert die PID des zuletzt gestarteten Prozesses
        public bool IsSelected { get; set; } = true; // Bestimmt, ob der Prozess im MainWindow angezeigt wird
    }
}
