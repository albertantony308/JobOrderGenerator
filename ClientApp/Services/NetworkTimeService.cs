using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ClientApp.Services
{
    public static class NetworkTimeService
    {
        private static TimeSpan _clockOffset = TimeSpan.Zero;
        private static bool _hasSynced = false;
        private static readonly object _syncLock = new object();

        public static bool HasSynced => _hasSynced;

        /// <summary>
        /// Gets the current trusted UTC time (network time if available, or local machine UTC time).
        /// </summary>
        public static DateTime GetUtcNow()
        {
            lock (_syncLock)
            {
                return DateTime.UtcNow + _clockOffset;
            }
        }

        /// <summary>
        /// Gets the current trusted local time (converted from trusted UTC time).
        /// </summary>
        public static DateTime GetLocalTime()
        {
            return GetUtcNow().ToLocalTime();
        }

        /// <summary>
        /// Updates the trusted clock offset using a known server UTC timestamp (e.g. from an HTTP response header or Supabase API).
        /// </summary>
        public static void UpdateServerTime(DateTime serverUtcTime)
        {
            lock (_syncLock)
            {
                _clockOffset = serverUtcTime - DateTime.UtcNow;
                _hasSynced = true;
            }
        }

        /// <summary>
        /// Attempts to fetch trusted UTC time from a fast, trusted HTTP endpoint (e.g. Google or Supabase server Date header).
        /// </summary>
        public static async Task SyncWithTrustedTimeAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                using var request = new HttpRequestMessage(HttpMethod.Head, "https://www.google.com");
                using var response = await client.SendAsync(request);

                if (response.Headers.Date.HasValue)
                {
                    DateTime serverUtc = response.Headers.Date.Value.UtcDateTime;
                    UpdateServerTime(serverUtc);
                    System.Diagnostics.Debug.WriteLine($"[NetworkTimeService] Synced trusted time from Google: {serverUtc} (Offset: {_clockOffset.TotalSeconds:F2}s)");
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NetworkTimeService] Primary time sync (Google) failed: {ex.Message}");
            }

            // Fallback: Try Supabase endpoint
            try
            {
                using var client = SupabaseClientManager.GetHttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                using var request = new HttpRequestMessage(HttpMethod.Head, "/rest/v1/");
                using var response = await client.SendAsync(request);

                if (response.Headers.Date.HasValue)
                {
                    DateTime serverUtc = response.Headers.Date.Value.UtcDateTime;
                    UpdateServerTime(serverUtc);
                    System.Diagnostics.Debug.WriteLine($"[NetworkTimeService] Synced trusted time from Supabase: {serverUtc} (Offset: {_clockOffset.TotalSeconds:F2}s)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NetworkTimeService] Fallback time sync (Supabase) failed: {ex.Message}");
            }
        }
    }
}
