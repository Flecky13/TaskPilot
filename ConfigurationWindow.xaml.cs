using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.ComponentModel;

namespace TaskPilot
{
    public partial class ConfigurationWindow : Window
    {
        private ConfigurationWindowViewModel? _viewModel;
        private string _configFilePath;

        public ConfigurationWindow(string configFilePath, List<MonitoredProgram> monitoredPrograms)
        {
            InitializeComponent();

            _configFilePath = configFilePath;
            _viewModel = new ConfigurationWindowViewModel(monitoredPrograms);
            DataContext = _viewModel;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            // Neu laden der INI
            _viewModel?.RefreshProcesses();
            DialogHelper.ShowConfigurationReloaded();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.SelectAllFiltered();
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            var result = DialogHelper.AskRemoveAllProcesses();
            if (result == MessageBoxResult.Yes)
            {
                _viewModel?.DeselectAll();
            }
        }

        private void ShowHelp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var helpWindow = new HelpWindow()
                {
                    Owner = this
                };
                helpWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Öffnen der Hilfe", ex.Message);
            }
        }

        private void AddProcesses_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addWindow = new AddProcessesWindow(_configFilePath)
                {
                    Owner = this
                };

                bool? result = addWindow.ShowDialog();

                if (result == true)
                {
                    // Neu laden der INI nach dem Hinzufügen
                    var updatedPrograms = IniConfigReader.ReadConfiguration(_configFilePath);
                    _viewModel = new ConfigurationWindowViewModel(updatedPrograms);
                    DataContext = _viewModel;
                    DialogHelper.ShowNewProcessesLoaded();
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Öffnen des Prozess-Hinzufügen-Fensters", ex.Message);
            }
        }

        private void AddSingleProcess_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addWindow = new AddSingleProcessWindow()
                {
                    Owner = this
                };

                bool? result = addWindow.ShowDialog();

                if (result == true)
                {
                    // Füge den neuen Prozess hinzu
                    var newProgram = new MonitoredProgram
                    {
                        ProcessName = addWindow.ProcessName,
                        DisplayName = addWindow.DisplayName,
                        Description = addWindow.Description,
                        AutoRestart = addWindow.AutoRestart,
                        StartCommand = addWindow.StartCommand,
                        IsSelected = addWindow.IsSelected,
                        LastStartedPID = 0
                    };

                    // Speichere sofort in der INI
                    IniConfigReader.SaveConfiguration(_configFilePath, new List<MonitoredProgram> { newProgram }, appendMode: true);

                    // Neu laden der ViewModel
                    var updatedPrograms = IniConfigReader.ReadConfiguration(_configFilePath);
                    _viewModel = new ConfigurationWindowViewModel(updatedPrograms);
                    DataContext = _viewModel;

                    DialogHelper.ShowNewProcessAdded(newProgram.DisplayName);
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Hinzufügen des Prozesses", ex.Message);
            }
        }

        private void DeleteProcess_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ProcessesDataGrid.SelectedItem is ConfigurableProcess process)
                {
                    var result = DialogHelper.AskDeleteProcess(process.DisplayName);
                    if (result == MessageBoxResult.Yes)
                    {
                        _viewModel?.RemoveProcess(process);
                        DialogHelper.ShowProcessDeleted(process.DisplayName);
                    }
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Löschen des Prozesses", ex.Message);
            }
        }

        private void EditProcess_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ProcessesDataGrid.SelectedItem is ConfigurableProcess process)
                {
                    // Erstelle MonitoredProgram aus ConfigurableProcess
                    var program = new MonitoredProgram
                    {
                        ProcessName = process.ProcessName,
                        DisplayName = process.DisplayName,
                        Description = process.Description,
                        StartCommand = process.StartCommand,
                        AutoRestart = process.AutoRestart,
                        IsSelected = process.IsSelected,
                        LastStartedPID = 0
                    };

                    // Öffne Dialog im Bearbeitungsmodus
                    var editWindow = new AddSingleProcessWindow(program)
                    {
                        Owner = this
                    };

                    bool? result = editWindow.ShowDialog();

                    if (result == true)
                    {
                        // Aktualisiere den Prozess in der ViewModel
                        process.ProcessName = editWindow.ProcessName;
                        process.DisplayName = editWindow.DisplayName;
                        process.Description = editWindow.Description;
                        process.StartCommand = editWindow.StartCommand;
                        process.AutoRestart = editWindow.AutoRestart;
                        process.IsSelected = editWindow.IsSelected;

                        // Speichere sofort in der INI
                        var allPrograms = _viewModel?.GetAllPrograms() ?? new List<MonitoredProgram>();
                        IniConfigReader.SaveConfiguration(_configFilePath, allPrograms);

                        DialogHelper.ShowInfo($"Der Prozess \"{editWindow.DisplayName}\" wurde erfolgreich aktualisiert.");
                    }
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Bearbeiten des Prozesses", ex.Message);
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Hole ALLE Prozesse (sowohl markiert als auch unmarkiert)
                var allPrograms = _viewModel?.GetAllPrograms() ?? new List<MonitoredProgram>();

                if (allPrograms.Count == 0)
                {
                    DialogHelper.ShowSelectAtLeastOneProcess();
                    return;
                }

                // Validierung: Auto-Restart erfordert StartCommand
                var invalidPrograms = allPrograms
                    .Where(p => p.AutoRestart && string.IsNullOrWhiteSpace(p.StartCommand))
                    .ToList();

                if (invalidPrograms.Count > 0)
                {
                    var programList = string.Join("\n• ", invalidPrograms.Select(p => p.DisplayName));
                    DialogHelper.ShowValidationError(
                        $"Folgende Programme haben Auto-Restart aktiviert, aber keinen Startbefehl definiert:\n\n• {programList}\n\n" +
                        $"Bitte definieren Sie einen Startbefehl für diese Programme oder deaktivieren Sie Auto-Restart.");
                    return;
                }

                // Speichere ALLE Programme in die INI
                IniConfigReader.SaveConfiguration(_configFilePath, allPrograms);

                // Zähle nur die überwachten für die Meldung
                var monitoredCount = allPrograms.Count(p => p.IsSelected);
                DialogHelper.ShowConfigurationSaved(monitoredCount);

                // Main-Window sofort aktualisieren, damit Änderungen direkt sichtbar sind
                if (Owner is MainWindow mainWindow)
                {
                    mainWindow.ReloadConfiguration();
                }

                // Fenster bleibt offen - nicht schließen!
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Speichern der Konfiguration", ex.Message);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // MainWindow aktualisieren beim Schließen
            if (Owner is MainWindow mainWindow)
            {
                mainWindow.ReloadConfiguration();
            }

            DialogResult = false;
            Close();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (Owner is MainWindow mainWindow)
            {
                mainWindow.ReloadConfiguration();
            }
        }
    }
}
