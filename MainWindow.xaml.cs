using System;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace TaskPilot
{
    public partial class MainWindow : Window
    {
        private readonly ProcessMonitor _monitor;
        private readonly System.Windows.Threading.DispatcherTimer _updateTimer;
        private ObservableCollection<ProgramStatus> _programStatuses;

        public MainWindow()
        {
            InitializeComponent();

            _programStatuses = new ObservableCollection<ProgramStatus>();
            ProgramsDataGrid.ItemsSource = _programStatuses;

            _monitor = new ProcessMonitor();
            _monitor.StatusChanged += OnStatusChanged;

            // Timer für automatische Aktualisierung (alle 5 Sekunden)
            _updateTimer = new System.Windows.Threading.DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromSeconds(5);
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();

            LoadConfiguration();
            UpdateProgramStatuses();
        }

        private void LoadConfiguration()
        {
            try
            {
                var programs = IniConfigReader.ReadConfiguration("programs.ini");
                _monitor.SetMonitoredPrograms(programs);
                StatusText.Text = $"Konfiguration geladen: {programs.Count} Programme";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Konfiguration:\n{ex.Message}",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Fehler beim Laden der Konfiguration";
            }
        }

        private void UpdateProgramStatuses()
        {
            _monitor.UpdateStatuses();

            Dispatcher.Invoke(() =>
            {
                _programStatuses.Clear();
                foreach (var status in _monitor.GetStatuses())
                {
                    _programStatuses.Add(status);
                }

                ProgramCountText.Text = $"{_programStatuses.Count} Programme überwacht";
                LastUpdateText.Text = $"Letzte Aktualisierung: {DateTime.Now:HH:mm:ss}";
            });
        }

        private void OnStatusChanged(object? sender, ProgramStatus status)
        {
            // Könnte für Benachrichtigungen genutzt werden
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            UpdateProgramStatuses();
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Fenster verstecken statt zu schließen
            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }

        public void ReloadConfiguration()
        {
            LoadConfiguration();
            UpdateProgramStatuses();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            UpdateProgramStatuses();
        }

        private void EditConfig_Click(object sender, RoutedEventArgs e)
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "programs.ini");

            if (File.Exists(configPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = configPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Öffnen der Konfigurationsdatei:\n{ex.Message}",
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Konfigurationsdatei nicht gefunden!",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
