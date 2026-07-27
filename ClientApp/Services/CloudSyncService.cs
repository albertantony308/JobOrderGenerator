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
        public static event Action<ServiceMemoDto>? CloudOrderCompleted;

        public static async Task SyncWithCloudAsync()
        {
            if (string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey))
                return;
            
            // Check if Cloud Sync is bypassed by SyncMode or Fully Offline Mode
            if (SettingsManager.Default.SyncMode == "LocalOnly" || SettingsManager.Default.IsFullyOfflineMode)
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
                var allAllowedKeyCodes = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase) { ownerKey };

                if (!string.IsNullOrEmpty(ownerKeyId)) allKeyIds.Add(ownerKeyId);

                // Step 2: Resolve ALL activation keys (Owner & Staff) sharing the same account email
                if (!string.IsNullOrEmpty(ownerEmail))
                {
                    try
                    {
                        var orgKeysResponse = await _http.GetAsync($"/rest/v1/activation_keys?email=eq.{Uri.EscapeDataString(ownerEmail)}&select=id,key_code");
                        if (orgKeysResponse.IsSuccessStatusCode)
                        {
                            var orgKeys = await orgKeysResponse.Content.ReadFromJsonAsync<JsonElement[]>();
                            if (orgKeys != null)
                            {
                                foreach (var ok in orgKeys)
                                {
                                    var kId = ok.GetProperty("id").GetString();
                                    var kCode = ok.GetProperty("key_code").GetString();
                                    if (!string.IsNullOrEmpty(kId) && !allKeyIds.Contains(kId))
                                    {
                                        allKeyIds.Add(kId);
                                    }
                                    if (!string.IsNullOrEmpty(kCode))
                                    {
                                        allAllowedKeyCodes.Add(kCode);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogSyncStatus($"Organization keys lookup failed (non-fatal): {ex.Message}");
                    }
                }

                if (string.IsNullOrEmpty(ownerKeyId))
                {
                    LogSyncStatus($"Owner keyId not found in response.");
                    MarkCloudUnavailable();
                    return;
                }
                LogSyncStatus($"Found owner keyId: {ownerKeyId}, total key IDs (incl. staff/owner): {allKeyIds.Count}");

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
                string? deviceId = null;
                if (devices != null && devices.Length > 0)
                {
                    deviceId = devices[0].GetProperty("id").GetString();
                }

                if (string.IsNullOrEmpty(deviceId))
                {
                    LogSyncStatus($"No device seat registered in DB for hardware_id: {deviceIdHardware} under key_id: {ownerKeyId}. Attempting auto-registration...");
                    try
                    {
                        var regData = new { hardware_id = deviceIdHardware, device_name = Environment.MachineName, activation_key_id = ownerKeyId };
                        var regRes = await _http.PostAsJsonAsync("/rest/v1/devices", regData);
                        if (regRes.IsSuccessStatusCode)
                        {
                            var newDev = await regRes.Content.ReadFromJsonAsync<JsonElement[]>();
                            if (newDev != null && newDev.Length > 0)
                            {
                                deviceId = newDev[0].GetProperty("id").GetString();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogSyncStatus($"Auto device registration failed: {ex.Message}");
                    }
                }

                if (string.IsNullOrEmpty(deviceId))
                {
                    LogSyncStatus("Could not resolve active deviceId.");
                    MarkCloudUnavailable();
                    return;
                }
                LogSyncStatus($"Active deviceId in cloud devices table: {deviceId}");

                // 1. PULL FROM CLOUD (Smart Delta Sync - Lightweight Header Query first to save bandwidth)
                var inKeysQuery = string.Join(",", allKeyIds);
                var allDevicesResponse = await _http.GetAsync($"/rest/v1/devices?activation_key_id=in.({inKeysQuery})&select=id");
                
                // Primary header query by devices; fallback to fetching all service memo headers if devices list is pending
                HttpResponseMessage headerResponse;
                if (allDevicesResponse.IsSuccessStatusCode)
                {
                    var allDevices = await allDevicesResponse.Content.ReadFromJsonAsync<JsonElement[]>();
                    var deviceIds = allDevices?.Select(d => d.GetProperty("id").GetString()).Where(id => !string.IsNullOrEmpty(id)).ToList();

                    if (deviceIds != null && deviceIds.Count > 0)
                    {
                        var inQuery = string.Join(",", deviceIds);
                        headerResponse = await _http.GetAsync($"/rest/v1/service_memos?device_id=in.({inQuery})&select=memo_number,updated_at");
                    }
                    else
                    {
                        headerResponse = await _http.GetAsync("/rest/v1/service_memos?select=memo_number,updated_at&order=updated_at.desc&limit=1000");
                    }
                }
                else
                {
                    headerResponse = await _http.GetAsync("/rest/v1/service_memos?select=memo_number,updated_at&order=updated_at.desc&limit=1000");
                }

                if (headerResponse.IsSuccessStatusCode)
                {
                    var cloudHeadersJson = await headerResponse.Content.ReadFromJsonAsync<JsonElement[]>();
                    
                    var cloudHeaderMap = new System.Collections.Generic.Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                    if (cloudHeadersJson != null)
                    {
                        RealTimeCloudRowsCount = cloudHeadersJson.Length;
                        foreach (var record in cloudHeadersJson)
                        {
                            if (record.TryGetProperty("memo_number", out JsonElement memoProp) && memoProp.ValueKind == JsonValueKind.String)
                            {
                                var mNum = memoProp.GetString();
                                if (!string.IsNullOrEmpty(mNum) && record.TryGetProperty("updated_at", out JsonElement dateProp) && dateProp.ValueKind == JsonValueKind.String)
                                {
                                    if (DateTime.TryParse(dateProp.GetString(), out DateTime uTime))
                                    {
                                        cloudHeaderMap[mNum] = DateTime.SpecifyKind(uTime, DateTimeKind.Utc);
                                    }
                                }
                            }
                        }
                    }

                    // Compare cloud headers with local DB to determine which memos actually require full payload download
                    var memosToFetch = new System.Collections.Generic.List<string>();
                    using (var dbCheck = new LocalDbContext())
                    {
                        var localMemosMap = dbCheck.ServiceMemos.ToDictionary(m => m.MemoNumber, m => m, StringComparer.OrdinalIgnoreCase);

                        foreach (var kvp in cloudHeaderMap)
                        {
                            var cloudMemoNum = kvp.Key;
                            var cloudUtc = kvp.Value;

                            if (!localMemosMap.TryGetValue(cloudMemoNum, out var localM))
                            {
                                // New record from cloud -> fetch full JSON
                                memosToFetch.Add(cloudMemoNum);
                            }
                            else if (localM.Status != "Deleted" && localM.Status != "Deleted_Synced")
                            {
                                var localUtc = ToUtc(localM.UpdatedAt);
                                if (cloudUtc > localUtc.AddMilliseconds(500))
                                {
                                    // Cloud has newer update -> fetch full JSON
                                    memosToFetch.Add(cloudMemoNum);
                                }
                            }
                        }
                    }

                    LogSyncStatus($"Smart Delta Sync: Cloud Total Memos = {cloudHeaderMap.Count}, Changed/New Memos To Download = {memosToFetch.Count}");

                    // Batch fetch full json_data ONLY for memos that changed or are new
                    var cloudMemosJsonList = new System.Collections.Generic.List<JsonElement>();
                    if (memosToFetch.Count > 0)
                    {
                        for (int i = 0; i < memosToFetch.Count; i += 50)
                        {
                            var batch = memosToFetch.Skip(i).Take(50);
                            var inMemosQuery = string.Join(",", batch.Select(Uri.EscapeDataString));
                            var batchRes = await _http.GetAsync($"/rest/v1/service_memos?memo_number=in.({inMemosQuery})&select=memo_number,json_data,updated_at");
                            if (batchRes.IsSuccessStatusCode)
                            {
                                var batchItems = await batchRes.Content.ReadFromJsonAsync<JsonElement[]>();
                                if (batchItems != null)
                                {
                                    cloudMemosJsonList.AddRange(batchItems);
                                }
                            }
                        }

                        // Calculate real-time storage used from fetched full payloads
                        double totalBytes = 0;
                        foreach (var record in cloudMemosJsonList)
                        {
                            if (record.TryGetProperty("json_data", out JsonElement prop) && prop.ValueKind == JsonValueKind.String)
                            {
                                string jData = prop.GetString() ?? "";
                                totalBytes += System.Text.Encoding.UTF8.GetByteCount(jData);
                            }
                        }
                        if (totalBytes > 0)
                        {
                            RealTimeCloudStorageUsedMb = Math.Max(RealTimeCloudStorageUsedMb, totalBytes / (1024.0 * 1024.0));
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
                    }

                    using (var db = new LocalDbContext())
                    {
                        if (cloudMemosJsonList.Count > 0)
                        {
                            foreach (var record in cloudMemosJsonList)
                            {
                                var memoNum = record.GetProperty("memo_number").GetString();
                                var jsonData = record.GetProperty("json_data").GetString();
                                var updatedAt = DateTime.SpecifyKind(record.GetProperty("updated_at").GetDateTime(), DateTimeKind.Utc);
                                
                                // Neutralize future-dated timestamps generated by older app versions with timezone offsets
                                var currentTrustedUtc = NetworkTimeService.GetUtcNow();
                                if (updatedAt > currentTrustedUtc.AddMinutes(1))
                                {
                                    updatedAt = currentTrustedUtc.AddSeconds(-5);
                                }

                                if (string.IsNullOrEmpty(memoNum) || string.IsNullOrEmpty(jsonData)) continue;

                                var cloudMemoDto = JsonSerializer.Deserialize<ServiceMemoDto>(jsonData);
                                if (cloudMemoDto == null) continue;

                                // Allow memos belonging to any activation key in the organization workspace
                                if (!string.IsNullOrEmpty(cloudMemoDto.CloudOwnerKey) && !allAllowedKeyCodes.Contains(cloudMemoDto.CloudOwnerKey))
                                {
                                    continue;
                                }

                                // Dynamically query DB to handle concurrent updates and purge any pre-existing duplicates
                                var matches = db.ServiceMemos.Where(m => m.MemoNumber == memoNum).ToList();
                                if (matches.Count > 1)
                                {
                                    var keep = matches.OrderByDescending(m => m.Id).First();
                                    var dupes = matches.Where(m => m.Id != keep.Id).ToList();
                                    db.ServiceMemos.RemoveRange(dupes);
                                    db.SaveChanges();
                                    matches = new List<ServiceMemo> { keep };
                                }

                                var localMemo = matches.FirstOrDefault();

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
                                        UpdatedAt = updatedAt,
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
                                        ItemizedCosts = cloudMemoDto.ItemizedCosts,
                                        ReturnDate = cloudMemoDto.ReturnDate,
                                        IsRepeatedDevice = cloudMemoDto.IsRepeatedDevice
                                    };
                                    db.ServiceMemos.Add(newMemo);
                                    db.SaveChanges();
                                }
                                else if (localMemo.Status == "Deleted" || localMemo.Status == "Deleted_Synced")
                                {
                                    // Protect locally deleted records; if cloud also has tombstone with newer timestamp, align to Deleted_Synced
                                    if ((cloudMemoDto.Status == "Deleted" || cloudMemoDto.Status == "Deleted_Synced") && ToUtc(updatedAt) > ToUtc(localMemo.UpdatedAt))
                                    {
                                        localMemo.Status = "Deleted_Synced";
                                        localMemo.UpdatedAt = updatedAt;
                                        db.ServiceMemos.Update(localMemo);
                                    }
                                }
                                else
                                {
                                      var cloudUtc = updatedAt; // already UTC (DateTimeKind.Utc set above)
                                      var localUtc = ToUtc(localMemo.UpdatedAt);
                                      bool isMobileUpdate = cloudMemoDto.IsMobilePortalUpdate || string.Equals(cloudMemoDto.Source, "MobilePortal", StringComparison.OrdinalIgnoreCase);
                                      bool cloudIsNewer = (cloudUtc > localUtc) || (isMobileUpdate && (cloudMemoDto.Status != localMemo.Status || cloudMemoDto.OrderUpdates != localMemo.OrderUpdates));

                                     if (cloudIsNewer)
                                    {
                                        // Cloud version is newer — pull cloud data into local DB
                                        if (cloudMemoDto.Status == "Deleted" || cloudMemoDto.Status == "Deleted_Synced")
                                        {
                                            localMemo.Status = "Deleted_Synced";
                                            localMemo.UpdatedAt = cloudUtc;
                                            db.ServiceMemos.Update(localMemo);
                                        }
                                        else
                                        {
                                            if (localMemo.Status != "Completed" && cloudMemoDto.Status == "Completed")
                                            {
                                                if (cloudMemoDto.IsMobilePortalUpdate || string.Equals(cloudMemoDto.Source, "MobilePortal", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    CloudOrderCompleted?.Invoke(cloudMemoDto);
                                                }
                                            }

                                            // Create notification if updated from Mobile Portal or status changed
                                            var oldCopy = new ServiceMemo { Status = localMemo.Status, TechnicianName = localMemo.TechnicianName, OrderUpdates = localMemo.OrderUpdates };
                                            
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
                                            localMemo.ItemizedCosts = cloudMemoDto.ItemizedCosts;
                                            localMemo.ReturnDate = cloudMemoDto.ReturnDate;
                                            localMemo.IsRepeatedDevice = cloudMemoDto.IsRepeatedDevice;
                                            localMemo.IsMobilePortalUpdate = cloudMemoDto.IsMobilePortalUpdate;
                                            localMemo.Source = cloudMemoDto.Source;
                                            localMemo.UpdatedAt = cloudUtc;

                                            db.Entry(localMemo).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                            db.ServiceMemos.Update(localMemo);

                                            NotificationManager.TrackStatusUpdate(oldCopy, localMemo);
                                        }
                                    }
                                    // else: local is same age or newer — local wins, push will handle upload
                                }
                            }
                            db.SaveChanges();
                        }

                        // 2. PUSH TO CLOUD (Re-query DB so freshly pulled cloud updates are preserved)
                        var localMemos = db.ServiceMemos.ToList();
                        foreach (var lMemo in localMemos)
                        {
                            // Process soft deletion tombstones
                            if (lMemo.Status == "Deleted")
                            {
                                var uploadDto = ServiceMemoDto.FromModel(lMemo, SettingsManager.Default.SyncImagesEnabled);
                                uploadDto.Status = "Deleted_Synced";

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
                                var delResponse = await _http.SendAsync(request);
                                if (delResponse.IsSuccessStatusCode)
                                {
                                    lMemo.Status = "Deleted_Synced";
                                    db.ServiceMemos.Update(lMemo);
                                }
                                continue;
                            }

                            if (lMemo.Status == "Deleted_Synced")
                            {
                                continue;
                            }

                            // Find corresponding cloud record to compare timestamp using cloudHeaderMap
                            bool needsUpload = true;
                            if (cloudHeaderMap.TryGetValue(lMemo.MemoNumber, out var cUpdatedAt))
                            {
                                var currentTrustedUtc = NetworkTimeService.GetUtcNow();
                                if (cUpdatedAt > currentTrustedUtc.AddMinutes(1))
                                {
                                    cUpdatedAt = currentTrustedUtc.AddSeconds(-5);
                                }

                                // DO NOT upload if local record is not newer than cloud (using 500ms threshold for clock precision)
                                if (ToUtc(lMemo.UpdatedAt) <= ToUtc(cUpdatedAt).AddMilliseconds(500))
                                {
                                    needsUpload = false;
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
            // SQLite stores DateTime strings (read by EF Core with Kind = Unspecified).
            // Since all saved timestamps are stored in UTC, Unspecified represents UTC.
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
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

        public static async Task<int> ForcePushAllLocalToCloudAsync()
        {
            if (string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey)) return 0;
            var _http = SupabaseClientManager.GetHttpClient();
            var ownerKey = SettingsManager.Default.SubscriptionKey;

            var keyResponse = await _http.GetAsync($"/rest/v1/activation_keys?key_code=eq.{Uri.EscapeDataString(ownerKey)}&select=id");
            if (!keyResponse.IsSuccessStatusCode) return 0;
            var keys = await keyResponse.Content.ReadFromJsonAsync<JsonElement[]>();
            if (keys == null || keys.Length == 0) return 0;
            var ownerKeyId = keys[0].GetProperty("id").GetString();

            var deviceIdHardware = LicenseManager.GetDeviceId();
            var deviceResponse = await _http.GetAsync($"/rest/v1/devices?hardware_id=eq.{Uri.EscapeDataString(deviceIdHardware)}&activation_key_id=eq.{ownerKeyId}&select=id");
            if (!deviceResponse.IsSuccessStatusCode) return 0;
            var devices = await deviceResponse.Content.ReadFromJsonAsync<JsonElement[]>();
            string? deviceId = (devices != null && devices.Length > 0) ? devices[0].GetProperty("id").GetString() : null;

            if (string.IsNullOrEmpty(deviceId)) return 0;

            int pushedCount = 0;
            var nowUtc = NetworkTimeService.GetUtcNow();

            using (var db = new LocalDbContext())
            {
                var activeMemos = db.ServiceMemos.Where(m => m.Status != "Deleted" && m.Status != "Deleted_Synced").ToList();
                foreach (var memo in activeMemos)
                {
                    memo.UpdatedAt = nowUtc;
                    memo.CloudOwnerKey = ownerKey;

                    var uploadDto = ServiceMemoDto.FromModel(memo, SettingsManager.Default.SyncImagesEnabled);
                    var payload = new
                    {
                        memo_number = memo.MemoNumber,
                        json_data = JsonSerializer.Serialize(uploadDto),
                        device_id = deviceId,
                        updated_at = nowUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    };

                    var request = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/service_memos?on_conflict=memo_number");
                    request.Headers.Add("Prefer", "resolution=merge-duplicates");
                    request.Content = JsonContent.Create(payload);
                    var res = await _http.SendAsync(request);
                    if (res.IsSuccessStatusCode)
                    {
                        pushedCount++;
                    }
                }
                db.SaveChanges();
            }
            return pushedCount;
        }

        public static async Task<int> ForcePullAllCloudToLocalAsync()
        {
            if (string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey)) return 0;
            var _http = SupabaseClientManager.GetHttpClient();
            var ownerKey = SettingsManager.Default.SubscriptionKey;

            var keyResponse = await _http.GetAsync($"/rest/v1/activation_keys?key_code=eq.{Uri.EscapeDataString(ownerKey)}&select=id,email");
            if (!keyResponse.IsSuccessStatusCode) return 0;
            var keys = await keyResponse.Content.ReadFromJsonAsync<JsonElement[]>();
            if (keys == null || keys.Length == 0) return 0;

            var ownerKeyId = keys[0].GetProperty("id").GetString();
            var ownerEmail = keys[0].GetProperty("email").GetString();
            var allKeyIds = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(ownerKeyId)) allKeyIds.Add(ownerKeyId);

            if (!string.IsNullOrEmpty(ownerEmail))
            {
                try
                {
                    var orgKeysResponse = await _http.GetAsync($"/rest/v1/activation_keys?email=eq.{Uri.EscapeDataString(ownerEmail)}&select=id");
                    if (orgKeysResponse.IsSuccessStatusCode)
                    {
                        var orgKeys = await orgKeysResponse.Content.ReadFromJsonAsync<JsonElement[]>();
                        if (orgKeys != null)
                        {
                            foreach (var ok in orgKeys)
                            {
                                var kId = ok.GetProperty("id").GetString();
                                if (!string.IsNullOrEmpty(kId) && !allKeyIds.Contains(kId)) allKeyIds.Add(kId);
                            }
                        }
                    }
                }
                catch { }
            }

            var inKeysQuery = string.Join(",", allKeyIds);
            var allDevicesResponse = await _http.GetAsync($"/rest/v1/devices?activation_key_id=in.({inKeysQuery})&select=id");

            HttpResponseMessage memosResponse;
            if (allDevicesResponse.IsSuccessStatusCode)
            {
                var allDevices = await allDevicesResponse.Content.ReadFromJsonAsync<JsonElement[]>();
                var deviceIds = allDevices?.Select(d => d.GetProperty("id").GetString()).Where(id => !string.IsNullOrEmpty(id)).ToList();
                if (deviceIds != null && deviceIds.Count > 0)
                {
                    var inQuery = string.Join(",", deviceIds);
                    memosResponse = await _http.GetAsync($"/rest/v1/service_memos?device_id=in.({inQuery})&select=memo_number,json_data,updated_at");
                }
                else
                {
                    memosResponse = await _http.GetAsync("/rest/v1/service_memos?select=memo_number,json_data,updated_at&order=updated_at.desc&limit=1000");
                }
            }
            else
            {
                memosResponse = await _http.GetAsync("/rest/v1/service_memos?select=memo_number,json_data,updated_at&order=updated_at.desc&limit=1000");
            }

            if (!memosResponse.IsSuccessStatusCode) return 0;

            var cloudMemosJson = await memosResponse.Content.ReadFromJsonAsync<JsonElement[]>();
            if (cloudMemosJson == null) return 0;

            int updatedCount = 0;
            using (var db = new LocalDbContext())
            {
                foreach (var record in cloudMemosJson)
                {
                    var memoNum = record.GetProperty("memo_number").GetString();
                    var jsonData = record.GetProperty("json_data").GetString();
                    var updatedAt = DateTime.SpecifyKind(record.GetProperty("updated_at").GetDateTime(), DateTimeKind.Utc);

                    if (string.IsNullOrEmpty(memoNum) || string.IsNullOrEmpty(jsonData)) continue;

                    var cloudMemoDto = JsonSerializer.Deserialize<ServiceMemoDto>(jsonData);
                    if (cloudMemoDto == null) continue;

                    var localMemo = db.ServiceMemos.FirstOrDefault(m => m.MemoNumber == memoNum);
                    if (localMemo == null)
                    {
                        var newMemo = new ServiceMemo
                        {
                            Id = 0,
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
                            UpdatedAt = updatedAt,
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
                            ItemizedCosts = cloudMemoDto.ItemizedCosts,
                            ReturnDate = cloudMemoDto.ReturnDate,
                            IsRepeatedDevice = cloudMemoDto.IsRepeatedDevice
                        };
                        db.ServiceMemos.Add(newMemo);
                        updatedCount++;
                    }
                    else
                    {
                        // Force overwrite local record with cloud data
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
                        localMemo.ItemizedCosts = cloudMemoDto.ItemizedCosts;
                        localMemo.ReturnDate = cloudMemoDto.ReturnDate;
                        localMemo.IsRepeatedDevice = cloudMemoDto.IsRepeatedDevice;
                        localMemo.UpdatedAt = updatedAt;

                        db.Entry(localMemo).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        db.ServiceMemos.Update(localMemo);
                        updatedCount++;
                    }
                }
                db.SaveChanges();
            }
            return updatedCount;
        }

        public static async Task PushSingleMemoAsync(ServiceMemo memo)
        {
            if (memo == null || string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey)) return;
            if (SettingsManager.Default.SyncMode == "LocalOnly" || SettingsManager.Default.IsFullyOfflineMode) return;

            var ownerKey = SettingsManager.Default.SubscriptionKey;
            var nowUtc = NetworkTimeService.GetUtcNow();
            memo.UpdatedAt = nowUtc;
            memo.CloudOwnerKey = ownerKey;

            try
            {
                var _http = SupabaseClientManager.GetHttpClient();
                _http.Timeout = TimeSpan.FromSeconds(5);

                var deviceIdHardware = LicenseManager.GetDeviceId();
                var keyRes = await _http.GetAsync($"/rest/v1/activation_keys?key_code=eq.{Uri.EscapeDataString(ownerKey)}&select=id");
                if (!keyRes.IsSuccessStatusCode) throw new Exception("Cloud unreachable");
                var keys = await keyRes.Content.ReadFromJsonAsync<JsonElement[]>();
                if (keys == null || keys.Length == 0) throw new Exception("Key not found");
                var keyId = keys[0].GetProperty("id").GetString();

                var devRes = await _http.GetAsync($"/rest/v1/devices?hardware_id=eq.{Uri.EscapeDataString(deviceIdHardware)}&activation_key_id=eq.{keyId}&select=id");
                if (!devRes.IsSuccessStatusCode) throw new Exception("Device query failed");
                var devices = await devRes.Content.ReadFromJsonAsync<JsonElement[]>();
                string? deviceId = (devices != null && devices.Length > 0) ? devices[0].GetProperty("id").GetString() : null;

                if (string.IsNullOrEmpty(deviceId)) throw new Exception("Device seat not found");

                var uploadDto = ServiceMemoDto.FromModel(memo, SettingsManager.Default.SyncImagesEnabled);
                var payload = new
                {
                    memo_number = memo.MemoNumber,
                    json_data = JsonSerializer.Serialize(uploadDto),
                    device_id = deviceId,
                    updated_at = nowUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/service_memos?on_conflict=memo_number");
                request.Headers.Add("Prefer", "resolution=merge-duplicates");
                request.Content = JsonContent.Create(payload);

                var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    memo.IsPendingCloudPush = false;
                    using (var db = new LocalDbContext())
                    {
                        var existing = db.ServiceMemos.FirstOrDefault(m => m.MemoNumber == memo.MemoNumber);
                        if (existing != null)
                        {
                            existing.IsPendingCloudPush = false;
                            db.SaveChanges();
                        }
                    }
                    MarkCloudAvailable();
                    return;
                }
                else
                {
                    throw new Exception($"Cloud POST failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogSyncStatus($"Instant Push deferred to offline queue for {memo.MemoNumber}: {ex.Message}");
                memo.IsPendingCloudPush = true;
                using (var db = new LocalDbContext())
                {
                    var existing = db.ServiceMemos.FirstOrDefault(m => m.MemoNumber == memo.MemoNumber);
                    if (existing != null)
                    {
                        existing.IsPendingCloudPush = true;
                        db.SaveChanges();
                    }
                }
                MarkCloudUnavailable();
            }
        }

        public static async Task<int> FlushOfflineQueueAsync()
        {
            if (string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey)) return 0;
            if (SettingsManager.Default.SyncMode == "LocalOnly") return 0;

            int flushedCount = 0;
            using (var db = new LocalDbContext())
            {
                var pendingMemos = db.ServiceMemos.Where(m => m.IsPendingCloudPush).ToList();
                if (pendingMemos.Count == 0) return 0;

                foreach (var memo in pendingMemos)
                {
                    try
                    {
                        await PushSingleMemoAsync(memo);
                        if (!memo.IsPendingCloudPush)
                        {
                            flushedCount++;
                        }
                    }
                    catch { }
                }
            }
            return flushedCount;
        }

        public static Task DeleteOldMemosAsync(int keepCount)
        {
            // Placeholder for now
            return Task.CompletedTask;
        }

    }
}
