using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace TaskPilot
{
    public partial class MainWindow : Window
    {
        private readonly ProcessMonitor _monitor;
        private readonly System.Windows.Threading.DispatcherTimer _updateTimer;
        private ObservableCollection<ProgramStatus> _programStatuses;
        private string _configPath;
        private List<MonitoredProgram> _currentPrograms;
        private HashSet<string> _recentlyRestartedProcesses = new HashSet<string>(); // Cache um doppelte Restarts zu vermeiden
        private bool _autoStartEnabled = true; // Control-Flag für Auto-Restart (nicht persistent)

        public MainWindow()
        {
            InitializeComponent();

            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "programs.ini");
            _programStatuses = new ObservableCollection<ProgramStatus>();
            _currentPrograms = new List<MonitoredProgram>();
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
                _currentPrograms = IniConfigReader.ReadConfiguration(_configPath);
                _monitor.SetMonitoredPrograms(_currentPrograms);
                StatusText.Text = $"Konfiguration geladen: {_currentPrograms.Count} Programme";
            }
            catch (Exception ex)
            {
                DialogHelper.ShowConfigurationError(ex.Message);
                StatusText.Text = "Fehler beim Laden der Konfiguration";
            }
        }

        private void UpdateProgramStatuses()
        {
            _monitor.UpdateStatuses();

            Dispatcher.Invoke(() =>
            {
                _programStatuses.Clear();
                int inactiveCount = 0;
                var restartedThisRound = new List<string>(); // Track was in dieser Runde restartet wurde

                foreach (var status in _monitor.GetStatuses())
                {
                    _programStatuses.Add(status);

                    // Überprüfe auch bereits inaktive Prozesse für Auto-Restart
                    if (!status.IsActive)
                    {
                        inactiveCount++;
                        DebugWindow.Instance?.LogMessage($"[UpdateProgramStatuses] Prozess #{inactiveCount} inaktiv: {status.ProcessName}");

                        var program = _currentPrograms.FindByProcessName(status.ProcessName);

                        if (program != null)
                        {
                            DebugWindow.Instance?.LogMessage($"[UpdateProgramStatuses] → Program gefunden: {program.DisplayName}, AutoRestart={program.AutoRestart}, HasCommand={!string.IsNullOrWhiteSpace(program.StartCommand)}");

                            if (program.AutoRestart && !string.IsNullOrWhiteSpace(program.StartCommand))
                            {
                                // Prüfe ob dieser Prozess kürzlich restartet wurde (Cooldown von 5 Sekunden)
                                if (_recentlyRestartedProcesses.Contains(program.ProcessName))
                                {
                                    DebugWindow.Instance?.LogMessage($"[UpdateProgramStatuses] → SKIPPED: {program.DisplayName} wurde gerade restartet (Cooldown)");
                                }
                                else
                                {
                                    DebugWindow.Instance?.LogMessage($"[UpdateProgramStatuses] → Starte Auto-Restart für {program.DisplayName}");
                                    TryRestartProcess(program, status);
                                    restartedThisRound.Add(program.ProcessName);
                                }
                            }
                            else
                            {
                                DebugWindow.Instance?.LogMessage($"[UpdateProgramStatuses] → SKIPPED: AutoRestart={program.AutoRestart}, HasCommand={!string.IsNullOrWhiteSpace(program.StartCommand)}");
                            }
                        }
                        else
                        {
                            DebugWindow.Instance?.LogMessage($"[UpdateProgramStatuses] → Program NICHT gefunden!");
                        }
                    }
                }

                // Aktualisiere den Cache mit gerade gestarteten Prozessen
                if (restartedThisRound.Count > 0)
                {
                    DebugWindow.Instance?.LogMessage($"[UpdateProgramStatuses] → {restartedThisRound.Count} Prozess(e) restartet in dieser Runde");
                    foreach (var procName in restartedThisRound)
                    {
                        _recentlyRestartedProcesses.Add(procName);
                    }

                    // Entferne die Einträge nach 5 Sekunden (Cooldown)
                    _ = System.Threading.Tasks.Task.Delay(5000).ContinueWith(_ =>
                    {
                        foreach (var procName in restartedThisRound)
                        {
                            _recentlyRestartedProcesses.Remove(procName);
                        }
                    });
                }

                DebugWindow.Instance?.LogMessage($"[UpdateProgramStatuses] Fertig - {inactiveCount} inaktive Prozesse geprüft");
                ProgramCountText.Text = $"{_programStatuses.Count} Programme überwacht";
                LastUpdateText.Text = $"Letzte Aktualisierung: {DateTime.Now:HH:mm:ss}";
            });
        }        private void OnStatusChanged(object? sender, ProgramStatus status)
        {
            // Per-Programm Auto-Restart: nur wenn im Programm konfiguriert
            if (!status.IsActive)
            {
                var program = _currentPrograms.FindByProcessName(status.ProcessName);

                if (program != null)
                {
                    DebugWindow.Instance?.LogMessage($"[OnStatusChanged] Prozess inaktiv: {status.ProcessName} ({status.DisplayName})");
                    TryRestartProcess(program, status);
                }
            }
        }

        private void TryRestartProcess(MonitoredProgram program, ProgramStatus status)
        {
            // Check if global AutoStart is enabled
            if (!_autoStartEnabled)
            {
                DebugWindow.Instance?.LogMessage($"[TryRestartProcess] AutoStart ist deaktiviert - {program.DisplayName} wird nicht neu gestartet");
                return;
            }

            try
            {
                DebugWindow.Instance?.LogMessage($"[TryRestartProcess] Starten: {program.DisplayName}");
                DebugWindow.Instance?.LogMessage($"  → AutoRestart: {program.AutoRestart}");
                DebugWindow.Instance?.LogMessage($"  → Befehl: {program.StartCommand}");

                var startCommand = program.StartCommand.Trim();

                ProcessStartInfo? startInfo = null;

                // Überprüfe ob der Command mit "start" beginnt (Windows START command)
                if (startCommand.StartsWith("start ", StringComparison.OrdinalIgnoreCase))
                {
                    var cmdArguments = startCommand.Substring(6).Trim();
                    DebugWindow.Instance?.LogMessage($"  → Format: START-Command");

                    startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {cmdArguments}",
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                }
                else if (startCommand.Contains(" "))
                {
                    DebugWindow.Instance?.LogMessage($"  → Format: Befehl mit Parametern");

                    startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {startCommand}",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    };
                }
                else
                {
                    DebugWindow.Instance?.LogMessage($"  → Format: Direkter Start");

                    startInfo = new ProcessStartInfo
                    {
                        FileName = startCommand,
                        UseShellExecute = true,
                        CreateNoWindow = true
                    };
                }

                var startedProcess = Process.Start(startInfo);
                DebugWindow.Instance?.LogMessage($"✓ {program.DisplayName} gestartet (PID: {startedProcess?.Id})");
                StatusText.Text = $"✓ {program.DisplayName} neu gestartet";
            }
            catch (Exception ex)
            {
                DebugWindow.Instance?.LogMessage($"✗ FEHLER bei {program.DisplayName}: {ex.Message}");
                StatusText.Text = $"✗ Fehler: {program.DisplayName} - {ex.Message}";
            }
        }        private void UpdateTimer_Tick(object? sender, EventArgs e)
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

        private void ShowDebug_Click(object sender, RoutedEventArgs e)
        {
            if (DebugWindow.Instance == null)
            {
                var debugWindow = new DebugWindow()
                {
                    Owner = this
                };
                debugWindow.Show();
            }
            else
            {
                DebugWindow.Instance.Activate();
            }
        }

        private void EditConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentPrograms = IniConfigReader.ReadConfiguration(_configPath);

                var configWindow = new ConfigurationWindow(_configPath, currentPrograms)
                {
                    Owner = this
                };

                bool? result = configWindow.ShowDialog();

                if (result == true)
                {
                    LoadConfiguration();
                    UpdateProgramStatuses();
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Öffnen der Konfiguration", ex.Message);
            }
        }

        private void AutoStartCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _autoStartEnabled = true;
            DebugWindow.Instance?.LogMessage("[AutoStart] Aktiviert");
        }

        private void AutoStartCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _autoStartEnabled = false;
            DebugWindow.Instance?.LogMessage("[AutoStart] Deaktiviert");
        }
    }
}
