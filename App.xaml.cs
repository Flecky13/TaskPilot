using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace TaskPilot
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
            if (_notifyIcon != null)
            {
                _notifyIcon.TrayMouseDoubleClick += NotifyIcon_TrayMouseDoubleClick;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }

        private void NotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (Current.MainWindow != null)
            {
                Current.MainWindow.Show();
                Current.MainWindow.WindowState = WindowState.Normal;
                Current.MainWindow.Activate();
            }
        }

        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            if (Current.MainWindow != null)
            {
                Current.MainWindow.Show();
                Current.MainWindow.WindowState = WindowState.Normal;
                Current.MainWindow.Activate();
            }
        }

        private void ReloadConfig_Click(object sender, RoutedEventArgs e)
        {
            if (Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ReloadConfiguration();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Current.Shutdown();
        }
    }
}
