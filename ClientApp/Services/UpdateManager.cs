using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ClientApp.Services
{
    public class UpdateInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string UpdateType { get; set; } = "minor"; // "minor" or "major"
        public bool IsCompulsory { get; set; } = false;
        public string Changelog { get; set; } = string.Empty;
        public double PaymentAmount { get; set; } = 0.00;
        public string FileUrl { get; set; } = string.Empty;
    }

    public class UpdateManager
    {
        public static string CurrentVersion => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        public static UpdateManager Instance { get; } = new UpdateManager();

        private readonly HttpClient _http;
        private const string SupabaseUrl = "https://qcmcoofnxqzyrrbcwdde.supabase.co";
        private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InFjbWNvb2ZueHF6eXJyYmN3ZGRlIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Nzg1NTE0MDYsImV4cCI6MjA5NDEyNzQwNn0.BDse0v5cLXNT9wK9K7bOkLOkyZhPJL9HpZQuFA5fixY";

        public bool IsDownloading { get; private set; } = false;
        public double DownloadProgress { get; private set; } = 0.0;
        public UpdateInfo? LatestDetectedUpdate { get; private set; }

        public event Action<double>? DownloadProgressChanged;
        public event Action<double, long, long>? DownloadProgressDetailsChanged; // %, bytesRead, totalBytes
        public event Action<string>? DownloadCompleted;
        public event Action<string>? DownloadFailed;
        public event Action<UpdateInfo>? LiveUpdateDetected;

        private DispatcherTimer? _periodicCheckTimer;

        private UpdateManager()
        {
            _http = new HttpClient { BaseAddress = new Uri(SupabaseUrl) };
            _http.DefaultRequestHeaders.Add("apikey", SupabaseKey);
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseKey}");
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                var response = await _http.GetAsync("/rest/v1/app_updates?select=*&order=created_at.desc&limit=1");
                if (!response.IsSuccessStatusCode) return null;

                var updates = await response.Content.ReadFromJsonAsync<JsonElement[]>();
                if (updates == null || updates.Length == 0) return null;

                var root = updates[0];
                string serverVersionStr = root.GetProperty("version").GetString() ?? "1.0.0";

                Version localVersion = Version.Parse(CurrentVersion);
                Version serverVersion = Version.Parse(serverVersionStr);

                if (serverVersion > localVersion)
                {
                    double amount = 0.00;
                    if (root.TryGetProperty("payment_amount", out JsonElement amtProp) && amtProp.ValueKind != JsonValueKind.Null)
                    {
                        if (amtProp.ValueKind == JsonValueKind.Number) amount = amtProp.GetDouble();
                        else if (double.TryParse(amtProp.GetString(), out double parsed)) amount = parsed;
                    }

                    var info = new UpdateInfo
                    {
                        Id = root.GetProperty("id").GetString() ?? string.Empty,
                        Version = serverVersionStr,
                        UpdateType = root.GetProperty("update_type").GetString() ?? "minor",
                        IsCompulsory = root.GetProperty("is_compulsory").GetBoolean(),
                        Changelog = root.GetProperty("changelog").GetString() ?? string.Empty,
                        PaymentAmount = amount,
                        FileUrl = root.GetProperty("file_url").GetString() ?? string.Empty
                    };

                    LatestDetectedUpdate = info;
                    return info;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckForUpdatesAsync failed: {ex.Message}");
            }
            return null;
        }

        public void StartPeriodicCheck(int intervalSeconds = 60)
        {
            if (_periodicCheckTimer != null) return;

            _periodicCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(intervalSeconds)
            };

            _periodicCheckTimer.Tick += async (s, e) =>
            {
                if (IsDownloading) return;
                var update = await CheckForUpdatesAsync();
                if (update != null)
                {
                    LiveUpdateDetected?.Invoke(update);
                }
            };

            _periodicCheckTimer.Start();
        }

        private System.Threading.CancellationTokenSource? _downloadCts;

        public void CancelDownload()
        {
            if (IsDownloading && _downloadCts != null)
            {
                _downloadCts.Cancel();
                IsDownloading = false;
                DownloadFailed?.Invoke("Download cancelled by user.");
            }
        }

        public async Task StartDownloadAsync(UpdateInfo update)
        {
            if (IsDownloading) return;
            IsDownloading = true;
            DownloadProgress = 0.0;
            DownloadProgressChanged?.Invoke(0.0);
            DownloadProgressDetailsChanged?.Invoke(0.0, 0, 0);

            _downloadCts = new System.Threading.CancellationTokenSource();

            try
            {
                bool isMock = string.IsNullOrWhiteSpace(update.FileUrl) || 
                             update.FileUrl.Contains("example.com") || 
                             update.FileUrl.Contains("mock") || 
                             !update.FileUrl.StartsWith("http");

                if (isMock)
                {
                    long simulatedTotal = 35 * 1024 * 1024;
                    for (int i = 1; i <= 100; i++)
                    {
                        _downloadCts.Token.ThrowIfCancellationRequested();
                        await Task.Delay(40, _downloadCts.Token);
                        DownloadProgress = i;
                        long read = (long)(simulatedTotal * (i / 100.0));
                        DownloadProgressChanged?.Invoke(DownloadProgress);
                        DownloadProgressDetailsChanged?.Invoke(DownloadProgress, read, simulatedTotal);
                    }
                }
                else
                {
                    using (var downloadClient = new HttpClient())
                    {
                        downloadClient.Timeout = TimeSpan.FromMinutes(15);
                        using (var response = await downloadClient.GetAsync(update.FileUrl, HttpCompletionOption.ResponseHeadersRead, _downloadCts.Token))
                        {
                            response.EnsureSuccessStatusCode();
                            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                            var contentStream = await response.Content.ReadAsStreamAsync(_downloadCts.Token);
                            
                            var tempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp", "updates");
                            Directory.CreateDirectory(tempPath);
                            
                            var tempFile = Path.Combine(tempPath, $"update_v{update.Version}.exe");

                            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
                            {
                                var buffer = new byte[16384];
                                var totalRead = 0L;
                                var bytesRead = 0;
                                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, _downloadCts.Token)) != 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead, _downloadCts.Token);
                                    totalRead += bytesRead;
                                    if (totalBytes > 0)
                                    {
                                        DownloadProgress = Math.Round((double)totalRead / totalBytes * 100, 1);
                                        DownloadProgressChanged?.Invoke(DownloadProgress);
                                        DownloadProgressDetailsChanged?.Invoke(DownloadProgress, totalRead, totalBytes);
                                    }
                                }
                            }
                        }
                    }
                }

                // Save update package metadata in settings
                SettingsManager.Default.IsUpdateReady = true;
                SettingsManager.Default.UpdateReadyVersion = update.Version;
                SettingsManager.Default.UpdateReadyChangelog = update.Changelog;
                SettingsManager.Default.UpdateReadyType = update.UpdateType;
                SettingsManager.Default.UpdateReadyPaymentAmount = update.PaymentAmount;
                SettingsManager.Default.UpdateReadyFileUrl = update.FileUrl;
                SettingsManager.Default.UpdateReadyCompulsory = update.IsCompulsory;
                SettingsManager.Save();

                IsDownloading = false;
                DownloadCompleted?.Invoke(update.Version);

                // Auto-Install for compulsory updates
                if (update.IsCompulsory)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateNotificationWindow.InstallAndRestart(update);
                    });
                }
            }
            catch (OperationCanceledException)
            {
                IsDownloading = false;
                DownloadFailed?.Invoke("Download cancelled.");
            }
            catch (Exception ex)
            {
                IsDownloading = false;
                DownloadFailed?.Invoke(ex.Message);
            }
        }
    }
}
