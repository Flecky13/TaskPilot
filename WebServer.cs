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
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
        private static string? _configPath;
        private static bool _httpsActive = false; // Flag ob HTTPS aktiv ist
        private const string JwtSecretKey = "TaskPilot_Default_Secret_Key_For_JWT_Tokens_2024"; // In Produktion von außen setzen

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

        public static void Start(ProcessMonitor monitor, IniConfigReader.ServerSettings serverSettings, string? configPath = null)
        {
            if (_initialized) return;
            _initialized = true;
            _configPath = configPath;

            var builder = WebApplication.CreateBuilder();
            // Binde auf alle Interfaces, damit Zugriff auch übers Netzwerk möglich ist
            builder.WebHost.UseKestrel(options =>
            {
                if (serverSettings.HttpsEnabled)
                {
                    var certificate = LoadCertificateByThumbprint(serverSettings.CertificateThumbprint);
                    if (certificate != null)
                    {
                        if (CanUseCertificatePrivateKey(certificate))
                        {
                            options.ListenAnyIP(serverSettings.Port, listen => listen.UseHttps(certificate));
                            _httpsActive = true;
                            System.Diagnostics.Debug.WriteLine($"[WebServer] HTTPS aktiviert auf Port {serverSettings.Port} (Thumbprint: {certificate.Thumbprint})");
                        }
                        else
                        {
                            options.ListenAnyIP(serverSettings.Port);
                            _httpsActive = false;
                            System.Diagnostics.Debug.WriteLine($"[WebServer] Zertifikat gefunden, aber Zugriff auf privaten Schlüssel nicht möglich. Fallback auf HTTP Port {serverSettings.Port}.");
                        }
                    }
                    else
                    {
                        options.ListenAnyIP(serverSettings.Port);
                        _httpsActive = false;
                        System.Diagnostics.Debug.WriteLine($"[WebServer] HTTPS konfiguriert, aber Zertifikat nicht gefunden. Fallback auf HTTP Port {serverSettings.Port}.");
                    }
                }
                else
                {
                    options.ListenAnyIP(serverSettings.Port);
                    _httpsActive = false;
                    System.Diagnostics.Debug.WriteLine($"[WebServer] HTTPS deaktiviert. HTTP Port {serverSettings.Port} aktiv.");
                }
            });

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

            // HTTP zu HTTPS Umleitung, wenn HTTPS aktiv ist
            if (_httpsActive)
            {
                _app.Use(async (context, next) =>
                {
                    if (!context.Request.IsHttps)
                    {
                        var httpsUrl = $"https://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
                        context.Response.Redirect(httpsUrl, permanent: true);
                        return;
                    }
                    await next();
                });
            }

            _app.UseDefaultFiles();
            _app.UseStaticFiles();

            // Login-Endpunkt (nicht authentifiziert)
            _app.MapPost("/api/auth/login", async (HttpRequest request) =>
            {
                try
                {
                    var body = await request.ReadFromJsonAsync<LoginRequest>();
                    if (body == null || string.IsNullOrWhiteSpace(body.Password))
                        return Results.BadRequest(new { error = "Password erforderlich" });

                    // Passwort aus INI lesen (aus [Server] Sektion)
                    var serverSettings = IniConfigReader.ReadServerSettings(_configPath ?? "programs.ini");
                    if (!string.Equals(body.Password, serverSettings.Password, StringComparison.Ordinal))
                        return Results.Unauthorized();

                    // JWT-Token generieren
                    var token = GenerateJwtToken();
                    return Results.Ok(new { token });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebServer] Login error: {ex.Message}");
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

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

            _app.MapPost("/api/processes/{processName}/start", (string processName, HttpRequest request, ProcessMonitor m) =>
            {
                if (!ValidateToken(request))
                    return Results.Unauthorized();

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

            _app.MapPost("/api/processes/{processName}/stop", (string processName, HttpRequest request, ProcessMonitor m) =>
            {
                if (!ValidateToken(request))
                    return Results.Unauthorized();

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

            _app.MapPost("/api/processes/{processName}/autorestart", (string processName, HttpRequest request, ProcessMonitor m) =>
            {
                if (!ValidateToken(request))
                    return Results.Unauthorized();

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
                    if (!ValidateToken(request))
                        return Results.Unauthorized();

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

            System.Diagnostics.Debug.WriteLine($"[WebServer] Starting on port {serverSettings.Port}...");
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
                _httpsActive = false;
            }
        }

        public static async Task RestartAsync(ProcessMonitor monitor, IniConfigReader.ServerSettings settings, string? configPath = null)
        {
            await StopAsync();
            if (settings.Enabled)
            {
                Start(monitor, settings, configPath);
            }
        }

        private static X509Certificate2? LoadCertificateByThumbprint(string thumbprint)
        {
            if (string.IsNullOrWhiteSpace(thumbprint))
                return null;

            var normalized = thumbprint.Replace(" ", string.Empty).ToUpperInvariant();

            X509Certificate2? FindInStore(StoreLocation location)
            {
                try
                {
                    using var store = new X509Store(StoreName.My, location);
                    store.Open(OpenFlags.ReadOnly);
                    var matches = store.Certificates
                        .Find(X509FindType.FindByThumbprint, normalized, validOnly: false)
                        .OfType<X509Certificate2>()
                        .ToList();

                    var withKey = matches.FirstOrDefault(c => c.HasPrivateKey && HasServerAuthenticationEku(c));
                    if (withKey == null && matches.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebServer] Zertifikat mit Thumbprint gefunden, aber kein privater Schlüssel oder fehlende ServerAuth EKU (Store={location}).");
                    }
                    return withKey;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebServer] Zertifikatssuche in {location} fehlgeschlagen: {ex.Message}");
                    return null;
                }
            }

            var certificate = FindInStore(StoreLocation.CurrentUser) ?? FindInStore(StoreLocation.LocalMachine);

            if (certificate == null)
            {
                System.Diagnostics.Debug.WriteLine($"[WebServer] Kein Zertifikat mit Thumbprint {normalized} gefunden.");
            }

            return certificate;
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

        private static bool CanUseCertificatePrivateKey(X509Certificate2 cert)
        {
            try
            {
                // Versuche RSA
                using var rsa = cert.GetRSAPrivateKey();
                if (rsa != null)
                {
                    var data = new byte[] { 1, 2, 3, 4 };
                    var sig = rsa.SignData(data, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
                    return sig != null && sig.Length > 0;
                }

                // Versuche ECDSA
                using var ecdsa = cert.GetECDsaPrivateKey();
                if (ecdsa != null)
                {
                    var data = new byte[] { 1, 2, 3, 4 };
                    var sig = ecdsa.SignData(data, System.Security.Cryptography.HashAlgorithmName.SHA256);
                    return sig != null && sig.Length > 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebServer] Private-Key-Test fehlgeschlagen: {ex.Message}");
                return false;
            }
        }

        private static string GenerateJwtToken()
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "Dashboard"),
                new Claim("isAuthenticated", "true")
            };

            var token = new JwtSecurityToken(
                issuer: "TaskPilot",
                audience: "TaskPilot-Dashboard",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static bool ValidateToken(HttpRequest request)
        {
            try
            {
                if (!request.Headers.TryGetValue("Authorization", out var authHeader))
                    return false;

                var token = authHeader.ToString().Replace("Bearer ", "");
                if (string.IsNullOrWhiteSpace(token))
                    return false;

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecretKey));
                var tokenHandler = new JwtSecurityTokenHandler();

                var validationParams = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                tokenHandler.ValidateToken(token, validationParams, out _);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebServer] Token validation error: {ex.Message}");
                return false;
            }
        }

        private class LoginRequest
        {
            public string? Password { get; set; }
        }

        private class AutoStartRequest
        {
            public bool Enabled { get; set; }
        }
    }
}
