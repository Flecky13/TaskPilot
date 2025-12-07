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

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Kommentare und leere Zeilen überspringen
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                    continue;

                // Sektion: [ProgramName]
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    if (currentProgram != null)
                    {
                        programs.Add(currentProgram);
                    }

                    var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    currentProgram = new MonitoredProgram
                    {
                        DisplayName = sectionName,
                        ProcessName = sectionName
                    };
                }
                // Key=Value Paare
                else if (trimmedLine.Contains("=") && currentProgram != null)
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
                    }
                }
            }

            // Letztes Programm hinzufügen
            if (currentProgram != null)
            {
                programs.Add(currentProgram);
            }

            return programs;
        }

        private static void CreateDefaultConfiguration(string filePath)
        {
            var defaultConfig = @"; TaskPilot Konfigurationsdatei
;
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

        public static void SaveConfiguration(string filePath, List<MonitoredProgram> programs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("; TaskPilot Konfigurationsdatei");
            sb.AppendLine("; Generiert am: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
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

                if (program.AutoRestart)
                {
                    sb.AppendLine("AutoRestart=true");
                }

                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
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
    }
}
