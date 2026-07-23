using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClientApp.Data;
using ClientApp.Models;

namespace ClientApp.Services
{
    public class LanPeer
    {
        public string DeviceId { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public IPAddress IPAddress { get; set; } = IPAddress.None;
        public int TcpPort { get; set; }
        public DateTime LastSeen { get; set; }
        public string ActiveDraftId { get; set; } = string.Empty;
    }

    public class LanDiscoveryPacket
    {
        public string SubscriptionKey { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public int TcpPort { get; set; }
        public string ActiveDraftId { get; set; } = string.Empty;
    }

    public static class LanSyncService
    {
        private const int UdpDiscoveryPort = 14000;
        private const int TcpBasePort = 14001;
        private const int TcpMaxPort = 14005;

        private static UdpClient? _udpListener;
        private static UdpClient? _udpSender;
        private static TcpListener? _tcpListener;
        private static CancellationTokenSource? _cts;
        
        private static int _actualTcpPort = TcpBasePort;
        private static string _activeDraftMemoNumber = string.Empty;
        private static bool _isSyncing = false;

        public static ConcurrentDictionary<string, LanPeer> DiscoveredPeers { get; } = new ConcurrentDictionary<string, LanPeer>();
        
        public static event Action? PeersChanged;
        public static event Action<bool>? SyncStateChanged;
        public static event Action<string>? StartupSyncProgressChanged;

        public static bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
        public static int ActualTcpPort => _actualTcpPort;
        public static bool StartupSyncCompleted { get; private set; } = false;

        public static string ActiveDraftMemoNumber
        {
            get => _activeDraftMemoNumber;
            set
            {
                _activeDraftMemoNumber = value;
                BroadcastDiscoveryPacket(); // Broadcast immediately to let peers know we are drafting
            }
        }

        public static void Start()
        {
            if (IsRunning) return;

            // Check if local network sync is enabled in settings
            if (SettingsManager.Default.SyncMode == "InternetOnly")
                return;

            _cts = new CancellationTokenSource();

            // 1. Initialize TCP Listener
            InitializeTcpListener();

            // 2. Initialize UDP Sockets
            InitializeUdpSockets();

            // 3. Start Background Daemons
            Task.Run(() => ListenForDiscoveryPacketsAsync(_cts.Token));
            Task.Run(() => SendDiscoveryPacketsPeriodicAsync(_cts.Token));
            Task.Run(() => AcceptTcpClientsAsync(_cts.Token));
            Task.Run(() => IncrementalSyncDaemonAsync(_cts.Token));
            Task.Run(() => PeerTimeoutCheckAsync(_cts.Token));

            // 4. Start HTTP API Server for Mobile client
            StartHttpApiServer();
        }

        public static void Stop()
        {
            _cts?.Cancel();
            
            try { _udpListener?.Close(); } catch { }
            try { _udpSender?.Close(); } catch { }
            try { _tcpListener?.Stop(); } catch { }

            // Stop HTTP API Server
            StopHttpApiServer();

            _udpListener = null;
            _udpSender = null;
            _tcpListener = null;
            _cts = null;

            DiscoveredPeers.Clear();
            PeersChanged?.Invoke();
        }

        private static void InitializeTcpListener()
        {
            for (int port = TcpBasePort; port <= TcpMaxPort; port++)
            {
                try
                {
                    _tcpListener = new TcpListener(IPAddress.Any, port);
                    _tcpListener.Start();
                    _actualTcpPort = port;
                    System.Diagnostics.Debug.WriteLine($"[LAN SYNC] Bound TCP Server to port {_actualTcpPort}");
                    return;
                }
                catch (SocketException)
                {
                    // Port in use, try next
                }
            }
            throw new Exception("LAN Sync failed to bind to any TCP ports between 14001 and 14005.");
        }

        private static void InitializeUdpSockets()
        {
            try
            {
                _udpListener = new UdpClient();
                _udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, UdpDiscoveryPort));

                _udpSender = new UdpClient();
                _udpSender.EnableBroadcast = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LAN SYNC] UDP Socket init error: {ex.Message}");
            }
        }

