using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

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

        public event Action<double>? DownloadProgressChanged;
        public event Action<string>? DownloadCompleted;
        public event Action<string>? DownloadFailed;

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

                    return new UpdateInfo
                    {
                        Id = root.GetProperty("id").GetString() ?? string.Empty,
                        Version = serverVersionStr,
                        UpdateType = root.GetProperty("update_type").GetString() ?? "minor",
                        IsCompulsory = root.GetProperty("is_compulsory").GetBoolean(),
                        Changelog = root.GetProperty("changelog").GetString() ?? string.Empty,
                        PaymentAmount = amount,
                        FileUrl = root.GetProperty("file_url").GetString() ?? string.Empty
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckForUpdatesAsync failed: {ex.Message}");
            }
            return null;
        }

        public async Task StartDownloadAsync(UpdateInfo update)
        {
            if (IsDownloading) return;
            IsDownloading = true;
            DownloadProgress = 0.0;
            DownloadProgressChanged?.Invoke(0.0);

            try
            {
                // Check if FileUrl is a mock or example URL
                bool isMock = string.IsNullOrWhiteSpace(update.FileUrl) || 
                             update.FileUrl.Contains("example.com") || 
                             update.FileUrl.Contains("mock") || 
                             !update.FileUrl.StartsWith("http");

                if (isMock)
                {
                    // Simulated realistic download progress
                    for (int i = 1; i <= 100; i++)
                    {
                        await Task.Delay(40); // Total 4 seconds
                        DownloadProgress = i;
                        DownloadProgressChanged?.Invoke(DownloadProgress);
                    }
                }
                else
                {
                    // Real download logic
                    using (var response = await _http.GetAsync(update.FileUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var contentStream = await response.Content.ReadAsStreamAsync();
                        
                        var tempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp", "updates");
                        Directory.CreateDirectory(tempPath);
                        
                        string extension = ".zip";
                        if (update.FileUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            extension = ".exe";
                        }
                        var tempFile = Path.Combine(tempPath, $"update_v{update.Version}{extension}");

                        using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            var totalRead = 0L;
                            var bytesRead = 0;
                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalRead += bytesRead;
                                if (totalBytes > 0)
                                {
                                    DownloadProgress = Math.Round((double)totalRead / totalBytes * 100, 1);
                                    DownloadProgressChanged?.Invoke(DownloadProgress);
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
            }
            catch (Exception ex)
            {
                IsDownloading = false;
                DownloadFailed?.Invoke(ex.Message);
            }
        }
    }
}
