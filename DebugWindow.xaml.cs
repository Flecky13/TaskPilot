using System;
using System.Windows;
using System.Windows.Controls;

namespace TaskPilot
{
    public partial class DebugWindow : Window
    {
        public static DebugWindow? Instance { get; private set; }

        public DebugWindow()
        {
            InitializeComponent();
            Instance = this;

            LogMessage("Debug-Konsole gestartet...");

            this.Closing += (s, e) =>
            {
                Instance = null;
            };
        }

        public void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
