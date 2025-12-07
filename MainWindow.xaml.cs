using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TaskPilot
{
    public partial class MainWindow : Window
    {
        private readonly ProcessMonitor _monitor;
        private readonly System.Windows.Threading.DispatcherTimer _updateTimer;
        private ObservableCollection<ProgramStatus> _programStatuses;
        private string _configPath;
        private List<MonitoredProgram> _currentPrograms;
        private readonly HashSet<string> _recentlyRestartedProcesses = new HashSet<string>(); // Cache um doppelte Restarts zu vermeiden
        private readonly object _restartLock = new object();
        private bool _autoStartEnabled = true; // Control-Flag für Auto-Restart (nicht persistent)

        public MainWindow()
        {
            InitializeComponent();

            // INI-Datei im %APPDATA%\TaskPilot Verzeichnis speichern (für Schreibzugriff bei Installation in Program Files)
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskPilot");
            Directory.CreateDirectory(appDataPath); // Stelle sicher, dass das Verzeichnis existiert
            _configPath = Path.Combine(appDataPath, "programs.ini");

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
                // Nur überwachte Programme (IsSelected) ins Monitoring übernehmen
                _currentPrograms = IniConfigReader.ReadConfiguration(_configPath)
                    .Where(p => p.IsSelected)
                    .ToList();

                _monitor.SetMonitoredPrograms(_currentPrograms);
                StatusText.Text = $"Konfiguration geladen: {_currentPrograms.Count} überwachte Programme";
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
                                var procKey = program.ProcessName.ToLowerInvariant();
                                if (_recentlyRestartedProcesses.Contains(procKey))
                                {
                                    DebugWindow.Instance?.LogMessage($"[UpdateProgramStatuses] → SKIPPED: {program.DisplayName} wurde gerade restartet (Cooldown)");
                                }
                                else
                                {
                                    DebugWindow.Instance?.LogMessage($"[UpdateProgramStatuses] → Starte Auto-Restart für {program.DisplayName}");
                                    TryRestartProcess(program, status);
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

                DebugWindow.Instance?.LogMessage($"[UpdateProgramStatuses] Fertig - {inactiveCount} inaktive Prozesse geprüft");
                ProgramCountText.Text = $"{_programStatuses.Count} Programme überwacht";
                LastUpdateText.Text = $"Letzte Aktualisierung: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
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

                    // Nur automatisch neu starten, wenn AutoRestart konfiguriert und StartCommand vorhanden
                    if (program.AutoRestart && !string.IsNullOrWhiteSpace(program.StartCommand))
                    {
                        TryRestartProcess(program, status);
                    }
                    else
                    {
                        DebugWindow.Instance?.LogMessage($"[OnStatusChanged] → Kein Auto-Restart konfiguriert, wird nicht neu gestartet");
                    }
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

            StartProcessWithCommand(program, status);
        }

        // Manueller Start (von Kontextmenü) - ignoriert AutoStart-Flag
        private void ManualStartProcess(MonitoredProgram program, ProgramStatus status)
        {
            StartProcessWithCommand(program, status);
        }

        // Gemeinsame Start-Logik
        private void StartProcessWithCommand(MonitoredProgram program, ProgramStatus status)
        {
            // Cooldown-Schutz mit Lock, um parallele Aufrufe abzufangen
            var procKey = program.ProcessName.ToLowerInvariant();
            lock (_restartLock)
            {
                if (_recentlyRestartedProcesses.Contains(procKey))
                {
                    DebugWindow.Instance?.LogMessage($"[StartProcessWithCommand] SKIPPED (Cooldown): {program.DisplayName}");
                    return;
                }

                _recentlyRestartedProcesses.Add(procKey);
            }

            _ = Task.Delay(5000).ContinueWith(_ =>
            {
                lock (_restartLock)
                {
                    _recentlyRestartedProcesses.Remove(procKey);
                }
            });

            try
            {
                DebugWindow.Instance?.LogMessage($"[StartProcessWithCommand] Starten: {program.DisplayName}");
                DebugWindow.Instance?.LogMessage($"  → AutoRestart: {program.AutoRestart}");
                DebugWindow.Instance?.LogMessage($"  → Befehl: {program.StartCommand}");

                var startCommand = program.StartCommand.Trim();

                // Generiere eindeutigen Fenster-Titel (DisplayName ohne TaskPilot-Zusatz)
                string windowTitle = $"{program.DisplayName}";

                ProcessStartInfo? startInfo = null;

                // Überprüfe ob der Command mit "start" beginnt (Windows START command)
                if (startCommand.StartsWith("start ", StringComparison.OrdinalIgnoreCase))
                {
                    var cmdArguments = startCommand.Substring(6).Trim();
                    DebugWindow.Instance?.LogMessage($"  → Format: START-Command");

                    // Füge TITLE-Befehl hinzu
                    var fullCommand = $"title {windowTitle} & {cmdArguments}";

                    startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {fullCommand}",
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                }
                else if (startCommand.Contains(" "))
                {
                    DebugWindow.Instance?.LogMessage($"  → Format: Befehl mit Parametern");

                    // Füge TITLE-Befehl hinzu
                    var fullCommand = $"title {windowTitle} & {startCommand}";

                    startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {fullCommand}",
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
                if (startedProcess != null)
                {
                    program.LastStartedPID = startedProcess.Id;
                    DebugWindow.Instance?.LogMessage($"✓ {program.DisplayName} gestartet (PID: {startedProcess.Id})");
                    DebugWindow.Instance?.LogMessage($"  → Fenster-Titel: {windowTitle}");
                    StatusText.Text = $"✓ {program.DisplayName} neu gestartet (PID: {startedProcess.Id})";
                }
                else
                {
                    DebugWindow.Instance?.LogMessage($"✗ Prozess konnte nicht gestartet werden: {program.DisplayName}");
                    StatusText.Text = $"✗ Fehler: {program.DisplayName} konnte nicht gestartet werden";
                }
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

        // Event: Status-Spalte Rechtsklick
        private void StatusCell_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ProgramStatus status)
            {
                // Finde die entsprechende Zeile und selektiere sie
                var row = ItemsControl.ContainerFromElement(ProgramsDataGrid, element) as DataGridRow;
                if (row != null)
                {
                    row.IsSelected = true;
                    row.Focus();
                }
            }
        }

        // Event: Context Menu öffnen - dynamisch Menu Items en/disable
        private void ContextMenu_Opening(object sender, ContextMenuEventArgs e)
        {
            if (sender is ContextMenu menu && ProgramsDataGrid.SelectedItem is ProgramStatus status)
            {
                // Finde die Menu Items
                var stopMenuItem = menu.Items.Cast<MenuItem>().FirstOrDefault(m => m.Name == "StopMenuItem");
                var startMenuItem = menu.Items.Cast<MenuItem>().FirstOrDefault(m => m.Name == "StartMenuItem");

                if (stopMenuItem != null)
                    stopMenuItem.IsEnabled = status.IsActive;

                if (startMenuItem != null)
                {
                    var program = _currentPrograms.FindByProcessName(status.ProcessName);
                    // Start-MenuItem nur enablen, wenn: nicht aktiv UND StartCommand vorhanden
                    startMenuItem.IsEnabled = !status.IsActive && program != null && !string.IsNullOrWhiteSpace(program.StartCommand);
                }
            }
        }

        // Kontextmenü: Prozess stoppen
        private void StopProcessMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramsDataGrid.SelectedItem is ProgramStatus status)
            {
                try
                {
                    var program = _currentPrograms.FindByProcessName(status.ProcessName);
                    if (program != null)
                    {
                        // Versuche zuerst mit gespeicherter PID zu killen
                        if (program.LastStartedPID > 0)
                        {
                            try
                            {
                                var proc = Process.GetProcessById(program.LastStartedPID);
                                proc.Kill();
                                DebugWindow.Instance?.LogMessage($"✓ Prozess {status.ProcessName} (PID {program.LastStartedPID}) gestoppt");
                                program.LastStartedPID = 0;
                            }
                            catch (Exception ex)
                            {
                                DebugWindow.Instance?.LogMessage($"Warnung: PID {program.LastStartedPID} konnte nicht gestoppt werden: {ex.Message}");
                            }
                        }

                        // Fallback: Alle Prozesse mit diesem Namen stoppen
                        var allProcs = Process.GetProcessesByName(status.ProcessName);
                        foreach (var proc in allProcs)
                        {
                            try
                            {
                                proc.Kill();
                                DebugWindow.Instance?.LogMessage($"✓ Prozess {proc.ProcessName} (PID {proc.Id}) gestoppt");
                            }
                            catch (Exception ex)
                            {
                                DebugWindow.Instance?.LogMessage($"Fehler beim Stoppen von {proc.ProcessName}: {ex.Message}");
                            }
                        }

                        StatusText.Text = $"Prozess {status.ProcessName} gestoppt.";
                        UpdateProgramStatuses();
                    }
                }
                catch (Exception ex)
                {
                    DebugWindow.Instance?.LogMessage($"✗ Fehler beim Stoppen: {ex.Message}");
                    StatusText.Text = $"Fehler beim Stoppen: {ex.Message}";
                }
            }
        }

        // Kontextmenü: Prozess starten (wenn StartCommand vorhanden)
        private void StartProcessMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramsDataGrid.SelectedItem is ProgramStatus status)
            {
                var program = _currentPrograms.FindByProcessName(status.ProcessName);
                if (program != null && !string.IsNullOrWhiteSpace(program.StartCommand))
                {
                    ManualStartProcess(program, status);
                }
                else
                {
                    StatusText.Text = $"Kein Startbefehl für {status.ProcessName} konfiguriert.";
                }
            }
        }

        // Kontextmenü: Prozess minimieren
        private void MinimizeProcess_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramsDataGrid.SelectedItem is ProgramStatus status)
            {
                DebugWindow.Instance?.LogMessage($"[MinimizeProcess_Click] Starten für {status.ProcessName}");
                try
                {
                    var program = _currentPrograms.FindByProcessName(status.ProcessName);
                    DebugWindow.Instance?.LogMessage($"[MinimizeProcess_Click] Program gefunden: {program?.DisplayName}, LastPID: {program?.LastStartedPID}");
                    int windowCount = 0;

                    // Versuche zuerst, die gespeicherte PID zu verwenden
                    if (program?.LastStartedPID > 0)
                    {
                        DebugWindow.Instance?.LogMessage($"[MinimizeProcess_Click] Versuche gespeicherte PID: {program.LastStartedPID}");
                        try
                        {
                            var proc = Process.GetProcessById(program.LastStartedPID);
                            DebugWindow.Instance?.LogMessage($"[MinimizeProcess_Click] Prozess gefunden (PID {proc.Id}), MainWindowHandle: {proc.MainWindowHandle}");
                            if (MinimizeProcessWindow(proc))
                                windowCount++;
                        }
                        catch (Exception ex)
                        {
                            // PID existiert nicht mehr, fallback zu ProcessName
                            DebugWindow.Instance?.LogMessage($"[MinimizeProcess_Click] Fehler bei gespeicherter PID: {ex.Message}");
                            program.LastStartedPID = 0;
                        }
                    }

                    // Fallback: Alle Prozesse mit diesem Namen
                    if (windowCount == 0)
                    {
                        DebugWindow.Instance?.LogMessage($"[MinimizeProcess_Click] Fallback zu ProcessName: {status.ProcessName}");
                        var allProcs = Process.GetProcessesByName(status.ProcessName);
                        DebugWindow.Instance?.LogMessage($"[MinimizeProcess_Click] {allProcs.Length} Prozesse mit Namen '{status.ProcessName}' gefunden");
                        foreach (var proc in allProcs)
                        {
                            DebugWindow.Instance?.LogMessage($"[MinimizeProcess_Click] Prozess: {proc.ProcessName} (PID {proc.Id}), MainWindowHandle: {proc.MainWindowHandle}");
                            if (MinimizeProcessWindow(proc))
                                windowCount++;
                        }
                    }

                    if (windowCount > 0)
                        StatusText.Text = $"{windowCount} Fenster von {status.ProcessName} minimiert.";
                    else
                        StatusText.Text = $"Keine Fenster für {status.ProcessName} gefunden.";
                }
                catch (Exception ex)
                {
                    DebugWindow.Instance?.LogMessage($"[MinimizeProcess_Click] Exception: {ex.Message}");
                    StatusText.Text = $"Fehler beim Minimieren: {ex.Message}";
                }
            }
        }

        // Kontextmenü: Prozess in den Vordergrund / maximieren
        private void MaximizeProcess_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramsDataGrid.SelectedItem is ProgramStatus status)
            {
                DebugWindow.Instance?.LogMessage($"[MaximizeProcess_Click] Starten für {status.ProcessName}");
                try
                {
                    var program = _currentPrograms.FindByProcessName(status.ProcessName);
                    DebugWindow.Instance?.LogMessage($"[MaximizeProcess_Click] Program gefunden: {program?.DisplayName}, LastPID: {program?.LastStartedPID}");
                    int windowCount = 0;

                    // Versuche zuerst, die gespeicherte PID zu verwenden
                    if (program?.LastStartedPID > 0)
                    {
                        DebugWindow.Instance?.LogMessage($"[MaximizeProcess_Click] Versuche gespeicherte PID: {program.LastStartedPID}");
                        try
                        {
                            var proc = Process.GetProcessById(program.LastStartedPID);
                            DebugWindow.Instance?.LogMessage($"[MaximizeProcess_Click] Prozess gefunden (PID {proc.Id}), MainWindowHandle: {proc.MainWindowHandle}");
                            if (MaximizeProcessWindow(proc))
                                windowCount++;
                        }
                        catch (Exception ex)
                        {
                            // PID existiert nicht mehr, fallback zu ProcessName
                            DebugWindow.Instance?.LogMessage($"[MaximizeProcess_Click] Fehler bei gespeicherter PID: {ex.Message}");
                            program.LastStartedPID = 0;
                        }
                    }

                    // Fallback: Alle Prozesse mit diesem Namen
                    if (windowCount == 0)
                    {
                        DebugWindow.Instance?.LogMessage($"[MaximizeProcess_Click] Fallback zu ProcessName: {status.ProcessName}");
                        var allProcs = Process.GetProcessesByName(status.ProcessName);
                        DebugWindow.Instance?.LogMessage($"[MaximizeProcess_Click] {allProcs.Length} Prozesse mit Namen '{status.ProcessName}' gefunden");
                        foreach (var proc in allProcs)
                        {
                            DebugWindow.Instance?.LogMessage($"[MaximizeProcess_Click] Prozess: {proc.ProcessName} (PID {proc.Id}), MainWindowHandle: {proc.MainWindowHandle}");
                            if (MaximizeProcessWindow(proc))
                                windowCount++;
                        }
                    }

                    if (windowCount > 0)
                        StatusText.Text = $"{windowCount} Fenster von {status.ProcessName} in den Vordergrund gebracht.";
                    else
                        StatusText.Text = $"Keine Fenster für {status.ProcessName} gefunden.";
                }
                catch (Exception ex)
                {
                    DebugWindow.Instance?.LogMessage($"[MaximizeProcess_Click] Exception: {ex.Message}");
                    StatusText.Text = $"Fehler beim Maximieren: {ex.Message}";
                }
            }
        }

        // P/Invoke für Fenstersteuerung
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        // Hilfsmethode: Fenster über Titel finden
        private IntPtr FindWindowByTitle(string windowTitle)
        {
            IntPtr foundWindow = IntPtr.Zero;
            List<string> foundTitles = new List<string>(); // Debug: alle gefundenen Fenster-Titel

            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                string title = sb.ToString();

                // Debug: alle Fenster-Titel loggen
                if (!string.IsNullOrWhiteSpace(title))
                {
                    foundTitles.Add(title);
                }

                if (title.Contains(windowTitle))
                {
                    foundWindow = hWnd;
                    return false; // Stop searching
                }
                return true;
            }, IntPtr.Zero);

            // Debug-Ausgabe: alle gefundenen Fenster-Titel (erste 10)
            DebugWindow.Instance?.LogMessage($"[FindWindowByTitle] Suche nach: '{windowTitle}'");
            DebugWindow.Instance?.LogMessage($"[FindWindowByTitle] Insgesamt {foundTitles.Count} Fenster gefunden");
            for (int i = 0; i < Math.Min(10, foundTitles.Count); i++)
            {
                DebugWindow.Instance?.LogMessage($"  [{i}] {foundTitles[i]}");
            }

            return foundWindow;
        }        private bool MinimizeProcessWindow(Process proc)
        {
            const int SW_MINIMIZE = 6;
            try
            {
                proc.Refresh();
                DebugWindow.Instance?.LogMessage($"[MinimizeProcessWindow] Verarbeite {proc.ProcessName} (PID {proc.Id})");
                DebugWindow.Instance?.LogMessage($"[MinimizeProcessWindow] MainWindowHandle: {proc.MainWindowHandle} (IsZero: {proc.MainWindowHandle == IntPtr.Zero})");

                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    bool result = ShowWindow(proc.MainWindowHandle, SW_MINIMIZE);
                    DebugWindow.Instance?.LogMessage($"[MinimizeProcessWindow] ShowWindow-Rückgabe: {result}");
                    DebugWindow.Instance?.LogMessage($"[MinimizeProcess] Fenster minimiert: {proc.ProcessName} (PID {proc.Id})");
                    return true;
                }
                else
                {
                    // Versuche, das Fenster über den Titel zu finden
                    DebugWindow.Instance?.LogMessage($"[MinimizeProcessWindow] MainWindowHandle ist null - versuche über Fenster-Titel zu suchen");
                    var windowHandle = FindWindowByTitle(proc.ProcessName);

                    if (windowHandle != IntPtr.Zero)
                    {
                        DebugWindow.Instance?.LogMessage($"[MinimizeProcessWindow] Fenster über Titel gefunden!");
                        bool result = ShowWindow(windowHandle, SW_MINIMIZE);
                        DebugWindow.Instance?.LogMessage($"[MinimizeProcessWindow] ShowWindow-Rückgabe: {result}");
                        return true;
                    }
                    else
                    {
                        DebugWindow.Instance?.LogMessage($"[MinimizeProcessWindow] Fenster auch über Titel nicht gefunden");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugWindow.Instance?.LogMessage($"[MinimizeProcessWindow] Exception: {ex.GetType().Name}: {ex.Message}");
            }
            return false;
        }

        private bool MaximizeProcessWindow(Process proc)
        {
            const int SW_RESTORE = 9;
            try
            {
                proc.Refresh();
                DebugWindow.Instance?.LogMessage($"[MaximizeProcessWindow] Verarbeite {proc.ProcessName} (PID {proc.Id})");
                DebugWindow.Instance?.LogMessage($"[MaximizeProcessWindow] MainWindowHandle: {proc.MainWindowHandle} (IsZero: {proc.MainWindowHandle == IntPtr.Zero})");

                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    bool result1 = ShowWindow(proc.MainWindowHandle, SW_RESTORE);
                    bool result2 = SetForegroundWindow(proc.MainWindowHandle);
                    DebugWindow.Instance?.LogMessage($"[MaximizeProcessWindow] ShowWindow-Rückgabe: {result1}, SetForegroundWindow-Rückgabe: {result2}");
                    DebugWindow.Instance?.LogMessage($"[MaximizeProcess] Fenster in Vordergrund: {proc.ProcessName} (PID {proc.Id})");
                    return true;
                }
                else
                {
                    // Versuche, das Fenster über den Titel zu finden
                    DebugWindow.Instance?.LogMessage($"[MaximizeProcessWindow] MainWindowHandle ist null - versuche über Fenster-Titel zu suchen");
                    var windowHandle = FindWindowByTitle(proc.ProcessName);

                    if (windowHandle != IntPtr.Zero)
                    {
                        DebugWindow.Instance?.LogMessage($"[MaximizeProcessWindow] Fenster über Titel gefunden!");
                        bool result1 = ShowWindow(windowHandle, SW_RESTORE);
                        bool result2 = SetForegroundWindow(windowHandle);
                        DebugWindow.Instance?.LogMessage($"[MaximizeProcessWindow] ShowWindow-Rückgabe: {result1}, SetForegroundWindow-Rückgabe: {result2}");
                        return true;
                    }
                    else
                    {
                        DebugWindow.Instance?.LogMessage($"[MaximizeProcessWindow] Fenster auch über Titel nicht gefunden");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugWindow.Instance?.LogMessage($"[MaximizeProcessWindow] Exception: {ex.GetType().Name}: {ex.Message}");
            }
            return false;
        }        private void AutoStartCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _autoStartEnabled = true;
            DebugWindow.Instance?.LogMessage("[AutoStart] Aktiviert");
        }

        private void AutoStartCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _autoStartEnabled = false;
            DebugWindow.Instance?.LogMessage("[AutoStart] Deaktiviert");
        }

        // Button: Alle Prozesse minimieren
        private void MinimizeAll_Click(object sender, RoutedEventArgs e)
        {
            DebugWindow.Instance?.LogMessage("[MinimizeAll_Click] Starten...");
            int totalMinimized = 0;

            try
            {
                foreach (var status in _programStatuses)
                {
                    if (status.IsActive)
                    {
                        var program = _currentPrograms.FindByProcessName(status.ProcessName);
                        DebugWindow.Instance?.LogMessage($"[MinimizeAll_Click] Verarbeite: {status.ProcessName}");

                        int windowCount = 0;

                        // Versuche zuerst, die gespeicherte PID zu verwenden
                        if (program?.LastStartedPID > 0)
                        {
                            try
                            {
                                var proc = Process.GetProcessById(program.LastStartedPID);
                                if (MinimizeProcessWindow(proc))
                                    windowCount++;
                            }
                            catch (Exception ex)
                            {
                                DebugWindow.Instance?.LogMessage($"[MinimizeAll_Click] Fehler bei PID {program?.LastStartedPID}: {ex.Message}");
                            }
                        }

                        // Fallback: Alle Prozesse mit diesem Namen
                        if (windowCount == 0)
                        {
                            var allProcs = Process.GetProcessesByName(status.ProcessName);
                            foreach (var proc in allProcs)
                            {
                                if (MinimizeProcessWindow(proc))
                                    windowCount++;
                            }
                        }

                        totalMinimized += windowCount;
                    }
                }

                StatusText.Text = $"✓ {totalMinimized} Fenster minimiert.";
                DebugWindow.Instance?.LogMessage($"[MinimizeAll_Click] Abgeschlossen: {totalMinimized} Fenster minimiert");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Fehler beim Minimieren: {ex.Message}";
                DebugWindow.Instance?.LogMessage($"[MinimizeAll_Click] Exception: {ex.Message}");
            }
        }

        // Button: Alle Prozesse maximieren / in den Vordergrund
        private void MaximizeAll_Click(object sender, RoutedEventArgs e)
        {
            DebugWindow.Instance?.LogMessage("[MaximizeAll_Click] Starten...");
            int totalMaximized = 0;

            try
            {
                foreach (var status in _programStatuses)
                {
                    if (status.IsActive)
                    {
                        var program = _currentPrograms.FindByProcessName(status.ProcessName);
                        DebugWindow.Instance?.LogMessage($"[MaximizeAll_Click] Verarbeite: {status.ProcessName}");

                        int windowCount = 0;

                        // Versuche zuerst, die gespeicherte PID zu verwenden
                        if (program?.LastStartedPID > 0)
                        {
                            try
                            {
                                var proc = Process.GetProcessById(program.LastStartedPID);
                                if (MaximizeProcessWindow(proc))
                                    windowCount++;
                            }
                            catch (Exception ex)
                            {
                                DebugWindow.Instance?.LogMessage($"[MaximizeAll_Click] Fehler bei PID {program?.LastStartedPID}: {ex.Message}");
                            }
                        }

                        // Fallback: Alle Prozesse mit diesem Namen
                        if (windowCount == 0)
                        {
                            var allProcs = Process.GetProcessesByName(status.ProcessName);
                            foreach (var proc in allProcs)
                            {
                                if (MaximizeProcessWindow(proc))
                                    windowCount++;
                            }
                        }

                        totalMaximized += windowCount;
                    }
                }

                StatusText.Text = $"✓ {totalMaximized} Fenster in den Vordergrund gebracht.";
                DebugWindow.Instance?.LogMessage($"[MaximizeAll_Click] Abgeschlossen: {totalMaximized} Fenster maximiert");

                // Bringe TaskPilot selbst in den Vordergrund
                Dispatcher.Invoke(() =>
                {
                    this.Activate();
                    this.Focus();
                    SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                });
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Fehler beim Maximieren: {ex.Message}";
                DebugWindow.Instance?.LogMessage($"[MaximizeAll_Click] Exception: {ex.Message}");
            }
        }
    }
}
