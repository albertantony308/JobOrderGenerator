using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ClientApp.Data;
using ClientApp.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace ClientApp.Services
{
    public static class CloudSyncService
    {
        public static double RealTimeCloudStorageUsedMb { get; private set; } = 0.0;
        public static int RealTimeCloudRowsCount { get; private set; } = 0;

        public static async Task SyncWithCloudAsync()
        {
            if (string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey))
                return;
            
            // Check if Cloud Sync is bypassed by SyncMode
            if (SettingsManager.Default.SyncMode == "LocalOnly")
                return;

            var _http = SupabaseClientManager.GetHttpClient();
            _http.Timeout = TimeSpan.FromSeconds(3); // 3-second timeout for fallback discovery
            var ownerKey = SettingsManager.Default.SubscriptionKey;

            try
            {
                LogSyncStatus($"Starting sync: ownerKey = {ownerKey}");
                // Step 1: Find the owner's activation key (to get ID and email)
                var keyResponse = await _http.GetAsync($"/rest/v1/activation_keys?key_code=eq.{Uri.EscapeDataString(ownerKey)}&select=id,key_code,email");
                if (!keyResponse.IsSuccessStatusCode)
                {
                    LogSyncStatus($"KeyResponse error: {keyResponse.StatusCode}");
                    MarkCloudUnavailable();
                    return;
                }

                var keys = await keyResponse.Content.ReadFromJsonAsync<JsonElement[]>();
                if (keys == null || keys.Length == 0)
                {
                    LogSyncStatus($"No activation key found in DB for key_code: {ownerKey}");
                    MarkCloudUnavailable();
                    return;
                }
                
                var ownerKeyId = keys[0].GetProperty("id").GetString();
                var ownerEmail = keys[0].GetProperty("email").GetString();
                var allKeyIds = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(ownerKeyId)) allKeyIds.Add(ownerKeyId);

                // Step 2: Find any staff keys (ST-*) linked to the same email
                if (!string.IsNullOrEmpty(ownerEmail))
                {
                    try
                    {
                        var staffKeyResponse = await _http.GetAsync($"/rest/v1/activation_keys?email=eq.{Uri.EscapeDataString(ownerEmail)}&key_code=like.ST-%25&select=id");
                        if (staffKeyResponse.IsSuccessStatusCode)
                        {
                            var staffKeys = await staffKeyResponse.Content.ReadFromJsonAsync<JsonElement[]>();
                            if (staffKeys != null)
                            {
                                foreach (var sk in staffKeys)
                                {
                                    var skId = sk.GetProperty("id").GetString();
                                    if (!string.IsNullOrEmpty(skId) && !allKeyIds.Contains(skId))
                                    {
                                        allKeyIds.Add(skId);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogSyncStatus($"Staff key lookup failed (non-fatal): {ex.Message}");
                    }
                }

                if (string.IsNullOrEmpty(ownerKeyId))
                {
                    LogSyncStatus($"Owner keyId not found in response.");
                    MarkCloudUnavailable();
                    return;
                }
                LogSyncStatus($"Found owner keyId: {ownerKeyId}, total key IDs (incl. staff): {allKeyIds.Count}");

                // Get the current Device ID
                var deviceIdHardware = LicenseManager.GetDeviceId();
                LogSyncStatus($"Current hardware deviceIdHardware: {deviceIdHardware}");

                var deviceResponse = await _http.GetAsync($"/rest/v1/devices?hardware_id=eq.{Uri.EscapeDataString(deviceIdHardware)}&activation_key_id=eq.{ownerKeyId}&select=id");
                if (!deviceResponse.IsSuccessStatusCode)
                {
                    LogSyncStatus($"DeviceResponse error: {deviceResponse.StatusCode}");
                    MarkCloudUnavailable();
                    return;
                }

                var devices = await deviceResponse.Content.ReadFromJsonAsync<JsonElement[]>();
                if (devices == null || devices.Length == 0)
                {
                    LogSyncStatus($"No device seat registered in DB for hardware_id: {deviceIdHardware} under key_id: {ownerKeyId}");
                    MarkCloudUnavailable();
                    return;
                }
                var deviceId = devices[0].GetProperty("id").GetString();
                LogSyncStatus($"Active deviceId in cloud devices table: {deviceId}");

                // 1. PULL FROM CLOUD (Fetch memos for devices linked to either key ID)
                var inKeysQuery = string.Join(",", allKeyIds);
                var allDevicesResponse = await _http.GetAsync($"/rest/v1/devices?activation_key_id=in.({inKeysQuery})&select=id");
                if (!allDevicesResponse.IsSuccessStatusCode)
                {
                    MarkCloudUnavailable();
                    return;
                }
                var allDevices = await allDevicesResponse.Content.ReadFromJsonAsync<JsonElement[]>();
                var deviceIds = allDevices?.Select(d => d.GetProperty("id").GetString()).ToList();

                if (deviceIds == null || deviceIds.Count == 0) return;
                var inQuery = string.Join(",", deviceIds);

                var memosResponse = await _http.GetAsync($"/rest/v1/service_memos?device_id=in.({inQuery})&select=memo_number,json_data,updated_at");
                if (memosResponse.IsSuccessStatusCode)
                {
                    var cloudMemosJson = await memosResponse.Content.ReadFromJsonAsync<JsonElement[]>();
                    
                    if (cloudMemosJson != null)
                    {
                        RealTimeCloudRowsCount = cloudMemosJson.Length;
                        double totalBytes = 0;
                        foreach (var record in cloudMemosJson)
                        {
                            if (record.TryGetProperty("json_data", out JsonElement prop) && prop.ValueKind == JsonValueKind.String)
                            {
                                string jData = prop.GetString() ?? "";
                                totalBytes += System.Text.Encoding.UTF8.GetByteCount(jData);
                            }
                        }
                        RealTimeCloudStorageUsedMb = totalBytes / (1024.0 * 1024.0);

                        // Async PATCH back to Supabase database so the cloud used storage remains synchronized globally
                        try
                        {
                            var updatePayload = new { cloud_storage_used_mb = RealTimeCloudStorageUsedMb };
                            var updateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/rest/v1/activation_keys?id=eq.{ownerKeyId}");
                            updateRequest.Content = JsonContent.Create(updatePayload);
                            await _http.SendAsync(updateRequest);
                        }
                        catch (Exception ex)
                        {
                            LogSyncStatus($"Failed to patch real-time storage to cloud: {ex.Message}");
                        }
                    }
                    
                    using (var db = new LocalDbContext())
                    {
                        var localMemos = db.ServiceMemos.ToList();

                        if (cloudMemosJson != null)
                        {
                            foreach (var record in cloudMemosJson)
                            {
                                var memoNum = record.GetProperty("memo_number").GetString();
                                var jsonData = record.GetProperty("json_data").GetString();
                                var updatedAt = DateTime.SpecifyKind(record.GetProperty("updated_at").GetDateTime(), DateTimeKind.Utc);

                                if (string.IsNullOrEmpty(memoNum) || string.IsNullOrEmpty(jsonData)) continue;

                                var cloudMemoDto = JsonSerializer.Deserialize<ServiceMemoDto>(jsonData);
                                if (cloudMemoDto == null) continue;

                                // Prevent mixing records: skip if the memo belongs to a different activation key/workspace
                                // (We allow empty CloudOwnerKey to enable backfilling for existing records)
                                if (!string.IsNullOrEmpty(cloudMemoDto.CloudOwnerKey) && cloudMemoDto.CloudOwnerKey != ownerKey) continue;

                                var localMemo = localMemos.FirstOrDefault(m => m.MemoNumber == memoNum);

                                 if (localMemo == null)
                                {
                                    // New from cloud
                                    var newMemo = new ServiceMemo
                                    {
                                        Id = 0, // Let SQLite assign auto-increment ID
                                        MemoNumber = cloudMemoDto.MemoNumber,
                                        CustomerName = cloudMemoDto.CustomerName,
                                        PhoneNumber = cloudMemoDto.PhoneNumber,
                                        DeviceName = cloudMemoDto.DeviceName,
                                        DeviceModel = cloudMemoDto.DeviceModel,
                                        IssueDescription = cloudMemoDto.IssueDescription,
                                        Status = cloudMemoDto.Status,
                                        CreatedAt = cloudMemoDto.CreatedAt,
                                        EstimatedCost = cloudMemoDto.EstimatedCost,
                                        ImagePath = SettingsManager.Default.SyncImagesEnabled ? cloudMemoDto.ImagePath : string.Empty,
                                        UpdatedAt = updatedAt.ToLocalTime(),
                                        CloudId = cloudMemoDto.CloudId,
                                        CloudOwnerKey = cloudMemoDto.CloudOwnerKey,
                                        CustomerAddress = cloudMemoDto.CustomerAddress,
                                        Phone1 = cloudMemoDto.Phone1,
                                        Phone2 = cloudMemoDto.Phone2,
                                        TechnicianName = cloudMemoDto.TechnicianName,
                                        Brand = cloudMemoDto.Brand,
                                        SerialNumber = cloudMemoDto.SerialNumber,
                                        Accessories = cloudMemoDto.Accessories,
                                        Diagnostics = cloudMemoDto.Diagnostics,
                                        OrderUpdates = cloudMemoDto.OrderUpdates,
                                        ReturnDate = cloudMemoDto.ReturnDate
                                    };
                                    db.ServiceMemos.Add(newMemo);
                                }
                                else if (ToUtc(updatedAt) > ToUtc(localMemo.UpdatedAt))
                                {
                                    // Cloud version is newer
                                    localMemo.CustomerName = cloudMemoDto.CustomerName;
                                    localMemo.PhoneNumber = cloudMemoDto.PhoneNumber;
                                    localMemo.DeviceName = cloudMemoDto.DeviceName;
                                    localMemo.DeviceModel = cloudMemoDto.DeviceModel;
                                    localMemo.IssueDescription = cloudMemoDto.IssueDescription;
                                    localMemo.Status = cloudMemoDto.Status;
                                    localMemo.EstimatedCost = cloudMemoDto.EstimatedCost;
                                    localMemo.CustomerAddress = cloudMemoDto.CustomerAddress;
                                    localMemo.Phone1 = cloudMemoDto.Phone1;
                                    localMemo.Phone2 = cloudMemoDto.Phone2;
                                    localMemo.TechnicianName = cloudMemoDto.TechnicianName;
                                    localMemo.Brand = cloudMemoDto.Brand;
                                    localMemo.SerialNumber = cloudMemoDto.SerialNumber;
                                    localMemo.Accessories = cloudMemoDto.Accessories;
                                    localMemo.Diagnostics = cloudMemoDto.Diagnostics;
                                    if (SettingsManager.Default.SyncImagesEnabled)
                                    {
                                        localMemo.ImagePath = cloudMemoDto.ImagePath;
                                    }
                                    localMemo.OrderUpdates = cloudMemoDto.OrderUpdates;
                                    localMemo.ReturnDate = cloudMemoDto.ReturnDate;
                                    localMemo.UpdatedAt = updatedAt.ToLocalTime();
                                    
                                    db.ServiceMemos.Update(localMemo);
                                }
                            }
                        }

                        // 2. PUSH TO CLOUD
                        foreach (var lMemo in localMemos)
                        {
                            // Find corresponding cloud record to compare timestamp
                            bool needsUpload = true;
                            if (cloudMemosJson != null)
                            {
                                var cMatch = cloudMemosJson.FirstOrDefault(c => c.GetProperty("memo_number").GetString() == lMemo.MemoNumber);
                                if (cMatch.ValueKind != JsonValueKind.Undefined)
                                {
                                    var cUpdatedAt = DateTime.SpecifyKind(cMatch.GetProperty("updated_at").GetDateTime(), DateTimeKind.Utc);
                                    if (ToUtc(lMemo.UpdatedAt) <= ToUtc(cUpdatedAt))
                                    {
                                        needsUpload = false;
                                    }
                                }
                            }

                            if (needsUpload)
                            {
                                var uploadDto = ServiceMemoDto.FromModel(lMemo, SettingsManager.Default.SyncImagesEnabled);

                                var payload = new
                                {
                                    memo_number = lMemo.MemoNumber,
                                    json_data = JsonSerializer.Serialize(uploadDto),
                                    device_id = deviceId,
                                    updated_at = ToUtc(lMemo.UpdatedAt).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                                };

                                var request = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/service_memos?on_conflict=memo_number");
                                request.Headers.Add("Prefer", "resolution=merge-duplicates");
                                request.Content = JsonContent.Create(payload);
                                await _http.SendAsync(request);
                            }
                        }

                        db.SaveChanges();
                    }
                }
                else
                {
                    MarkCloudUnavailable();
                    return;
                }

                MarkCloudAvailable();
                CloudStatusChanged?.Invoke();
                SyncCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                LogSyncStatus($"Sync exception: {ex.Message}\nStackTrace: {ex.StackTrace}\nInner: {ex.InnerException?.Message}");
                System.Diagnostics.Debug.WriteLine("Sync error: " + ex.Message);
                MarkCloudUnavailable();
            }
        }

        private static DateTime ToUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc)
                return dt;
            if (dt.Kind == DateTimeKind.Local)
                return dt.ToUniversalTime();
            return DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime();
        }

        private static void LogSyncStatus(string msg)
        {
            try
            {
                var logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp");
                System.IO.Directory.CreateDirectory(logDir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(logDir, "sync_status.log"), $"[{DateTime.Now}] {msg}\r\n");
            }
            catch { }
        }

        public static bool IsCloudOffline { get; private set; } = false;
        public static event Action? CloudStatusChanged;
        public static event Action? SyncCompleted;

        private static void MarkCloudUnavailable()
        {
            if (!IsCloudOffline)
            {
                IsCloudOffline = true;
                CloudStatusChanged?.Invoke();
            }
        }

        private static void MarkCloudAvailable()
        {
            if (IsCloudOffline)
            {
                IsCloudOffline = false;
                CloudStatusChanged?.Invoke();
            }
        }

        public static Task DeleteOldMemosAsync(int keepCount)
        {
            // Placeholder for now
            return Task.CompletedTask;
        }

    }
}
