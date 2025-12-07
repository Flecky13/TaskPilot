using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace TaskPilot
{
    public partial class AddProcessesWindow : Window
    {
        private AddProcessesWindowViewModel? _viewModel;
        private string _configFilePath;

        // Für die Rückgabe an AddSingleProcessWindow
        public SelectableProcess? SelectedProcess { get; private set; }

        public AddProcessesWindow(string configFilePath)
        {
            InitializeComponent();

            _configFilePath = configFilePath;
            _viewModel = new AddProcessesWindowViewModel(configFilePath);
            DataContext = _viewModel;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.RefreshRunningProcesses();
            DialogHelper.ShowProcessListUpdated();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.SelectAllFiltered();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedProcesses = _viewModel?.GetSelectedProcesses() ?? new List<SelectableProcess>();

                if (selectedProcesses.Count == 0)
                {
                    DialogHelper.ShowSelectAtLeastOneProcess();
                    return;
                }

                // Wenn nur ein Prozess ausgewählt ist, gib diesen zu AddSingleProcessWindow zurück
                if (selectedProcesses.Count == 1)
                {
                    SelectedProcess = selectedProcesses[0];
                    DialogResult = true;
                    Close();
                    return;
                }

                // Wenn mehrere Prozesse ausgewählt sind, speichere sie direkt in INI
                // Lade aktuelle INI-Konfiguration
                var currentPrograms = IniConfigReader.ReadConfiguration(_configFilePath);

                // Füge neue Prozesse hinzu (überspringe vorhandene)
                foreach (var selectedProcess in selectedProcesses)
                {
                    var exists = currentPrograms.ExistsByProcessName(selectedProcess.ProcessName);

                    if (!exists)
                    {
                        currentPrograms.Add(new MonitoredProgram
                        {
                            ProcessName = selectedProcess.ProcessName,
                            DisplayName = selectedProcess.DisplayName,
                            Description = selectedProcess.Description,
                            AutoRestart = false,
                            StartCommand = string.Empty
                        });
                    }
                }

                // Speichere die erweiterte Konfiguration
                IniConfigReader.SaveConfiguration(_configFilePath, currentPrograms);

                DialogHelper.ShowProcessesAdded(selectedProcesses.Count);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Speichern der Prozesse", ex);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
