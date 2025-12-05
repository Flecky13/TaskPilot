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

            // Initialisiere Status-Cache
            foreach (var program in programs)
            {
                _statusCache[program.ProcessName] = new ProgramStatus
                {
                    DisplayName = program.DisplayName,
                    ProcessName = program.ProcessName,
                    Description = program.Description,
                    IsActive = false,
                    StatusSince = DateTime.Now
                };
            }
        }

        public void UpdateStatuses()
        {
            var runningProcesses = Process.GetProcesses()
                .Select(p => p.ProcessName.ToLowerInvariant())
                .ToHashSet();

            foreach (var program in _monitoredPrograms)
            {
                var processNameLower = program.ProcessName.ToLowerInvariant();
                var isRunning = runningProcesses.Contains(processNameLower);

                if (_statusCache.TryGetValue(program.ProcessName, out var status))
                {
                    if (status.IsActive != isRunning)
                    {
                        // Status hat sich geändert
                        status.IsActive = isRunning;
                        status.StatusSince = DateTime.Now;

                        StatusChanged?.Invoke(this, status);
                    }
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

        public string DisplayName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

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
