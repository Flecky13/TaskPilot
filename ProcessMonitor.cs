using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;

namespace TaskPilot
{
    /// <summary>
    /// Überwacht den Status von konfigurierten Programmen
    /// </summary>
    public class ProcessMonitor
    {
        private List<MonitoredProgram> _monitoredPrograms = new List<MonitoredProgram>();
        private Dictionary<string, ProgramStatus> _statusCache = new Dictionary<string, ProgramStatus>();

        public event EventHandler<ProgramStatus>? StatusChanged;

        public void SetMonitoredPrograms(List<MonitoredProgram> programs)
        {
            _monitoredPrograms = programs;
            _statusCache.Clear();

            // Initialisiere Status-Cache mit aktuellen Prozess-Status
            var runningProcesses = Process.GetProcesses()
                .Select(p => p.ProcessName.ToLowerInvariant())
                .ToHashSet();

            foreach (var program in programs)
            {
                var processNameLower = program.ProcessName.ToLowerInvariant();
                var isRunning = runningProcesses.Contains(processNameLower);

                _statusCache[program.ProcessName] = new ProgramStatus
                {
                    DisplayName = program.DisplayName,
                    ProcessName = program.ProcessName,
                    Description = program.Description,
                    IsActive = isRunning,
                    StatusSince = DateTime.Now
                };

                // Debug: Initial Status
                Debug.WriteLine($"[ProcessMonitor] Program initialisiert: {program.DisplayName} - IsActive: {isRunning}");
            }
        }

        public void UpdateStatuses()
        {
            // Erstelle ein Lookup statt Dictionary, um mehrere Prozesse mit demselben Namen zu unterstützen
            var runningProcesses = Process.GetProcesses()
                .ToLookup(p => p.ProcessName.ToLowerInvariant(), p => p.Id);

            foreach (var program in _monitoredPrograms)
            {
                var processNameLower = program.ProcessName.ToLowerInvariant();
                var processIds = runningProcesses[processNameLower].ToList();
                var isRunning = processIds.Count > 0;
                var processId = processIds.FirstOrDefault();

                if (_statusCache.TryGetValue(program.ProcessName, out var status))
                {
                    Debug.WriteLine($"[ProcessMonitor.UpdateStatuses] {program.ProcessName}: isRunning={isRunning}, wasActive={status.IsActive}");

                    // Update ProcessID (erste aktive PID, oder 0 wenn nicht laufend)
                    status.ProcessID = isRunning ? processId : 0;

                    if (status.IsActive != isRunning)
                    {
                        // Status hat sich geändert
                        Debug.WriteLine($"[ProcessMonitor.UpdateStatuses] STATUS CHANGED: {program.ProcessName} -> {isRunning}");
                        status.IsActive = isRunning;
                        status.StatusSince = DateTime.Now;

                        StatusChanged?.Invoke(this, status);
                    }
                }
                else
                {
                    Debug.WriteLine($"[ProcessMonitor.UpdateStatuses] WARNING: {program.ProcessName} nicht im Cache!");
                }
            }
        }

        public IEnumerable<ProgramStatus> GetStatuses()
        {
            return _statusCache.Values.OrderBy(s => s.DisplayName);
        }
    }

    /// <summary>
    /// Repräsentiert den aktuellen Status eines Programms
    /// </summary>
    public class ProgramStatus : INotifyPropertyChanged
    {
        private bool _isActive;
        private DateTime _statusSince;
        private int _processID;

        public string DisplayName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ProcessID
        {
            get => _processID;
            set
            {
                if (_processID != value)
                {
                    _processID = value;
                    OnPropertyChanged(nameof(ProcessID));
                }
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged(nameof(IsActive));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public DateTime StatusSince
        {
            get => _statusSince;
            set
            {
                _statusSince = value;
                OnPropertyChanged(nameof(StatusSince));
                OnPropertyChanged(nameof(StatusSinceFormatted));
            }
        }

        public string StatusText => IsActive ? "Aktiv" : "Inaktiv";

        public Brush StatusColor => IsActive
            ? new SolidColorBrush(Color.FromRgb(76, 175, 80))  // Grün
            : new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Rot

        public string StatusSinceFormatted
        {
            get
            {
                var duration = DateTime.Now - StatusSince;

                if (duration.TotalMinutes < 1)
                    return "Gerade eben";
                else if (duration.TotalMinutes < 60)
                    return $"{(int)duration.TotalMinutes} Min.";
                else if (duration.TotalHours < 24)
                    return $"{(int)duration.TotalHours} Std.";
                else
                    return $"{(int)duration.TotalDays} Tage";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
