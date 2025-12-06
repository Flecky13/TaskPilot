using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace TaskPilot
{
    /// <summary>
    /// ViewModel für das Konfigurationsfenster
    /// </summary>
    public class ConfigurationWindowViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<ConfigurableProcess> _availableProcesses;
        private List<ConfigurableProcess> _allProcesses;
        private string _filterText = string.Empty;
        private List<MonitoredProgram> _originalMonitoredPrograms;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ConfigurableProcess> AvailableProcesses
        {
            get => _availableProcesses;
            set
            {
                if (_availableProcesses != value)
                {
                    _availableProcesses = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText != value)
                {
                    _filterText = value;
                    OnPropertyChanged();
                    ApplyFilter();
                }
            }
        }

        public ConfigurationWindowViewModel(List<MonitoredProgram> monitoredPrograms)
        {
            _availableProcesses = new ObservableCollection<ConfigurableProcess>();
            _allProcesses = new List<ConfigurableProcess>();
            _originalMonitoredPrograms = new List<MonitoredProgram>(monitoredPrograms);

            LoadAvailableProcesses();
        }

        private void LoadAvailableProcesses()
        {
            // Lade nur Programme aus der INI-Datei (nicht laufende Prozesse)
            _allProcesses.Clear();

            foreach (var program in _originalMonitoredPrograms)
            {
                _allProcesses.Add(new ConfigurableProcess
                {
                    ProcessName = program.ProcessName,
                    DisplayName = program.DisplayName,
                    IsSelected = true,  // In der INI konfigurierte Programme sind standardmäßig "überwacht"
                    Description = program.Description,
                    AutoRestart = program.AutoRestart,
                    StartCommand = program.StartCommand
                });
            }

            // Sortiere nach DisplayName
            _allProcesses = _allProcesses.OrderBy(p => p.DisplayName).ToList();

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            List<ConfigurableProcess> source;

            if (string.IsNullOrWhiteSpace(_filterText))
            {
                source = _allProcesses;
            }
            else
            {
                source = FilterProcesses(_filterText);
            }

            // Sortiere nach DisplayName
            source = source.OrderBy(p => p.DisplayName).ToList();

            _availableProcesses.Clear();
            foreach (var process in source)
            {
                _availableProcesses.Add(process);
            }
        }

        private List<ConfigurableProcess> FilterProcesses(string filterText)
        {
            // Prüfe ob es ein Wildcard-Pattern ist
            if (filterText.Contains("*") || filterText.Contains("?"))
            {
                return FilterWithWildcards(filterText);
            }
            else
            {
                // Normale Teilstring-Suche (Standard)
                var filterLower = filterText.ToLowerInvariant();
                return _allProcesses
                    .Where(p => p.DisplayName.ToLowerInvariant().Contains(filterLower) ||
                               p.ProcessName.ToLowerInvariant().Contains(filterLower))
                    .ToList();
            }
        }

        private List<ConfigurableProcess> FilterWithWildcards(string pattern)
        {
            try
            {
                // Konvertiere das Wildcard-Pattern zu Regex
                var regexPattern = WildcardToRegex(pattern);
                var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

                return _allProcesses
                    .Where(p => regex.IsMatch(p.DisplayName) || regex.IsMatch(p.ProcessName))
                    .ToList();
            }
            catch
            {
                // Falls das Regex-Pattern ungültig ist, fall zurück auf normale Suche
                var filterLower = pattern.ToLowerInvariant();
                return _allProcesses
                    .Where(p => p.DisplayName.ToLowerInvariant().Contains(filterLower) ||
                               p.ProcessName.ToLowerInvariant().Contains(filterLower))
                    .ToList();
            }
        }

        private string WildcardToRegex(string pattern)
        {
            // Escape alle Regex-Sonderzeichen außer * und ?
            var escaped = Regex.Escape(pattern);

            // Ersetze die escapten Wildcard-Zeichen mit Regex-Äquivalenten
            var regex = escaped
                .Replace(@"\*", ".*")      // * = beliebig viele Zeichen
                .Replace(@"\?", ".");      // ? = genau ein Zeichen

            // Anchore das Pattern um exakte Übereinstimmung zu ermöglichen
            return "^" + regex + "$";
        }

        public List<MonitoredProgram> GetSelectedPrograms()
        {
            return _allProcesses
                .Where(p => p.IsSelected)
                .Select(p => new MonitoredProgram
                {
                    ProcessName = p.ProcessName,
                    DisplayName = p.DisplayName,
                    Description = p.Description,
                    AutoRestart = p.AutoRestart,
                    StartCommand = p.StartCommand
                })
                .ToList();
        }

        public List<MonitoredProgram> GetDeselectedPrograms()
        {
            return _allProcesses
                .Where(p => !p.IsSelected)
                .Select(p => new MonitoredProgram
                {
                    ProcessName = p.ProcessName,
                    DisplayName = p.DisplayName,
                    Description = p.Description,
                    AutoRestart = p.AutoRestart,
                    StartCommand = p.StartCommand
                })
                .ToList();
        }

        public void SelectAllFiltered()
        {
            foreach (var process in _availableProcesses)
            {
                process.IsSelected = true;
            }
        }

        public void DeselectAll()
        {
            foreach (var process in _allProcesses)
            {
                process.IsSelected = false;
            }
        }

        public void RefreshProcesses()
        {
            LoadAvailableProcesses();
            FilterText = string.Empty;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Repräsentiert einen konfigurierbaren Prozess
    /// </summary>
    public class ConfigurableProcess : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _displayName = string.Empty;
        private string _description = string.Empty;
        private bool _autoRestart;
        private string _startCommand = string.Empty;

        public string ProcessName { get; set; } = string.Empty;

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoRestart
        {
            get => _autoRestart;
            set
            {
                if (_autoRestart != value)
                {
                    _autoRestart = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StartCommand
        {
            get => _startCommand;
            set
            {
                if (_startCommand != value)
                {
                    _startCommand = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
