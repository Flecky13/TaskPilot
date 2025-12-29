using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;

namespace TaskPilot
{
    public partial class ConfigurationWindow : Window
    {
        private ConfigurationWindowViewModel? _viewModel;
        private string _configFilePath;
        private IniConfigReader.ServerSettings _serverSettings;

        public ConfigurationWindow(string configFilePath, List<MonitoredProgram> monitoredPrograms, IniConfigReader.ServerSettings serverSettings)
        {
            InitializeComponent();

            _configFilePath = configFilePath;
            _serverSettings = serverSettings;
            _viewModel = new ConfigurationWindowViewModel(monitoredPrograms, _serverSettings);
            DataContext = _viewModel;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            // Neu laden der INI
            _viewModel?.RefreshProcesses();
            DialogHelper.ShowConfigurationReloaded();
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            var result = DialogHelper.AskRemoveAllProcesses();
            if (result == MessageBoxResult.Yes)
            {
                _viewModel?.DeselectAll();
            }
        }

        private void SetAllMonitored_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel?.AvailableProcesses == null)
                    return;

                // Setze alle IsSelected zu true
                foreach (var process in _viewModel.AvailableProcesses)
                {
                    process.IsSelected = true;
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Setzen aller Überwachen-Checkboxen", ex.Message);
            }
        }

        private void SetAllAutostart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel?.AvailableProcesses == null)
                    return;

                // Validiere zuerst: Alle Prozesse mit AutoRestart müssen einen StartCommand haben
                var processesWithoutCommand = _viewModel.AvailableProcesses
                    .Where(p => string.IsNullOrWhiteSpace(p.StartCommand))
                    .ToList();

                if (processesWithoutCommand.Count > 0)
                {
                    var programList = string.Join("\n• ", processesWithoutCommand.Select(p => p.DisplayName));
                    DialogHelper.ShowValidationError(
                        $"Folgende Programme haben keinen Startbefehl definiert:\n\n• {programList}\n\n" +
                        $"Bitte definieren Sie zuerst Startbefehle für diese Programme.");
                    return;
                }

                // Setze alle AutoRestart zu true
                foreach (var process in _viewModel.AvailableProcesses)
                {
                    process.AutoRestart = true;
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Setzen aller AutoStart-Checkboxen", ex.Message);
            }
        }

        private void ClearAllMonitored_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel?.AvailableProcesses == null)
                    return;

                // Deaktiviere alle IsSelected
                foreach (var process in _viewModel.AvailableProcesses)
                {
                    process.IsSelected = false;
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Deaktivieren aller Überwachen-Checkboxen", ex.Message);
            }
        }

        private void ClearAllAutostart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel?.AvailableProcesses == null)
                    return;

                // Deaktiviere alle AutoRestart
                foreach (var process in _viewModel.AvailableProcesses)
                {
                    process.AutoRestart = false;
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Deaktivieren aller AutoStart-Checkboxen", ex.Message);
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
                    _serverSettings = IniConfigReader.ReadServerSettings(_configFilePath);
                    _viewModel = new ConfigurationWindowViewModel(updatedPrograms, _serverSettings);
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
                    _serverSettings = IniConfigReader.ReadServerSettings(_configFilePath);
                    _viewModel = new ConfigurationWindowViewModel(updatedPrograms, _serverSettings);
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
                        var currentSettings = _viewModel?.GetServerSettings() ?? _serverSettings;
                        IniConfigReader.SaveConfiguration(_configFilePath, allPrograms, currentSettings);

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

                var serverSettings = _viewModel?.GetServerSettings() ?? _serverSettings;

                // Validierung Server-Port
                if (serverSettings.Port <= 0 || serverSettings.Port > 65535)
                {
                    DialogHelper.ShowValidationError("Bitte einen gültigen Port zwischen 1 und 65535 eingeben.");
                    return;
                }

                if (serverSettings.HttpsEnabled && string.IsNullOrWhiteSpace(serverSettings.CertificateThumbprint))
                {
                    DialogHelper.ShowValidationError("Bitte einen Zertifikat-Thumbprint angeben oder HTTPS deaktivieren.");
                    return;
                }

                // Speichere ALLE Programme + Server-Einstellungen (inkl. Passwort) in die INI
                IniConfigReader.SaveConfiguration(_configFilePath, allPrograms, serverSettings);

                // Zähle nur die überwachten für die Meldung
                var monitoredCount = allPrograms.Count(p => p.IsSelected);
                DialogHelper.ShowConfigurationSaved(monitoredCount);

                // Main-Window sofort aktualisieren, damit Änderungen direkt sichtbar sind
                if (Owner is MainWindow mainWindow)
                {
                    mainWindow.ApplyServerSettings(serverSettings);
                    mainWindow.ReloadConfiguration();
                }

                // Fenster bleibt offen - nicht schließen!
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Speichern der Konfiguration", ex.Message);
            }
        }

        private void SelectCertificate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var collection = new X509Certificate2Collection();

                void AddFromStore(StoreLocation location)
                {
                    try
                    {
                        using var store = new X509Store(StoreName.My, location);
                        store.Open(OpenFlags.ReadOnly);
                        collection.AddRange(store.Certificates);
                    }
                    catch (Exception ex)
                    {
                        DialogHelper.ShowOperationError($"Zugriff auf Zertifikatsspeicher {location}", ex.Message);
                    }
                }

                AddFromStore(StoreLocation.CurrentUser);
                AddFromStore(StoreLocation.LocalMachine);

                // Nur Zertifikate mit privatem Schlüssel und Server Auth EKU anzeigen
                var filtered = new X509Certificate2Collection();
                foreach (var cert in collection.OfType<X509Certificate2>())
                {
                    if (cert.HasPrivateKey && HasServerAuthenticationEku(cert))
                    {
                        filtered.Add(cert);
                    }
                }

                if (filtered.Count == 0)
                {
                    DialogHelper.ShowValidationError("Kein Zertifikat im Windows Store gefunden.");
                    return;
                }

                var selection = X509Certificate2UI.SelectFromCollection(
                    filtered,
                    "Zertifikat auswählen",
                    "Wählen Sie ein Zertifikat mit privatem Schlüssel für HTTPS",
                    X509SelectionFlag.SingleSelection);

                if (selection.Count > 0)
                {
                    var cert = selection[0];
                    var thumb = cert.Thumbprint?.Replace(" ", string.Empty) ?? string.Empty;
                    if (_viewModel != null)
                    {
                        _viewModel.CertificateThumbprint = thumb;
                        _viewModel.HttpsEnabled = true;
                        // Store- und Name-Anzeige aktualisieren
                        var (foundCert, storeText) = CertificateLookup.FindByThumbprintWithStore(thumb);
                        var displayName = !string.IsNullOrWhiteSpace(foundCert?.FriendlyName)
                            ? foundCert!.FriendlyName
                            : CertificateLookup.ExtractCn(foundCert?.Subject ?? cert.Subject);
                        _viewModel.CertificateDisplayLabel = $"{storeText} — {displayName}";
                    }
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowOperationError("Zertifikat auswählen", ex.Message);
            }
        }

        private static class CertificateLookup
        {
            public static (X509Certificate2? cert, string storeText) FindByThumbprintWithStore(string thumbprint)
            {
                var normalized = (thumbprint ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
                var cert = FindInStore(StoreLocation.CurrentUser, out var storeTextCU);
                if (cert != null) return (cert, storeTextCU);
                cert = FindInStore(StoreLocation.LocalMachine, out var storeTextLM);
                if (cert != null) return (cert, storeTextLM);
                return (null, string.Empty);

                X509Certificate2? FindInStore(StoreLocation location, out string storeText)
                {
                    storeText = location == StoreLocation.CurrentUser ? "CurrentUser\\My" : "LocalMachine\\My";
                    try
                    {
                        using var store = new X509Store(StoreName.My, location);
                        store.Open(OpenFlags.ReadOnly);
                        var match = store.Certificates
                            .Find(X509FindType.FindByThumbprint, normalized, validOnly: false)
                            .OfType<X509Certificate2>()
                            .FirstOrDefault();
                        return match;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            public static string ExtractCn(string subject)
            {
                if (string.IsNullOrWhiteSpace(subject)) return string.Empty;
                // Subject wie "CN=example.com, O=..." → CN extrahieren
                foreach (var part in subject.Split(','))
                {
                    var p = part.Trim();
                    if (p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                    {
                        return p.Substring(3).Trim();
                    }
                }
                return subject;
            }
        }

        private static bool HasServerAuthenticationEku(X509Certificate2 cert)
        {
            try
            {
                foreach (var ext in cert.Extensions)
                {
                    if (ext is X509EnhancedKeyUsageExtension eku)
                    {
                        foreach (var oid in eku.EnhancedKeyUsages)
                        {
                            if (oid?.Value == "1.3.6.1.5.5.7.3.1") // Server Authentication
                                return true;
                        }
                        return false; // EKU vorhanden, aber kein Server Auth
                    }
                }
                // Keine EKU-Erweiterung vorhanden → meist alle Zwecke erlaubt
                return true;
            }
            catch
            {
                return false;
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
