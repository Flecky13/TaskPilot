using System;
using System.Windows;

namespace TaskPilot
{
    /// <summary>
    /// Utility-Klasse für zentrale Dialog- und MessageBox-Verwaltung
    /// </summary>
    public static class DialogHelper
    {
        public static void ShowInfo(string message, string title = "Information")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void ShowWarning(string message, string title = "Warnung")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void ShowError(string message, string title = "Fehler")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static MessageBoxResult ShowConfirm(string message, string title = "Bestätigung")
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        }

        public static void ShowConfigurationLoaded(int count)
        {
            ShowInfo($"Konfiguration geladen: {count} Programme");
        }

        public static void ShowConfigurationReloaded()
        {
            ShowInfo("Konfiguration neu geladen.");
        }

        public static void ShowNewProcessesLoaded()
        {
            ShowInfo("Neue Prozesse geladen.");
        }

        public static void ShowConfigurationSaved(int count)
        {
            ShowInfo($"Konfiguration erfolgreich gespeichert.\n{count} Programme werden überwacht.");
        }

        public static void ShowProcessesAdded(int count)
        {
            ShowInfo($"Konfiguration erfolgreich gespeichert.\n{count} Prozesse hinzugefügt.");
        }

        public static void ShowProcessListUpdated()
        {
            ShowInfo("Prozessliste aktualisiert.");
        }

        public static void ShowStartCommandsSaved()
        {
            ShowInfo("Startbefehle gespeichert.");
        }

        public static void ShowSelectAtLeastOneProcess()
        {
            ShowWarning("Bitte wählen Sie mindestens einen Prozess aus.");
        }

        public static void ShowValidationError(string details)
        {
            ShowWarning($"Validierungsfehler\n\n{details}");
        }

        public static void ShowConfigurationError(string message)
        {
            ShowError($"Fehler beim Laden der Konfiguration:\n{message}");
        }

        public static void ShowOperationError(string operation, string message)
        {
            ShowError($"Fehler beim {operation}:\n{message}");
        }

        public static void ShowOperationError(string operation, Exception ex)
        {
            ShowError($"Fehler beim {operation}:\n{ex.Message}");
        }

        public static MessageBoxResult AskRemoveAllProcesses()
        {
            return ShowConfirm("Sind Sie sicher, dass Sie alle Prozesse entfernen möchten?");
        }
    }
}
