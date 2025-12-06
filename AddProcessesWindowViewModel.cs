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
    /// ViewModel für das AddProcessesWindow
    /// </summary>
    public class AddProcessesWindowViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<SelectableProcess> _runningProcesses;
        private List<SelectableProcess> _allRunningProcesses;
        private string _filterText = string.Empty;
        private string _configFilePath;
        private HashSet<string> _alreadyConfiguredProcesses = new HashSet<string>();

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<SelectableProcess> RunningProcesses
        {
            get => _runningProcesses;
            set
            {
                if (_runningProcesses != value)
                {
                    _runningProcesses = value;
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

        public AddProcessesWindowViewModel(string configFilePath)
        {
            _configFilePath = configFilePath;
            _runningProcesses = new ObservableCollection<SelectableProcess>();
            _allRunningProcesses = new List<SelectableProcess>();

            // Lade bereits konfigurierte Prozesse
            LoadConfiguredProcesses();

            // Lade laufende Prozesse
            LoadRunningProcesses();
        }

        private void LoadConfiguredProcesses()
        {
            try
            {
                var currentPrograms = IniConfigReader.ReadConfiguration(_configFilePath);
                _alreadyConfiguredProcesses = new HashSet<string>(
                    currentPrograms.Select(p => p.ProcessName.ToLowerInvariant())
                );
            }
            catch
            {
                _alreadyConfiguredProcesses = new HashSet<string>();
            }
        }

        private void LoadRunningProcesses()
        {
            var runningProcesses = RunningProcessProvider.GetRunningProcesses();

            _allRunningProcesses.Clear();

            foreach (var process in runningProcesses)
            {
                // Überspringe bereits konfigurierte Prozesse
                if (_alreadyConfiguredProcesses.Contains(process.ProcessName.ToLowerInvariant()))
                {
                    continue;
                }

                _allRunningProcesses.Add(new SelectableProcess
                {
                    ProcessName = process.ProcessName,
                    DisplayName = process.DisplayName,
                    Description = string.Empty,
                    IsSelected = false
                });
            }

            // Sortiere nach DisplayName
            _allRunningProcesses = _allRunningProcesses.OrderBy(p => p.DisplayName).ToList();

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            List<SelectableProcess> source;

            if (string.IsNullOrWhiteSpace(_filterText))
            {
                source = _allRunningProcesses;
            }
            else
            {
                source = FilterProcesses(_filterText);
            }

            // Sortiere nach DisplayName
            source = source.OrderBy(p => p.DisplayName).ToList();

            _runningProcesses.Clear();
            foreach (var process in source)
            {
                _runningProcesses.Add(process);
            }
        }

        private List<SelectableProcess> FilterProcesses(string filterText)
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
                return _allRunningProcesses
                    .Where(p => p.DisplayName.ToLowerInvariant().Contains(filterLower) ||
                               p.ProcessName.ToLowerInvariant().Contains(filterLower))
                    .ToList();
            }
        }

        private List<SelectableProcess> FilterWithWildcards(string pattern)
        {
            try
            {
                // Konvertiere das Wildcard-Pattern zu Regex
                var regexPattern = WildcardToRegex(pattern);
                var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

                return _allRunningProcesses
                    .Where(p => regex.IsMatch(p.DisplayName) || regex.IsMatch(p.ProcessName))
                    .ToList();
            }
            catch
            {
                // Falls das Regex-Pattern ungültig ist, fall zurück auf normale Suche
                var filterLower = pattern.ToLowerInvariant();
                return _allRunningProcesses
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

        public List<SelectableProcess> GetSelectedProcesses()
        {
            return _allRunningProcesses
                .Where(p => p.IsSelected)
                .ToList();
        }

        public void SelectAllFiltered()
        {
            foreach (var process in _runningProcesses)
            {
                process.IsSelected = true;
            }
        }

        public void RefreshRunningProcesses()
        {
            LoadConfiguredProcesses();
            LoadRunningProcesses();
            FilterText = string.Empty;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Repräsentiert einen wählbaren laufenden Prozess
    /// </summary>
    public class SelectableProcess : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _displayName = string.Empty;
        private string _description = string.Empty;

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
