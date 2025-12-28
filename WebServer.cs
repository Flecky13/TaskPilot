using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace TaskPilot
{
    public static class WebServer
    {
        private static WebApplication? _app;
        private static IHubContext<StatusHub>? _hubContext;
        private static bool _initialized;
        private static Timer? _snapshotTimer;
        private static bool _hubContextReady = false; // Flag um zu überprüfen ob HubContext bereit ist
        private static Task? _runTask;

        public static void NotifyAutoStartChanged(bool enabled)
        {
            try
            {
                if (_hubContext != null && _hubContextReady)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _hubContext.Clients.All.SendAsync("settingsChanged", new { autoStartEnabled = enabled });
                            System.Diagnostics.Debug.WriteLine($"[WebServer] settingsChanged broadcast: autoStartEnabled={enabled}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WebServer] Error broadcasting settingsChanged: {ex.Message}");
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WebServer] NotifyAutoStartChanged: HubContext not ready (_hubContext={_hubContext != null}, ready={_hubContextReady})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebServer] NotifyAutoStartChanged exception: {ex.Message}");
            }
        }

        public static void Start(ProcessMonitor monitor, int port = 5110)
        {
            if (_initialized) return;
            _initialized = true;

            var builder = WebApplication.CreateBuilder();
            // Binde auf alle Interfaces, damit Zugriff auch übers Netzwerk möglich ist
            builder.WebHost.UseKestrel().UseUrls($"http://0.0.0.0:{port}");

            builder.Services.AddSingleton(monitor);
            builder.Services.AddSignalR();
            builder.Services.AddCors(options =>
            {
                // Für LAN-Zugriff: Alle Origins zulassen (keine Credentials genutzt)
                options.AddDefaultPolicy(policy =>
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod());
            });

            _app = builder.Build();
            _app.UseCors();
            _app.UseDefaultFiles();
            _app.UseStaticFiles();

            _app.MapGet("/api/processes", (ProcessMonitor m) =>
            {
                // Hole MainWindow für Zugriff auf _currentPrograms
                MonitoredProgram? FindProgram(string processName)
                {
                    MonitoredProgram? result = null;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                        result = mainWindow?.GetProgramByProcessName(processName);
                    });
                    return result;
                }

                var data = m.GetStatuses().Select(s =>
                {
                    var program = FindProgram(s.ProcessName);
                    return new
                    {
                        processName = s.ProcessName,
                        displayName = s.DisplayName,
                        isActive = s.IsActive,
                        processId = s.ProcessID,
                        statusSince = s.StatusSince,
                        statusText = s.StatusText,
                        autoRestart = program?.AutoRestart ?? false,
                        hasStartCommand = !string.IsNullOrWhiteSpace(program?.StartCommand)
                    };
                });
                return Results.Ok(data);
            });

            _app.MapGet("/api/debug/signalr", () =>
            {
                return Results.Ok(new
                {
                    hubContextAvailable = _hubContext != null,
                    serverTime = DateTime.Now,
                    message = "SignalR Hub Status"
                });
            });

            _app.MapPost("/api/processes/{processName}/start", (string processName, ProcessMonitor m) =>
            {
                System.Diagnostics.Debug.WriteLine($"[WebServer] POST /start: {processName}");
                var statuses = m.GetStatuses().ToList();
                var status = statuses.FirstOrDefault(s => s.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));

                if (status == null)
                    return Results.NotFound(new { error = "Prozess nicht gefunden" });

                if (status.IsActive)
                    return Results.BadRequest(new { error = "Prozess läuft bereits" });

                // Trigger manual start via MainWindow
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                    mainWindow?.StartProcessManually(processName);
                });

                return Results.Ok(new { message = "Start-Befehl gesendet" });
            });

            _app.MapPost("/api/processes/{processName}/stop", (string processName, ProcessMonitor m) =>
            {
                System.Diagnostics.Debug.WriteLine($"[WebServer] POST /stop: {processName}");
                var statuses = m.GetStatuses().ToList();
                var status = statuses.FirstOrDefault(s => s.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));

                if (status == null)
                    return Results.NotFound(new { error = "Prozess nicht gefunden" });

                if (!status.IsActive)
                    return Results.BadRequest(new { error = "Prozess läuft nicht" });

                // Stop process
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                    mainWindow?.StopProcessManually(processName);
                });

                return Results.Ok(new { message = "Stop-Befehl gesendet" });
            });

            _app.MapPost("/api/processes/{processName}/autorestart", (string processName, ProcessMonitor m) =>
            {
                System.Diagnostics.Debug.WriteLine($"[WebServer] POST /autorestart: {processName}");

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                    var toggled = mainWindow?.ToggleAutoRestart(processName);

                    if (toggled.HasValue)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebServer] AutoRestart toggled to: {toggled.Value}");
                    }
                });

                return Results.Ok(new { message = "AutoRestart umgeschaltet" });
            });

                _app.MapGet("/api/settings/autostart", () =>
                {
                    bool enabled = false;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                        enabled = mainWindow?.GetGlobalAutoStartEnabled() ?? false;
                    });

                    return Results.Ok(new { enabled });
                });

                _app.MapPost("/api/settings/autostart", async (HttpRequest request) =>
                {
                    var body = await request.ReadFromJsonAsync<AutoStartRequest>();
                    if (body == null)
                        return Results.BadRequest(new { error = "Invalid request" });

                    System.Diagnostics.Debug.WriteLine($"[WebServer] POST /autostart: {body.Enabled}");

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                        mainWindow?.SetGlobalAutoStartEnabled(body.Enabled);
                        System.Diagnostics.Debug.WriteLine($"[WebServer] SetGlobalAutoStartEnabled called with {body.Enabled}");
                    });

                    // SetGlobalAutoStartEnabled ruft bereits NotifyAutoStartChanged auf
                    System.Diagnostics.Debug.WriteLine($"[WebServer] POST /autostart response ready");

                    return Results.Ok(new { message = "AutoStart gesetzt", enabled = body.Enabled });
                });

            _app.MapHub<StatusHub>("/hub/status");

            // Nach Build HubContext holen und auf Events reagieren
            _app.Lifetime.ApplicationStarted.Register(() =>
            {
                System.Diagnostics.Debug.WriteLine("[WebServer] ApplicationStarted - Setting up SignalR");
                _hubContext = _app!.Services.GetRequiredService<IHubContext<StatusHub>>();
                _hubContextReady = true; // Markiere dass HubContext bereit ist
                System.Diagnostics.Debug.WriteLine("[WebServer] HubContext obtained successfully");

                monitor.StatusChanged += async (_, status) =>
                {
                    try
                    {
                        if (_hubContext != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WebServer] Broadcasting statusChanged: {status.DisplayName} -> {status.StatusText}");
                            await _hubContext.Clients.All.SendAsync("statusChanged", new
                            {
                                processName = status.ProcessName,
                                displayName = status.DisplayName,
                                isActive = status.IsActive,
                                processId = status.ProcessID,
                                statusSince = status.StatusSince,
                                statusText = status.StatusText
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebServer] Error in StatusChanged handler: {ex.Message}");
                    }
                };

                // Periodischer Snapshot alle 5 Sekunden, damit das Dashboard regelmäßig aktualisiert
                System.Diagnostics.Debug.WriteLine("[WebServer] Starting snapshot timer...");
                _snapshotTimer = new Timer(_ =>
                {
                    try
                    {
                        if (_hubContext != null)
                        {
                            var data = monitor.GetStatuses().Select(s => new
                            {
                                processName = s.ProcessName,
                                displayName = s.DisplayName,
                                isActive = s.IsActive,
                                processId = s.ProcessID,
                                statusSince = s.StatusSince,
                                statusText = s.StatusText
                            }).ToList();

                            System.Diagnostics.Debug.WriteLine($"[WebServer] Broadcasting statusSnapshot: {data.Count} processes");

                            // Fire-and-forget but log errors
                            Task.Run(async () =>
                            {
                                try
                                {
                                    await _hubContext.Clients.All.SendAsync("statusSnapshot", data);
                                    System.Diagnostics.Debug.WriteLine($"[WebServer] statusSnapshot sent successfully");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[WebServer] Error sending snapshot: {ex.Message}");
                                    System.Diagnostics.Debug.WriteLine($"[WebServer] Stack: {ex.StackTrace}");
                                }
                            });
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[WebServer] HubContext is null in timer!");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebServer] Error in snapshot timer: {ex.Message}");
                    }
                }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));

                System.Diagnostics.Debug.WriteLine("[WebServer] Setup complete");
            });

            System.Diagnostics.Debug.WriteLine($"[WebServer] Starting on port {port}...");
            _runTask = _app.RunAsync();
        }

        public static async Task StopAsync()
        {
            try
            {
                _snapshotTimer?.Dispose();
                _snapshotTimer = null;

                if (_app != null)
                {
                    await _app.StopAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebServer] StopAsync error: {ex.Message}");
            }
            finally
            {
                _app = null;
                _hubContext = null;
                _hubContextReady = false;
                _initialized = false;
                _runTask = null;
            }
        }

        public static async Task RestartAsync(ProcessMonitor monitor, int port, bool enabled)
        {
            await StopAsync();
            if (enabled)
            {
                Start(monitor, port);
            }
        }

        private class AutoStartRequest
        {
            public bool Enabled { get; set; }
        }
    }
}
