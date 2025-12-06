using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

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
                DialogHelper.ShowOperationError("\u00d6ffnen des Prozess-Hinzuf\u00fcgen-Fensters", ex.Message);
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedPrograms = _viewModel?.GetSelectedPrograms() ?? new List<MonitoredProgram>();
                var deselectedPrograms = _viewModel?.GetDeselectedPrograms() ?? new List<MonitoredProgram>();

                if (selectedPrograms.Count == 0)
                {
                    DialogHelper.ShowSelectAtLeastOneProcess();
                    return;
                }

                // Validierung: Auto-Restart erfordert StartCommand
                var invalidPrograms = selectedPrograms
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

                IniConfigReader.SaveConfiguration(_configFilePath, selectedPrograms);
                DialogHelper.ShowConfigurationSaved(selectedPrograms.Count);

                // Fenster bleibt offen - nicht schließen!
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Speichern der Konfiguration", ex.Message);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
