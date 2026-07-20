using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Threading.Tasks;

namespace ClientApp.Services;

public class LicenseManager
{
    private static readonly string LicenseFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp", "license.json");
    private readonly HttpClient _http;
    private const string SupabaseUrl = "https://qcmcoofnxqzyrrbcwdde.supabase.co";
    private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InFjbWNvb2ZueHF6eXJyYmN3ZGRlIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Nzg1NTE0MDYsImV4cCI6MjA5NDEyNzQwNn0.BDse0v5cLXNT9wK9K7bOkLOkyZhPJL9HpZQuFA5fixY";

    public static LicenseStatus? CurrentStatus { get; set; }

    public LicenseManager()
    {
        _http = new HttpClient { BaseAddress = new Uri(SupabaseUrl) };
        _http.DefaultRequestHeaders.Add("apikey", SupabaseKey);
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseKey}");
    }


    public async Task<bool> IsLicenseValidAsync()
    {
        var status = await VerifyLicenseStatusAsync();
        return status.IsValid;
    }

    public async Task<LicenseStatus> VerifyLicenseStatusAsync()
    {
        var status = new LicenseStatus();

        if (!File.Exists(LicenseFilePath))
        {
            status.IsValid = false;
            status.WarningMessage = "No license found. Please activate the application.";
            return status;
        }

        LocalLicense? license = null;
        try
        {
            var json = await File.ReadAllTextAsync(LicenseFilePath);
            license = JsonSerializer.Deserialize<LocalLicense>(json);
        }
        catch
        {
            status.IsValid = false;
            status.WarningMessage = "License file is corrupted.";
            return status;
        }

        if (license == null)
        {
            status.IsValid = false;
            status.WarningMessage = "License file is empty.";
            return status;
        }

        status.IsTrial = license.IsTrial;

        try
        {
            // Try online validation
            var keyRes = await _http.GetAsync($"/rest/v1/activation_keys?id=eq.{license.KeyId}&select=*,subscriptions(name,max_devices)");
            if (keyRes.IsSuccessStatusCode)
            {
                var keys = await keyRes.Content.ReadFromJsonAsync<JsonElement[]>();
                if (keys != null && keys.Length > 0)
                {
                    var root = keys[0];

                    // Check if key is active. If deactivated from admin, it's immediately invalid!
                    bool isActive = false;
                    if (root.TryGetProperty("is_active", out JsonElement activeProp) && activeProp.ValueKind == JsonValueKind.True)
                    {
                        isActive = true;
                    }

                    if (!isActive)
                    {
                        try { File.Delete(LicenseFilePath); } catch { }
                        status.IsValid = false;
                        status.WarningMessage = "This license key has been deactivated or revoked.";
                        return status;
                    }

                    // Check if device is still registered on the server
                    var response = await _http.GetAsync($"/rest/v1/devices?hardware_id=eq.{license.DeviceId}&activation_key_id=eq.{license.KeyId}&select=id");
                    bool deviceRegistered = false;
                    if (response.IsSuccessStatusCode)
                    {
                        var devices = await response.Content.ReadFromJsonAsync<JsonElement[]>();
                        if (devices != null && devices.Length > 0)
                        {
                            deviceRegistered = true;
                        }
                    }

                    if (!deviceRegistered)
                    {
                        try { File.Delete(LicenseFilePath); } catch { }
                        status.IsValid = false;
                        status.WarningMessage = "This device has been deregistered for this key.";
                        return status;
                    }

                    // Update local cache values from the server
                    DateTime? expiresAt = null;
                    if (root.TryGetProperty("expires_at", out JsonElement expiresProp) && expiresProp.ValueKind != JsonValueKind.Null)
                    {
                        if (DateTime.TryParse(expiresProp.GetString(), out DateTime exp))
                            expiresAt = exp;
                    }

                    bool isTrial = false;
                    if (root.TryGetProperty("is_trial", out JsonElement trialProp) && trialProp.ValueKind == JsonValueKind.True)
                    {
                        isTrial = true;
                    }

                    string planName = isTrial ? "Free Trial" : "Basic";
                    if (!isTrial && root.TryGetProperty("subscriptions", out JsonElement subProp) && subProp.ValueKind == JsonValueKind.Object)
                    {
                        if (subProp.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind != JsonValueKind.Null)
                        {
                            planName = nameProp.GetString() ?? "Basic";
                        }
                    }

                    license.ExpiresAt = expiresAt;
                    license.IsTrial = isTrial;
                    license.IsUnlimited = (expiresAt == null);
                    license.PlanName = planName;
                    license.LastCheckedAt = DateTime.UtcNow;

                    if (expiresAt.HasValue && expiresAt.Value > DateTime.UtcNow)
                    {
                        // Reset grace expiry on renewal
                        license.GraceExpiry = null;
                        license.HasSeenExpiredWarning = false;
                    }

                    // Save the updated cache
                    SaveLicense(license.Key, license.DeviceId, license.KeyId, license.ExpiresAt, license.IsTrial, license.PlanName, license.IsUnlimited, license.GraceExpiry, license.HasSeenExpiredWarning);

                    status.IsTrial = isTrial;
                }
            }
        }
        catch
        {
            // Network error / server offline: Fall back to cached local details
        }

        // Evaluate using the resolved configuration (either latest online or cached offline)
        if (license.IsUnlimited)
        {
            status.IsValid = true;
            status.DaysLeft = -1;
            status.WarningMessage = "";
            return status;
        }

        if (license.ExpiresAt.HasValue)
        {
            DateTime expiresAt = license.ExpiresAt.Value;
            if (DateTime.UtcNow > expiresAt)
            {
                // Expired! Check grace day
                if (!license.GraceExpiry.HasValue)
                {
                    // First time opening app after expiration - grant grace day ending at end of today
                    license.GraceExpiry = DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
                    SaveLicense(license.Key, license.DeviceId, license.KeyId, license.ExpiresAt, license.IsTrial, license.PlanName, license.IsUnlimited, license.GraceExpiry, license.HasSeenExpiredWarning);

                    status.IsValid = true;
                    status.DaysLeft = 0;
                    status.ShowBigWarning = true;
                    status.WarningMessage = license.IsTrial
                        ? "Your trial will expire today. Recharge or upgrade your subscription to continue using the app."
                        : "Your subscription will expire today. Recharge or upgrade your subscription to continue using the app.";
                    return status;
                }
                else
                {
                    if (DateTime.UtcNow > license.GraceExpiry.Value)
                    {
                        status.IsValid = false;
                        status.WarningMessage = license.IsTrial ? "Trial expired" : "Subscription expired";
                        return status;
                    }
                    else
                    {
                        status.IsValid = true;
                        status.DaysLeft = 0;
                        status.ShowBigWarning = true;
                        status.WarningMessage = license.IsTrial
                            ? "Your trial will expire today. Recharge or upgrade your subscription to continue using the app."
                            : "Your subscription will expire today. Recharge or upgrade your subscription to continue using the app.";
                        return status;
                    }
                }
            }
            else
            {
                int daysLeft = (expiresAt.Date - DateTime.UtcNow.Date).Days;
                status.IsValid = true;
                status.DaysLeft = daysLeft;

                if (daysLeft <= 0)
                {
                    status.ShowBigWarning = true;
                    status.WarningMessage = license.IsTrial
                        ? "Your trial will expire today. Recharge or upgrade your subscription to continue using the app."
                        : "Your subscription will expire today. Recharge or upgrade your subscription to continue using the app.";
                }
                else if (daysLeft <= 5)
                {
                    status.ShowBigWarning = false;
                    status.WarningMessage = license.IsTrial
                        ? $"Your trial will expire in {daysLeft} days."
                        : $"Your subscription will expire in {daysLeft} days.";
                }
                else
                {
                    status.WarningMessage = "";
                }
                return status;
            }
        }

        status.IsValid = false;
        status.WarningMessage = "Invalid license status.";
        return status;
    }

    public async Task<(bool success, string keyId, string errorMsg)> ActivateOnlineAsync(string key)
    {
        try
        {
            var response = await _http.GetAsync($"/rest/v1/activation_keys?key_code=eq.{Uri.EscapeDataString(key)}&is_active=eq.true&select=*,subscriptions(name,max_devices)");
            if (!response.IsSuccessStatusCode) return (false, "", "Network error.");
            var keys = await response.Content.ReadFromJsonAsync<JsonElement[]>();
            if (keys == null || keys.Length == 0) return (false, "", "Invalid or revoked key.");
            
            if (keys[0].TryGetProperty("expires_at", out JsonElement expProp) && expProp.ValueKind != JsonValueKind.Null)
            {
                if (DateTime.TryParse(expProp.GetString(), out DateTime expAt) && expAt < DateTime.UtcNow)
                {
                    return (false, "", "This key has expired.");
                }
            }

            var keyData = keys[0];
            var keyId = keyData.GetProperty("id").GetString()!;
            var maxDevices = keyData.GetProperty("custom_max_devices").ValueKind != JsonValueKind.Null ? 
                             keyData.GetProperty("custom_max_devices").GetInt32() : 
                             keyData.GetProperty("subscriptions").GetProperty("max_devices").GetInt32();

            var deviceId = GetDeviceId();
            
            // Clean up any old device seats registered under other activation keys
            try
            {
                await _http.DeleteAsync($"/rest/v1/devices?hardware_id=eq.{Uri.EscapeDataString(deviceId)}&activation_key_id=not.eq.{keyId}");
            }
            catch { }
            
            var devRes = await _http.GetAsync($"/rest/v1/devices?activation_key_id=eq.{keyId}&select=id,hardware_id");
            var currentDevices = await devRes.Content.ReadFromJsonAsync<JsonElement[]>() ?? Array.Empty<JsonElement>();

            // Check if key is a free trial and prevent multiple free trials on the same device
            bool isTrialKey = false;
            if (keyData.TryGetProperty("is_trial", out JsonElement trialProp) && trialProp.ValueKind == JsonValueKind.True)
            {
                isTrialKey = true;
            }

            if (isTrialKey)
            {
                // If the device is not already registered under this activation key, check if it has ever activated any trial key
                if (!currentDevices.Any(d => d.TryGetProperty("hardware_id", out JsonElement hwProp) && hwProp.GetString() == deviceId))
                {
                    var deviceCheckRes = await _http.GetAsync($"/rest/v1/devices?hardware_id=eq.{deviceId}&select=activation_key_id");
                    if (deviceCheckRes.IsSuccessStatusCode)
                      {
                        var registeredDevices = await deviceCheckRes.Content.ReadFromJsonAsync<JsonElement[]>();
                        if (registeredDevices != null && registeredDevices.Length > 0)
                        {
                            var keyIds = registeredDevices
                                .Select(d => d.TryGetProperty("activation_key_id", out JsonElement prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() : null)
                                .Where(id => !string.IsNullOrEmpty(id))
                                .ToList();

                            if (keyIds.Count > 0)
                            {
                                var keyFilter = string.Join(",", keyIds.Select(id => $"\"{id}\""));
                                var trialCheckRes = await _http.GetAsync($"/rest/v1/activation_keys?id=in.({keyFilter})&is_trial=eq.true&select=id");
                                if (trialCheckRes.IsSuccessStatusCode)
                                {
                                    var trialKeys = await trialCheckRes.Content.ReadFromJsonAsync<JsonElement[]>();
                                    if (trialKeys != null && trialKeys.Length > 0)
                                    {
                                        return (false, "", "A free trial has already been activated on this device. You can only use a paid subscription plan (Basic, Professional, or Enterprise).");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Extract expiry and metadata for offline caching
            DateTime? expiresAt = null;
            if (keyData.TryGetProperty("expires_at", out JsonElement expiresProp) && expiresProp.ValueKind != JsonValueKind.Null)
            {
                if (DateTime.TryParse(expiresProp.GetString(), out DateTime exp))
                    expiresAt = exp;
            }

            bool isTrial = isTrialKey;
            string planName = isTrial ? "Free Trial" : "Basic";
            if (!isTrial && keyData.TryGetProperty("subscriptions", out JsonElement subProp) && subProp.ValueKind == JsonValueKind.Object)
            {
                if (subProp.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind != JsonValueKind.Null)
                {
                    planName = nameProp.GetString() ?? "Basic";
                }
            }

            bool isUnlimited = expiresAt == null;

            if (currentDevices.Any(d => d.GetProperty("hardware_id").GetString() == deviceId))
            {
                SaveLicense(key, deviceId, keyId, expiresAt, isTrial, planName, isUnlimited);
                return (true, keyId, "");
            }

            if (maxDevices != -1 && currentDevices.Length >= maxDevices)
            {
                return (false, "", $"Device limit reached for this key. Maximum {maxDevices} device(s) allowed.");
            }

            var registerData = new { hardware_id = deviceId, device_name = Environment.MachineName, activation_key_id = keyId };
            var regRes = await _http.PostAsJsonAsync("/rest/v1/devices", registerData);
            
            if (regRes.IsSuccessStatusCode)
            {
                SaveLicense(key, deviceId, keyId, expiresAt, isTrial, planName, isUnlimited);
                return (true, keyId, "");
            }
            else
            {
                // Fall back: if POST failed (likely due to unique constraint on hardware_id), try PATCH to update the device registration
                var patchData = new { device_name = Environment.MachineName, activation_key_id = keyId };
                var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/rest/v1/devices?hardware_id=eq.{Uri.EscapeDataString(deviceId)}");
                patchRequest.Content = JsonContent.Create(patchData);
                var patchRes = await _http.SendAsync(patchRequest);
                if (patchRes.IsSuccessStatusCode)
                {
                    SaveLicense(key, deviceId, keyId, expiresAt, isTrial, planName, isUnlimited);
                    return (true, keyId, "");
                }
                
                string errorDetail = "Failed to register device.";
                try
                {
                    var responseText = await regRes.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(responseText))
                    {
                        var errorJson = JsonSerializer.Deserialize<JsonElement>(responseText);
                        if (errorJson.TryGetProperty("message", out JsonElement msgProp))
                        {
                            errorDetail = msgProp.GetString() ?? errorDetail;
                        }
                    }
                }
                catch { }
                return (false, "", errorDetail);
            }
        }
        catch (Exception ex)
        {
            return (false, "", "Connection error: " + ex.Message);
        }
    }

    public async Task<CompanyProfile?> GetProfileAsync(string keyId)
    {
        try
        {
            var response = await _http.GetAsync($"/rest/v1/company_profiles?activation_key_id=eq.{keyId}&select=*");
            var profiles = await response.Content.ReadFromJsonAsync<CompanyProfile[]>();
            return profiles?.FirstOrDefault();
        }
        catch { return null; }
    }

    public async Task<bool> SaveProfileAsync(string keyId, CompanyProfile profile)
    {
        try
        {
            profile.activation_key_id = keyId;
            var existingProfile = await GetProfileAsync(keyId);
            if (existingProfile != null)
            {
                profile.id = existingProfile.id;
                var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Patch, $"/rest/v1/company_profiles?activation_key_id=eq.{keyId}")
                {
                    Content = System.Net.Http.Json.JsonContent.Create(profile)
                };
                var response = await _http.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            else
            {
                var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "/rest/v1/company_profiles")
                {
                    Content = System.Net.Http.Json.JsonContent.Create(profile)
                };
                var response = await _http.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveProfileAsync exception: {ex.Message}");
            return false;
        }
    }

    private void SaveLicense(string key, string deviceId, string? keyId, DateTime? expiresAt = null, bool isTrial = false, string planName = "Trial", bool isUnlimited = false, DateTime? graceExpiry = null, bool hasSeenExpiredWarning = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LicenseFilePath)!);
        var license = new LocalLicense
        {
            Key = key,
            DeviceId = deviceId,
            KeyId = keyId,
            ExpiresAt = expiresAt,
            IsTrial = isTrial,
            PlanName = planName,
            IsUnlimited = isUnlimited,
            GraceExpiry = graceExpiry,
            HasSeenExpiredWarning = hasSeenExpiredWarning,
            LastCheckedAt = DateTime.UtcNow
        };
        File.WriteAllText(LicenseFilePath, JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true }));
    }

    public string GetCurrentKeyId()
    {
        if (!File.Exists(LicenseFilePath)) return "";
        var json = File.ReadAllText(LicenseFilePath);
        var license = JsonSerializer.Deserialize<LocalLicense>(json);
        return license?.KeyId ?? "";
    }

    public async Task<(string key, DateTime? expiresAt, int activeDevices, int maxDevices, bool cloudSyncEnabled, double cloudStorageLimitGb, double cloudStorageUsedMb, string planName)> GetCurrentLicenseInfoAsync()
    {
        if (!File.Exists(LicenseFilePath)) return ("", null, 0, 0, false, 5.0, 0.0, "Trial");
        var json = await File.ReadAllTextAsync(LicenseFilePath);
        var license = JsonSerializer.Deserialize<LocalLicense>(json);
        if (license == null) return ("", null, 0, 0, false, 5.0, 0.0, "Trial");
 
        try
        {
            var keyRes = await _http.GetAsync($"/rest/v1/activation_keys?id=eq.{license.KeyId}&select=key_code,expires_at,custom_max_devices,cloud_sync_enabled,cloud_storage_limit_gb,cloud_storage_used_mb,is_trial,subscriptions(name,max_devices),devices(id)");
            if (keyRes.IsSuccessStatusCode)
            {
                var keys = await keyRes.Content.ReadFromJsonAsync<JsonElement[]>();
                if (keys != null && keys.Length > 0)
                {
                    var root = keys[0];
                    string keyCode = root.GetProperty("key_code").GetString() ?? license.Key;
                    DateTime? expiresAt = null;
                    if (root.TryGetProperty("expires_at", out JsonElement expiresProp) && expiresProp.ValueKind != JsonValueKind.Null)
                    {
                        if (DateTime.TryParse(expiresProp.GetString(), out DateTime exp))
                            expiresAt = exp;
                    }
 
                    int activeDevices = 0;
                    if (root.TryGetProperty("devices", out JsonElement devicesProp) && devicesProp.ValueKind == JsonValueKind.Array)
                    {
                        activeDevices = devicesProp.GetArrayLength();
                    }
 
                    int maxDevices = -1;
                    string planName = "Basic";
                    
                    bool isTrial = false;
                    if (root.TryGetProperty("is_trial", out JsonElement trialProp) && trialProp.ValueKind == JsonValueKind.True)
                    {
                        isTrial = true;
                        planName = "Free Trial";
                    }

                    if (root.TryGetProperty("custom_max_devices", out JsonElement customMaxProp) && customMaxProp.ValueKind != JsonValueKind.Null)
                    {
                        maxDevices = customMaxProp.GetInt32();
                    }
                    else if (root.TryGetProperty("subscriptions", out JsonElement subProp) && subProp.ValueKind == JsonValueKind.Object)
                    {
                        if (subProp.TryGetProperty("max_devices", out JsonElement maxDevicesProp) && maxDevicesProp.ValueKind != JsonValueKind.Null)
                        {
                            maxDevices = maxDevicesProp.GetInt32();
                        }
                    }

                    if (!isTrial && root.TryGetProperty("subscriptions", out JsonElement sProp) && sProp.ValueKind == JsonValueKind.Object)
                    {
                        if (sProp.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind != JsonValueKind.Null)
                        {
                            planName = nameProp.GetString() ?? "Basic";
                        }
                    }
 
                    bool cloudSyncEnabled = false;
                    if (root.TryGetProperty("cloud_sync_enabled", out JsonElement csProp) && csProp.ValueKind == JsonValueKind.True)
                    {
                        cloudSyncEnabled = true;
                    }
 
                    double cloudStorageLimit = 5.0;
                    if (root.TryGetProperty("cloud_storage_limit_gb", out JsonElement cslProp) && cslProp.ValueKind != JsonValueKind.Null)
                    {
                        if (cslProp.ValueKind == JsonValueKind.Number)
                        {
                            cloudStorageLimit = cslProp.GetDouble();
                        }
                    }
 
                    double cloudStorageUsed = 0.0;
                    if (root.TryGetProperty("cloud_storage_used_mb", out JsonElement csuProp) && csuProp.ValueKind != JsonValueKind.Null)
                    {
                        if (csuProp.ValueKind == JsonValueKind.Number)
                        {
                            cloudStorageUsed = csuProp.GetDouble();
                        }
                    }
 
                    return (keyCode, expiresAt, activeDevices, maxDevices, cloudSyncEnabled, cloudStorageLimit, cloudStorageUsed, planName);
                }
            }
        }
        catch { }
        return (license.Key, null, 0, 0, false, 5.0, 0.0, "Trial");
    }

    public static string GetDeviceId()
    {
        try
        {
            if (File.Exists(LicenseFilePath))
            {
                var json = File.ReadAllText(LicenseFilePath);
                var license = JsonSerializer.Deserialize<LocalLicense>(json);
                if (license != null && !string.IsNullOrEmpty(license.DeviceId))
                {
                    return license.DeviceId;
                }
            }
        }
        catch { }

        var macAddr = (
            from nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            where nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
            select nic.GetPhysicalAddress().ToString()
        ).FirstOrDefault();
        return string.IsNullOrEmpty(macAddr) ? Environment.MachineName : macAddr;
    }

    /// <summary>
    /// Returns a deterministic 1-letter prefix for this device's order IDs,
    /// derived from the device's MAC address / hardware ID.
    /// Examples: "A", "B", "C"
    /// </summary>
    public static string GetDeviceOrderPrefix()
    {
        string deviceId = GetDeviceId();
        int hash = 0;
        foreach (char c in deviceId)
            hash = (hash * 31 + c) & 0x7FFFFFFF;
        hash %= 26; // 26 possibilities (A to Z)
        char first = (char)('A' + hash);
        return $"{first}-";
    }


    public async Task<(bool valid, string email)> VerifySubscriptionAsync(string key)
    {
        try
        {
            var response = await _http.GetAsync($"/rest/v1/activation_keys?key_code=eq.{Uri.EscapeDataString(key)}&is_active=eq.true&select=email,expires_at");
            if (response.IsSuccessStatusCode)
            {
                var keys = await response.Content.ReadFromJsonAsync<JsonElement[]>();
                if (keys != null && keys.Length > 0)
                {
                    if (keys[0].TryGetProperty("expires_at", out JsonElement expProp) && expProp.ValueKind != JsonValueKind.Null)
                    {
                        if (DateTime.TryParse(expProp.GetString(), out DateTime expAt) && expAt < DateTime.UtcNow)
                        {
                            return (false, string.Empty);
                        }
                    }
                    return (true, keys[0].GetProperty("email").GetString() ?? "");
                }
            }
        }
        catch { }
        return (false, string.Empty);
    }

    public async Task<bool> DeactivateLicenseAsync()
    {
        if (!File.Exists(LicenseFilePath)) return true;
        
        try
        {
            var json = await File.ReadAllTextAsync(LicenseFilePath);
            var license = JsonSerializer.Deserialize<LocalLicense>(json);
            if (license != null && !string.IsNullOrEmpty(license.DeviceId))
            {
                // Delete device registration from Supabase to free up device seat
                var response = await _http.DeleteAsync($"/rest/v1/devices?hardware_id=eq.{license.DeviceId}");
            }
        }
        catch { }

        try
        {
            if (File.Exists(LicenseFilePath))
            {
                File.Delete(LicenseFilePath);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private class LocalLicense
    {
        public string Key { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public string? KeyId { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsTrial { get; set; }
        public bool IsUnlimited { get; set; }
        public string PlanName { get; set; } = "Trial";
        public DateTime? GraceExpiry { get; set; }
        public bool HasSeenExpiredWarning { get; set; }
        public DateTime? LastCheckedAt { get; set; }
    }
}

public class LicenseStatus
{
    public bool IsValid { get; set; }
    public string WarningMessage { get; set; } = "";
    public int DaysLeft { get; set; }
    public bool ShowBigWarning { get; set; }
    public bool IsTrial { get; set; }
}

public class CompanyProfile
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? id { get; set; }
    public string activation_key_id { get; set; } = "";
    public string company_name { get; set; } = "";
    public string phone_number { get; set; } = "";
    public string email_id { get; set; } = "";
    public string? logo_base64 { get; set; }
}
