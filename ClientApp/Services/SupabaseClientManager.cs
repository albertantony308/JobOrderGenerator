using System;
using System.Threading.Tasks;
using Supabase;

namespace ClientApp.Services
{
    public static class SupabaseClientManager
    {
        private static Client? _client;

        public static Client? Instance
        {
            get
            {
                if (_client == null)
                {
                    Initialize();
                }
                return _client;
            }
        }

        public static void Initialize()
        {
            try
            {
                var url = "https://qcmcoofnxqzyrrbcwdde.supabase.co";
                if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    throw new ArgumentException("Invalid Supabase URL");
                }

                var key = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InFjbWNvb2ZueHF6eXJyYmN3ZGRlIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Nzg1NTE0MDYsImV4cCI6MjA5NDEyNzQwNn0.BDse0v5cLXNT9wK9K7bOkLOkyZhPJL9HpZQuFA5fixY";

                var options = new SupabaseOptions
                {
                    AutoRefreshToken = true,
                    AutoConnectRealtime = true
                };

                _client = new Client(url, key, options);
                
                // Initialize asynchronously without blocking the calling thread
                _ = _client.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Supabase Init Error: {ex.Message}");
                _client = null;
            }
        }
        
        public static bool IsConfigured => !string.IsNullOrWhiteSpace(SettingsManager.Default.SubscriptionKey);

        public static System.Net.Http.HttpClient GetHttpClient()
        {
            var url = "https://qcmcoofnxqzyrrbcwdde.supabase.co";
            var key = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InFjbWNvb2ZueHF6eXJyYmN3ZGRlIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Nzg1NTE0MDYsImV4cCI6MjA5NDEyNzQwNn0.BDse0v5cLXNT9wK9K7bOkLOkyZhPJL9HpZQuFA5fixY";
            var client = new System.Net.Http.HttpClient { BaseAddress = new Uri(url) };
            client.DefaultRequestHeaders.Add("apikey", key);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
            return client;
        }
    }
}
