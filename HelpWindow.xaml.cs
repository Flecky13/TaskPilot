using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.IO;

namespace TaskPilot
{
    public partial class HelpWindow : Window
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private string? latestDownloadUrl;
        private string? latestTagName;

        public HelpWindow()
        {
            InitializeComponent();
            LoadCurrentVersion();
        }

        private void LoadCurrentVersion()
        {
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                CurrentVersionText.Text = version != null ? version.ToString() : "unbekannt";
            }
            catch
            {
                CurrentVersionText.Text = "unbekannt";
            }
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusText.Text = "Prüfe auf Updates...";
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/Flecky13/TaskPilot/releases/latest");
                request.Headers.UserAgent.ParseAdd("TaskPilot/1.0");
                request.Headers.Accept.ParseAdd("application/vnd.github+json");

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                var response = await httpClient.SendAsync(request, cts.Token);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(cts.Token);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                latestTagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
                var htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : "https://github.com/Flecky13/TaskPilot/releases/latest";

                latestDownloadUrl = null;
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                        var dl = asset.TryGetProperty("browser_download_url", out var d) ? d.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(dl))
                        {
                            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                latestDownloadUrl = dl;
                                break;
                            }
                        }
                    }
                }

                var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var localVerString = localVersion != null ? localVersion.ToString() : null;

                if (string.IsNullOrWhiteSpace(latestTagName))
                {
                    UpdateStatusText.Text = "Konnte neueste Version nicht ermitteln.";
                    InstallNowButton.IsEnabled = false;
                    return;
                }

                var latest = latestTagName!.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? latestTagName.Substring(1) : latestTagName;

                bool updateAvailable = false;
                if (!string.IsNullOrWhiteSpace(localVerString))
                {
                    if (Version.TryParse(latest, out var latestVer) && Version.TryParse(localVerString, out var localVer))
                    {
                        updateAvailable = latestVer > localVer;
                    }
                    else
                    {
                        updateAvailable = !string.Equals(latest, localVerString, StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (updateAvailable)
                {
                    UpdateStatusText.Text = $"Update verfügbar: {latestTagName}";
                    InstallNowButton.IsEnabled = !string.IsNullOrWhiteSpace(latestDownloadUrl);
                }
                else
                {
                    UpdateStatusText.Text = "Aktuell";
                    InstallNowButton.IsEnabled = false;
                }
            }
            catch (TaskCanceledException)
            {
                UpdateStatusText.Text = "Zeitüberschreitung beim Update-Check.";
                InstallNowButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = "Fehler beim Update-Check.";
                MessageBox.Show($"Fehler beim Update-Check: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                InstallNowButton.IsEnabled = false;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenReleases_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "https://github.com/Flecky13/TaskPilot/releases/latest",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Konnte die Release-Seite nicht öffnen: {ex.Message}", "Releases öffnen", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void InstallNow_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(latestDownloadUrl))
            {
                MessageBox.Show("Kein Download-Link gefunden. Öffne Releases-Seite.", "Update", MessageBoxButton.OK, MessageBoxImage.Information);
                OpenReleases_Click(sender, e);
                return;
            }

            try
            {
                var tag = latestTagName ?? "latest";
                if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase)) tag = tag.Substring(1);

                var tempDir = Path.GetTempPath();
                var fileName = $"TaskPilot-Setup-{tag}.exe";
                var filePath = Path.Combine(tempDir, fileName);

                UpdateStatusText.Text = "Lade Installer herunter...";
                using (var resp = await httpClient.GetAsync(latestDownloadUrl))
                {
                    resp.EnsureSuccessStatusCode();
                    await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await resp.Content.CopyToAsync(fs);
                }

                UpdateStatusText.Text = "Starte Installer und beende App...";

                // Start Installer in neuer Shell, damit er unabhängig vom aktuellen Prozess läuft
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(startInfo);

                // App beenden
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Herunterladen/Starten des Installers: {ex.Message}", "Update", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
