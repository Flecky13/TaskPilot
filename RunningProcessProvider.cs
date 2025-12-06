using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TaskPilot
{
    /// <summary>
    /// Liefert eine Liste aller derzeit laufenden Prozesse
    /// </summary>
    public static class RunningProcessProvider
    {
        public static List<ProcessInfo> GetRunningProcesses()
        {
            var processes = new List<ProcessInfo>();

            try
            {
                var runningProcesses = Process.GetProcesses();

                foreach (var process in runningProcesses)
                {
                    try
                    {
                        var processInfo = new ProcessInfo
                        {
                            ProcessName = process.ProcessName.ToLowerInvariant(),
                            DisplayName = GetProcessDisplayName(process),
                            IsRunning = true
                        };

                        // Duplikate vermeiden
                        if (!processes.Any(p => p.ProcessName == processInfo.ProcessName))
                        {
                            processes.Add(processInfo);
                        }
                    }
                    catch
                    {
                        // Ignoriere Prozesse, auf die wir nicht zugreifen können
                    }
                }
            }
            catch
            {
                // Fehler beim Abrufen der Prozesse
            }

            return processes.OrderBy(p => p.DisplayName).ToList();
        }

        private static string GetProcessDisplayName(Process process)
        {
            try
            {
                // Versuche den Dateinamen zu bekommen
                var mainModuleFileName = process.MainModule?.FileName;
                if (!string.IsNullOrEmpty(mainModuleFileName))
                {
                    return System.IO.Path.GetFileNameWithoutExtension(mainModuleFileName);
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Zugriff auf MainModule ist nicht erlaubt für einige Systemprozesse
                // Ignoriere diesen Fehler
            }
            catch (UnauthorizedAccessException)
            {
                // Keine Berechtigung für diesen Prozess
            }
            catch (Exception)
            {
                // Andere Fehler beim Abrufen des Modulnamens
            }

            return process.ProcessName;
        }
    }

    /// <summary>
    /// Repräsentiert einen Prozess
    /// </summary>
    public class ProcessInfo
    {
        public string ProcessName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
    }
}
