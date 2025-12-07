using System;
using System.Windows;

namespace TaskPilot
{
    public partial class AddSingleProcessWindow : Window
    {
        public string ProcessName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string StartCommand { get; set; } = string.Empty;
        public bool AutoRestart { get; set; } = false;
        public bool IsSelected { get; set; } = true;

        private string _originalProcessName = string.Empty;

        public AddSingleProcessWindow()
        {
            InitializeComponent();
        }

        public AddSingleProcessWindow(MonitoredProgram program) : this()
        {
            _originalProcessName = program.ProcessName;

            // Felder vorausfüllen
            ProcessNameTextBox.Text = program.ProcessName;
            DisplayNameTextBox.Text = program.DisplayName;
            DescriptionTextBox.Text = program.Description ?? string.Empty;
            StartCommandTextBox.Text = program.StartCommand ?? string.Empty;
            AutoRestartCheckBox.IsChecked = program.AutoRestart;
            IsSelectedCheckBox.IsChecked = program.IsSelected;

            // UI anpassen
            Title = "Prozess bearbeiten";
            TitleTextBlock.Text = "Prozess bearbeiten";
            AddButton.Content = "Speichern";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            // Validierung
            var processName = ProcessNameTextBox.Text?.Trim() ?? string.Empty;
            var displayName = DisplayNameTextBox.Text?.Trim() ?? string.Empty;
            var description = DescriptionTextBox.Text?.Trim() ?? string.Empty;
            var startCommand = StartCommandTextBox.Text?.Trim() ?? string.Empty;
            var autoRestart = AutoRestartCheckBox.IsChecked == true;
            var isSelected = IsSelectedCheckBox.IsChecked != false; // default true

            if (string.IsNullOrWhiteSpace(processName))
            {
                DialogHelper.ShowValidationError("Prozessname ist erforderlich.");
                ProcessNameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                DialogHelper.ShowValidationError("Anzeigename ist erforderlich.");
                DisplayNameTextBox.Focus();
                return;
            }

            // Prozessname bereinigen (keine .exe, Leerzeichen trimmen)
            processName = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase).Trim();

            if (processName.Contains(" "))
            {
                DialogHelper.ShowValidationError("Prozessname darf keine Leerzeichen enthalten.");
                ProcessNameTextBox.Focus();
                return;
            }

            if (processName.Length > 255)
            {
                DialogHelper.ShowValidationError("Prozessname ist zu lang (max. 255 Zeichen).");
                return;
            }

            if (displayName.Length > 255)
            {
                DialogHelper.ShowValidationError("Anzeigename ist zu lang (max. 255 Zeichen).");
                return;
            }

            if (autoRestart && string.IsNullOrWhiteSpace(startCommand))
            {
                DialogHelper.ShowValidationError("Für Auto-Restart muss ein Startbefehl definiert sein.");
                StartCommandTextBox.Focus();
                return;
            }

            if (startCommand.Length > 2000)
            {
                DialogHelper.ShowValidationError("Startbefehl ist zu lang (max. 2000 Zeichen).");
                return;
            }

            ProcessName = processName;
            DisplayName = displayName;
            Description = description;
            StartCommand = startCommand;
            AutoRestart = autoRestart;
            IsSelected = isSelected;

            DialogResult = true;
            Close();
        }
    }
}