        private static async Task SendDiscoveryPacketsPeriodicAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                BroadcastDiscoveryPacket();
                await Task.Delay(5000, token);
            }
        }

        private static void BroadcastDiscoveryPacket()
        {
            if (_udpSender == null || string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey)) return;

            try
            {
                var packet = new LanDiscoveryPacket
                {
                    SubscriptionKey = SettingsManager.Default.SubscriptionKey,
                    DeviceId = LicenseManager.GetDeviceId(),
                    MachineName = Environment.MachineName,
                    TcpPort = _actualTcpPort,
                    ActiveDraftId = _activeDraftMemoNumber
                };

                string json = JsonSerializer.Serialize(packet);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                _udpSender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, UdpDiscoveryPort));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LAN SYNC] UDP Broadcast error: {ex.Message}");
            }
        }

        private static async Task ListenForDiscoveryPacketsAsync(CancellationToken token)
        {
            if (_udpListener == null) return;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpListener.ReceiveAsync(token);
                    string json = Encoding.UTF8.GetString(result.Buffer);
                    
                    var packet = JsonSerializer.Deserialize<LanDiscoveryPacket>(json);
                    if (packet != null && packet.SubscriptionKey == SettingsManager.Default.SubscriptionKey)
                    {
                        string localDeviceId = LicenseManager.GetDeviceId();
                        if (packet.DeviceId != localDeviceId)
                        {
                            var peer = new LanPeer
                            {
                                DeviceId = packet.DeviceId,
                                MachineName = packet.MachineName,
                                IPAddress = result.RemoteEndPoint.Address,
                                TcpPort = packet.TcpPort,
                                LastSeen = DateTime.Now,
                                ActiveDraftId = packet.ActiveDraftId
                            };

                            bool isNew = !DiscoveredPeers.ContainsKey(packet.DeviceId);
                            DiscoveredPeers[packet.DeviceId] = peer;

                            if (isNew)
                            {
                                PeersChanged?.Invoke();
                                // Trigger an instant synchronization with this newly discovered peer
                                _ = Task.Run(() => SyncWithSinglePeerAsync(peer));
                            }
                        }
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    System.Diagnostics.Debug.WriteLine($"[LAN SYNC] UDP Listen error: {ex.Message}");
                }
            }
        }

        private static async Task PeerTimeoutCheckAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool changed = false;
                foreach (var peer in DiscoveredPeers.Values.ToList())
                {
                    if ((DateTime.Now - peer.LastSeen).TotalSeconds > 15)
                    {
                        DiscoveredPeers.TryRemove(peer.DeviceId, out _);
                        changed = true;
                    }
                }

                if (changed)
                {
                    PeersChanged?.Invoke();
                }

                await Task.Delay(3000, token);
            }
        }

        private static async Task AcceptTcpClientsAsync(CancellationToken token)
        {
            if (_tcpListener == null) return;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync(token);
                    _ = Task.Run(() => HandleTcpClientAsync(client));
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    System.Diagnostics.Debug.WriteLine($"[LAN SYNC] TCP Accept error: {ex.Message}");
                }
            }
        }

        private static async Task HandleTcpClientAsync(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
            {
                try
                {
                    string? line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) return;

                    var parts = line.Split('|', 2);
                    string cmd = parts[0];
                    string payload = parts.Length > 1 ? parts[1] : string.Empty;

                    switch (cmd)
                    {
                        case "GetRecentMemos":
                            using (var db = new LocalDbContext())
                            {
                                var memos = db.ServiceMemos
                                    .Select(m => new { m.MemoNumber, m.UpdatedAt, m.Status })
                                    .ToList();
                                string json = JsonSerializer.Serialize(memos);
                                await writer.WriteLineAsync(json);
                            }
                            break;

                        case "GetMemoData":
                            using (var db = new LocalDbContext())
                            {
                                var memo = db.ServiceMemos.FirstOrDefault(m => m.MemoNumber == payload);
                                if (memo != null)
                                {
                                    var dto = ServiceMemoDto.FromModel(memo);
                                    string json = JsonSerializer.Serialize(dto);
                                    await writer.WriteLineAsync(json);
                                }
                                else
                                {
                                    await writer.WriteLineAsync("NULL");
                                }
                            }
                            break;

                        case "PushMemoData":
                            var pushedMemoDto = JsonSerializer.Deserialize<ServiceMemoDto>(payload);
                            if (pushedMemoDto != null)
                            {
                                bool applied = MergePushedMemo(pushedMemoDto);
                                await writer.WriteLineAsync(applied ? "OK" : "SKIP");
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LAN SYNC] TCP Handler error: {ex.Message}");
                }
            }
        }



        private static readonly object _syncLock = new object();

        private static bool MergePushedMemo(ServiceMemoDto remote)
        {
            var currentTrustedUtc = NetworkTimeService.GetUtcNow();
            if (remote.UpdatedAt > currentTrustedUtc.AddMinutes(1))
            {
                remote.UpdatedAt = currentTrustedUtc.AddSeconds(-5);
            }

            lock (_syncLock)
            {
                using (var db = new LocalDbContext())
                {
                    db.Migrate();

                    var matches = db.ServiceMemos.Where(m => m.MemoNumber == remote.MemoNumber).ToList();
                    if (matches.Count > 1)
                    {
                        var keep = matches.OrderByDescending(m => m.Id).First();
                        var dupes = matches.Where(m => m.Id != keep.Id).ToList();
                        db.ServiceMemos.RemoveRange(dupes);
                        db.SaveChanges();
                        matches = new List<ServiceMemo> { keep };
                    }

                    var local = matches.FirstOrDefault();
                    if (local == null)
                    {
                        var remoteModel = new ServiceMemo
                        {
                            Id = 0, // SQLite autoincrement
                            MemoNumber = remote.MemoNumber,
                            CustomerName = remote.CustomerName,
                            PhoneNumber = remote.PhoneNumber,
                            DeviceName = remote.DeviceName,
                            DeviceModel = remote.DeviceModel,
                            IssueDescription = remote.IssueDescription,
                            Status = remote.Status,
                            CreatedAt = remote.CreatedAt,
                            EstimatedCost = remote.EstimatedCost,
                            CustomerAddress = remote.CustomerAddress,
                            Phone1 = remote.Phone1,
                            Phone2 = remote.Phone2,
                            TechnicianName = remote.TechnicianName,
                            Brand = remote.Brand,
                            SerialNumber = remote.SerialNumber,
                            Accessories = remote.Accessories,
                            Diagnostics = remote.Diagnostics,
                            OrderUpdates = remote.OrderUpdates,
                            ItemizedCosts = remote.ItemizedCosts,
                            ReturnDate = remote.ReturnDate,
                            IsRepeatedDevice = remote.IsRepeatedDevice,
                            UpdatedAt = remote.UpdatedAt,
                            ImagePath = SettingsManager.Default.SyncImagesEnabled ? remote.ImagePath : string.Empty
                        };
                        db.ServiceMemos.Add(remoteModel);
                        db.SaveChanges();
                        System.Diagnostics.Debug.WriteLine($"[LAN SYNC] Integrated new LAN order: {remote.MemoNumber}");
                        return true;
                    }
                    else
                    {
                        // Use a 2-second grace window: the remote must be clearly newer before we overwrite.
                        bool remoteIsNewer = remote.UpdatedAt > local.UpdatedAt.AddSeconds(2);
                        if (remoteIsNewer)
                        {
                            local.CustomerName = remote.CustomerName;
                            local.PhoneNumber = remote.PhoneNumber;
                            local.DeviceName = remote.DeviceName;
                            local.DeviceModel = remote.DeviceModel;
                            local.IssueDescription = remote.IssueDescription;
                            local.Status = remote.Status;
                            local.EstimatedCost = remote.EstimatedCost;
                            local.CustomerAddress = remote.CustomerAddress;
                            local.Phone1 = remote.Phone1;
                            local.Phone2 = remote.Phone2;
                            local.TechnicianName = remote.TechnicianName;
                            local.Brand = remote.Brand;
                            local.SerialNumber = remote.SerialNumber;
                            local.Accessories = remote.Accessories;
                            local.Diagnostics = remote.Diagnostics;
                            local.OrderUpdates = remote.OrderUpdates;
                            local.ItemizedCosts = remote.ItemizedCosts;
                            local.ReturnDate = remote.ReturnDate;
                            local.IsRepeatedDevice = remote.IsRepeatedDevice;
                            local.UpdatedAt = remote.UpdatedAt;

                            // Only overwrite image if settings allow
                            if (SettingsManager.Default.SyncImagesEnabled)
                            {
                                local.ImagePath = remote.ImagePath;
                            }

                            db.Entry(local).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            db.ServiceMemos.Update(local);
                            db.SaveChanges();
                            System.Diagnostics.Debug.WriteLine($"[LAN SYNC] Updated existing local order from LAN: {remote.MemoNumber}");
                            return true;
                        }
                        return false;
                    }
                }
            }
        }

        public static async Task BroadcastMemoSavedAsync(ServiceMemo memo)
        {
            // Fallback Architecture: If Cloud Sync is enabled & online, Cloud is primary sync engine.
            // LAN Wi-Fi broadcast steps aside to prevent dual-channel race conditions and duplicates.
            if (SettingsManager.Default.SyncMode != "LocalOnly" && !CloudSyncService.IsCloudOffline)
            {
                return;
            }

            var peers = DiscoveredPeers.Values.ToList();
            if (peers.Count == 0) return;

            TriggerSyncState(true);
            var tasks = peers.Select(async peer =>
            {
                try
                {
                    using (var client = new TcpClient())
                    {
                        var connectTask = client.ConnectAsync(peer.IPAddress, peer.TcpPort);
                        var timeoutTask = Task.Delay(1500);
                        var completed = await Task.WhenAny(connectTask, timeoutTask);
                        if (completed != connectTask) return;

                        using (var stream = client.GetStream())
                        using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
                        {
                            var uploadDto = ServiceMemoDto.FromModel(memo, SettingsManager.Default.SyncImagesEnabled);
                            string json = JsonSerializer.Serialize(uploadDto);
                            await writer.WriteLineAsync($"PushMemoData|{json}");
                        }
                    }
                }
                catch
                {
                    // Ignore peer transmission exceptions
                }
            });

            await Task.WhenAll(tasks);
            TriggerSyncState(false);
        }

        private static async Task IncrementalSyncDaemonAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(10000, token);

                if (_isSyncing || DiscoveredPeers.Count == 0) continue;

                // Fallback Architecture: If Cloud Sync is enabled & online, Cloud handles sync.
                // LAN background daemon skips active peer polling when Cloud is online.
                if (SettingsManager.Default.SyncMode != "LocalOnly" && !CloudSyncService.IsCloudOffline)
                {
                    continue;
                }

                try
                {
                    TriggerSyncState(true);
                    var peers = DiscoveredPeers.Values.ToList();
                    foreach (var peer in peers)
                    {
                        await SyncWithSinglePeerAsync(peer);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LAN SYNC] Daemon sync error: {ex.Message}");
                }
                finally
                {
                    TriggerSyncState(false);
                }
            }
        }

        public static async Task SyncWithSinglePeerAsync(LanPeer peer)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync(peer.IPAddress, peer.TcpPort);
                    var timeoutTask = Task.Delay(2000);
                    var completed = await Task.WhenAny(connectTask, timeoutTask);
                    if (completed != connectTask) return; // Timeout

                    List<PeerMemoMeta>? peerMemos = null;

                    using (var stream = client.GetStream())
                    using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        await writer.WriteLineAsync("GetRecentMemos");
                        string? json = await reader.ReadLineAsync();
                        if (json != null)
                        {
                            peerMemos = JsonSerializer.Deserialize<List<PeerMemoMeta>>(json);
                        }
                    }

                    if (peerMemos == null || peerMemos.Count == 0) return;

                    using (var db = new LocalDbContext())
                    {
                        var localMemos = db.ServiceMemos.Select(m => new { m.MemoNumber, m.UpdatedAt, m.Status }).ToList();

                        // 1. Pull missing/outdated from peer
                        foreach (var pm in peerMemos)
                        {
                            var lm = localMemos.FirstOrDefault(m => m.MemoNumber == pm.MemoNumber);
                            // Use 2-second grace: only pull if peer record is clearly newer
                            if (lm == null || pm.UpdatedAt > lm.UpdatedAt.AddSeconds(2))
                            {
                                await PullMemoFromPeerAsync(peer, pm.MemoNumber);
                            }
                        }

                        // 2. Push missing/outdated to peer
                        var refreshedLocalMemos = db.ServiceMemos.ToList();
                        foreach (var lm in refreshedLocalMemos)
                        {
                            var pm = peerMemos.FirstOrDefault(m => m.MemoNumber == lm.MemoNumber);
                            if (pm == null || lm.UpdatedAt > pm.UpdatedAt)
                            {
                                await PushMemoToPeerAsync(peer, lm);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LAN SYNC] Peer direct sync failed: {ex.Message}");
            }
        }

        private static async Task PullMemoFromPeerAsync(LanPeer peer, string memoNumber)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(peer.IPAddress, peer.TcpPort);
                    using (var stream = client.GetStream())
                    using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        await writer.WriteLineAsync($"GetMemoData|{memoNumber}");
                        string? json = await reader.ReadLineAsync();
                        if (!string.IsNullOrEmpty(json) && json != "NULL")
                        {
                            var remoteMemoDto = JsonSerializer.Deserialize<ServiceMemoDto>(json);
                            if (remoteMemoDto != null)
                            {
                                MergePushedMemo(remoteMemoDto);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static async Task PushMemoToPeerAsync(LanPeer peer, ServiceMemo localMemo)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(peer.IPAddress, peer.TcpPort);
                    using (var stream = client.GetStream())
                    using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
                    {
                        var uploadDto = ServiceMemoDto.FromModel(localMemo, SettingsManager.Default.SyncImagesEnabled);
                        string json = JsonSerializer.Serialize(uploadDto);
                        await writer.WriteLineAsync($"PushMemoData|{json}");
                    }
                }
            }
            catch { }
        }

        private static void TriggerSyncState(bool active)
        {
            _isSyncing = active;
            SyncStateChanged?.Invoke(active);
        }

        /// <summary>
        /// Called once on app startup. Waits up to <paramref name="discoveryWindowMs"/> ms for peer
        /// discovery, then performs a full bidirectional sync with every discovered peer.
        /// Fires <see cref="StartupSyncProgressChanged"/> with human-readable progress strings.
        /// Returns the number of records integrated from peers.
        /// </summary>
        public static async Task<int> PerformStartupSyncAsync(int discoveryWindowMs = 7000, CancellationToken token = default)
        {
            int totalIntegrated = 0;
            try
            {
                // ── Step 1: Wait for peer discovery ──────────────────────────────────
                StartupSyncProgressChanged?.Invoke("Looking for other workstations on your network…");

                int elapsed = 0;
                const int pollInterval = 500;
                bool firstPeerFound = false;
                int quietMs = 0; // ms since last new peer was discovered

                while (elapsed < discoveryWindowMs && !token.IsCancellationRequested)
                {
                    await Task.Delay(pollInterval, token).ConfigureAwait(false);
                    elapsed += pollInterval;

                    if (DiscoveredPeers.Count > 0)
                    {
                        if (!firstPeerFound)
                        {
                            firstPeerFound = true;
                            quietMs = 0;
                            int count = DiscoveredPeers.Count;
                            StartupSyncProgressChanged?.Invoke(
                                $"Found {count} workstation{(count == 1 ? "" : "s")} on your network. Preparing sync…");
                        }
                        else
                        {
                            quietMs += pollInterval;
                            // Exit early once we have been quiet for 2.5 s after first peer
                            if (quietMs >= 2500) break;
                        }
                    }
                }

                if (DiscoveredPeers.Count == 0 || token.IsCancellationRequested)
                {
                    StartupSyncProgressChanged?.Invoke("No other workstations found. Starting fresh.");
                    await Task.Delay(800, CancellationToken.None).ConfigureAwait(false);
                    return 0;
                }

                // ── Step 2: Sync with every peer ─────────────────────────────────────
                var peers = DiscoveredPeers.Values.ToList();
                int peerIndex = 0;
                foreach (var peer in peers)
                {
                    if (token.IsCancellationRequested) break;
                    peerIndex++;
                    StartupSyncProgressChanged?.Invoke(
                        $"Syncing with {peer.MachineName} ({peerIndex}/{peers.Count})…");

                    int before = CountLocalMemos();
                    await SyncWithSinglePeerAsync(peer).ConfigureAwait(false);
                    int after = CountLocalMemos();
                    totalIntegrated += Math.Max(0, after - before);
                }

                // ── Step 3: Report result ─────────────────────────────────────────────
                string resultMsg = totalIntegrated > 0
                    ? $"Sync complete! {totalIntegrated} new record{(totalIntegrated == 1 ? "" : "s")} received."
                    : "All records are already up to date.";
                StartupSyncProgressChanged?.Invoke(resultMsg);
                await Task.Delay(1200, CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Skipped by user — that's fine
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LAN SYNC] Startup sync error: {ex.Message}");
                StartupSyncProgressChanged?.Invoke("Sync encountered an error. Continuing…");
                await Task.Delay(800, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                StartupSyncCompleted = true;
            }
            return totalIntegrated;
        }

        /// <summary>Mark startup sync as done without waiting (called when user hits Skip).</summary>
        public static void MarkStartupSyncComplete()
        {
            StartupSyncCompleted = true;
        }

        private static int CountLocalMemos()
        {
            try
            {
                using var db = new LocalDbContext();
                return db.ServiceMemos.Count(m => m.Status != "Deleted" && m.Status != "Deleted_Synced");
            }
            catch { return 0; }
        }

        private static HttpListener? _httpListener;
        private const int HttpApiPort = 14010;

        public static void StartHttpApiServer()
        {
            if (_httpListener != null) return; // Already running

            try
            {
                _httpListener = new HttpListener();
                
                // Get all local IP addresses to bind to them
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        _httpListener.Prefixes.Add($"http://{ip}:{HttpApiPort}/");
                    }
                }
                _httpListener.Prefixes.Add($"http://localhost:{HttpApiPort}/");
                _httpListener.Prefixes.Add($"http://127.0.0.1:{HttpApiPort}/");

                _httpListener.Start();
                Task.Run(() => AcceptHttpRequestsAsync(_cts!.Token));
                System.Diagnostics.Debug.WriteLine($"[LAN SYNC] Bound HTTP API Server to port {HttpApiPort}");
            }
            catch (HttpListenerException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LAN SYNC] Access denied on external bind. Falling back to localhost: {ex.Message}");
                try
                {
                    _httpListener?.Close();
                    _httpListener = new HttpListener();
                    _httpListener.Prefixes.Add($"http://localhost:{HttpApiPort}/");
                    _httpListener.Prefixes.Add($"http://127.0.0.1:{HttpApiPort}/");

                    _httpListener.Start();
                    Task.Run(() => AcceptHttpRequestsAsync(_cts!.Token));
                    System.Diagnostics.Debug.WriteLine($"[LAN SYNC] Fallback HTTP API Server bound to localhost/127.0.0.1");
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[LAN SYNC] HTTP API Server fallback error: {fallbackEx.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LAN SYNC] HTTP API Server init error: {ex.Message}");
            }
        }

        public static void StopHttpApiServer()
        {
            try { _httpListener?.Stop(); _httpListener?.Close(); } catch { }
            _httpListener = null;
        }

        private static async Task AcceptHttpRequestsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _httpListener != null && _httpListener.IsListening)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(() => HandleHttpRequestAsync(context));
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException || ex is HttpListenerException) break;
                }
            }
        }

        private static async Task HandleHttpRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Handle CORS for browser requests
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            try
            {
                var path = request.Url?.AbsolutePath.ToLower();
                if (path == "/api/config" && request.HttpMethod == "GET")
                {
                    var licenseManager = new LicenseManager();
                    var payload = new
                    {
                        subscription_key = SettingsManager.Default.SubscriptionKey,
                        key_id = licenseManager.GetCurrentKeyId(),
                        cloud_sync_enabled = SettingsManager.Default.IsCloudSyncEnabled,
                        device_id = LicenseManager.GetDeviceId(),
                        company_name = SettingsManager.Default.CompanyName
                    };
                    string json = JsonSerializer.Serialize(payload);
                    SendHttpResponse(response, 200, json);
                    return;
                }

                if (path == "/api/company/logo" && request.HttpMethod == "GET")
                {
                    string logoPath = SettingsManager.Default.CompanyLogoPath;
                    if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                    {
                        try
                        {
                            byte[] bytes = File.ReadAllBytes(logoPath);
                            response.StatusCode = 200;
                            string ext = Path.GetExtension(logoPath).ToLower();
                            response.ContentType = ext == ".png" ? "image/png" : 
                                                   (ext == ".jpg" || ext == ".jpeg") ? "image/jpeg" : 
                                                   "application/octet-stream";
                            response.ContentLength64 = bytes.Length;
                            response.OutputStream.Write(bytes, 0, bytes.Length);
                            response.OutputStream.Close();
                            return;
                        }
                        catch (Exception ex)
                        {
                            SendHttpResponse(response, 500, $"{{\"error\":\"{ex.Message}\"}}");
                            return;
                        }
                    }
                    else
                    {
                        SendHttpResponse(response, 404, "{\"error\":\"Logo not found\"}");
                        return;
                    }
                }

                if (path == "/api/memo" && request.HttpMethod == "GET")
                {
                    var memoNumber = request.QueryString["id"];
                    if (string.IsNullOrEmpty(memoNumber))
                    {
                        SendHttpResponse(response, 400, "{\"error\":\"Missing id parameter\"}");
                        return;
                    }

                    using (var db = new LocalDbContext())
                    {
                        var cleanId = memoNumber.Trim();
                        var memo = db.ServiceMemos.FirstOrDefault(m => 
                            string.Equals(m.MemoNumber, cleanId, StringComparison.OrdinalIgnoreCase) && m.Status != "Deleted" && m.Status != "Deleted_Synced");
                        if (memo != null)
                        {
                            var dto = ServiceMemoDto.FromModel(memo);
                            string json = JsonSerializer.Serialize(dto);
                            SendHttpResponse(response, 200, json);
                        }
                        else
                        {
                            SendHttpResponse(response, 404, "{\"error\":\"Memo not found\"}");
                        }
                    }
                }
                else if (path == "/api/memo/status" && request.HttpMethod == "POST")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        var body = await reader.ReadToEndAsync();
                        var updateReq = JsonSerializer.Deserialize<StatusUpdateRequest>(body);
                        if (updateReq == null || string.IsNullOrEmpty(updateReq.MemoNumber) || string.IsNullOrEmpty(updateReq.Status))
                        {
                            SendHttpResponse(response, 400, "{\"error\":\"Invalid payload\"}");
                            return;
                        }

                        using (var db = new LocalDbContext())
                        {
                            var cleanId = updateReq.MemoNumber.Trim();
                            var memo = db.ServiceMemos.FirstOrDefault(m => 
                                string.Equals(m.MemoNumber, cleanId, StringComparison.OrdinalIgnoreCase) && m.Status != "Deleted" && m.Status != "Deleted_Synced");
                            if (memo != null)
                            {
                                memo.Status = updateReq.Status;
                                memo.TechnicianName = updateReq.TechnicianName;
                                if (updateReq.OrderUpdates != null)
                                {
                                    memo.OrderUpdates = updateReq.OrderUpdates;
                                }
                                var nowUtc = NetworkTimeService.GetUtcNow();
                                memo.UpdatedAt = nowUtc > memo.UpdatedAt ? nowUtc : memo.UpdatedAt.AddSeconds(1);
                                db.Entry(memo).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                db.ServiceMemos.Update(memo);
                                db.SaveChanges();

                                // 1. Broadcast to other LAN peers instantly
                                _ = Task.Run(() => BroadcastMemoSavedAsync(memo));

                                // 2. Sync with cloud in the background if enabled
                                if (SettingsManager.Default.SyncMode != "LocalOnly" && !string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey))
                                {
                                    _ = Task.Run(() => CloudSyncService.SyncWithCloudAsync());
                                }

                                // 3. Dispatch an immediate UI reload back to the main WPF thread
                                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (System.Windows.Application.Current.MainWindow is MainWindow mainWin)
                                    {
                                        mainWin.LoadData(); // Triggers reloading from SQLite db
                                        
                                        if (updateReq.Status == "Completed")
                                        {
                                            MainWindow.ShowOrderCompletedNotification(memo.MemoNumber, memo.DeviceName, memo.DeviceModel, memo.TechnicianName);
                                        }
                                    }
                                }));

                                SendHttpResponse(response, 200, "{\"success\":true}");
                            }
                            else
                            {
                                SendHttpResponse(response, 404, "{\"error\":\"Memo not found\"}");
                            }
                        }
                    }
                }
                else if (path == "/api/clear-workspace" && request.HttpMethod == "POST")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        var body = await reader.ReadToEndAsync();
                        var clearReq = JsonSerializer.Deserialize<ClearWorkspaceRequest>(body);
                        if (clearReq == null || string.IsNullOrEmpty(clearReq.KeyCode))
                        {
                            SendHttpResponse(response, 400, "{\"error\":\"Invalid payload: KeyCode is required.\"}");
                            return;
                        }

                        var keyCode = clearReq.KeyCode;
                        try
                        {
                            // 1. Back up existing records for that key in LocalDbContext to Documents/joborgen/service memo generator/backups
                            var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                            var backupDir = Path.Combine(docsPath, "joborgen", "service memo generator", "backups");
                            Directory.CreateDirectory(backupDir);

                            var backupFileName = $"Backup_{keyCode}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                            var backupPath = Path.Combine(backupDir, backupFileName);

                            using (var db = new LocalDbContext())
                            {
                                var memosToBackup = db.ServiceMemos.Where(m => m.CloudOwnerKey == keyCode).ToList();
                                
                                // Serialize memos to JSON file
                                var backupJson = JsonSerializer.Serialize(memosToBackup, new JsonSerializerOptions { WriteIndented = true });
                                File.WriteAllText(backupPath, backupJson);

                                // 2. Delete the local memos for that key
                                if (memosToBackup.Any())
                                {
                                    db.ServiceMemos.RemoveRange(memosToBackup);
                                    db.SaveChanges();
                                }
                            }

                            // 3. If the currently active license matches the cleared key, wipe settings and reload UI
                            if (SettingsManager.Default.SubscriptionKey == keyCode)
                            {
                                SettingsManager.Default.SubscriptionKey = string.Empty;
                                SettingsManager.Default.CloudUserEmail = string.Empty;
                                SettingsManager.Default.IsCloudSyncEnabled = false;
                                SettingsManager.Default.SyncMode = "LocalOnly";
                                SettingsManager.Save();

                                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (System.Windows.Application.Current.MainWindow is MainWindow mainWin)
                                    {
                                        mainWin.LoadData();
                                    }
                                }));
                            }

                            SendHttpResponse(response, 200, "{\"status\":\"success\",\"message\":\"Backup taken and local data cleared successfully.\"}");
                            return;
                        }
                        catch (Exception ex)
                        {
                            SendHttpResponse(response, 500, $"{{\"error\":\"Backup or clearance failed: {ex.Message}\"}}");
                            return;
                        }
                    }
                }
                else
                {
                    SendHttpResponse(response, 404, "{\"error\":\"Endpoint not found\"}");
                }
            }
            catch (Exception ex)
            {
                SendHttpResponse(response, 500, $"{{\"error\":\"{ex.Message}\"}}");
            }
        }

        private static void SendHttpResponse(HttpListenerResponse response, int statusCode, string content)
        {
            try
            {
                response.StatusCode = statusCode;
                response.ContentType = "application/json";
                byte[] buffer = Encoding.UTF8.GetBytes(content);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch { }
        }

        private class StatusUpdateRequest
        {
            public string MemoNumber { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string TechnicianName { get; set; } = string.Empty;
            public string? OrderUpdates { get; set; }
        }

        private class PeerMemoMeta
        {
            public string MemoNumber { get; set; } = string.Empty;
            public DateTime UpdatedAt { get; set; }
            public string Status { get; set; } = string.Empty;
        }
        private class ClearWorkspaceRequest
        {
            public string KeyCode { get; set; } = string.Empty;
        }
    }
}
