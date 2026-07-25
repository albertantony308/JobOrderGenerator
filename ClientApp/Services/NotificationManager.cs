using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClientApp.Models;

namespace ClientApp.Services
{
    public class NotificationItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MemoNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string StaffName { get; set; } = "Mobile Staff Portal";
        public string OldStatus { get; set; } = "Pending";
        public string NewStatus { get; set; } = "Updated";
        public string UpdateNotes { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
        public string Source { get; set; } = "MobilePortal";

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - Timestamp;
                if (diff.TotalSeconds < 60) return "Just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                return Timestamp.ToString("MMM dd, hh:mm tt");
            }
        }

        public System.Windows.Visibility HasNotesVisibility => 
            string.IsNullOrWhiteSpace(UpdateNotes) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    }

    public static class NotificationManager
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ServiceMemoApp",
            "notifications.json"
        );

        private static List<NotificationItem> _notifications = new();
        public static event Action? OnNotificationsUpdated;

        static NotificationManager()
        {
            Load();
        }

        public static List<NotificationItem> GetAll()
        {
            lock (_notifications)
            {
                return _notifications.OrderByDescending(n => n.Timestamp).ToList();
            }
        }

        public static int GetUnreadCount()
        {
            lock (_notifications)
            {
                return _notifications.Count(n => !n.IsRead);
            }
        }

        public static void AddNotification(NotificationItem item)
        {
            lock (_notifications)
            {
                if (_notifications.Any(n => n.MemoNumber == item.MemoNumber && n.NewStatus == item.NewStatus && Math.Abs((n.Timestamp - item.Timestamp).TotalSeconds) < 10))
                {
                    return;
                }
                _notifications.Add(item);
                Save();
            }
            OnNotificationsUpdated?.Invoke();
        }

        public static void MarkAllAsRead()
        {
            lock (_notifications)
            {
                foreach (var n in _notifications)
                {
                    n.IsRead = true;
                }
                Save();
            }
            OnNotificationsUpdated?.Invoke();
        }

        public static void ClearAll()
        {
            lock (_notifications)
            {
                _notifications.Clear();
                Save();
            }
            OnNotificationsUpdated?.Invoke();
        }

        public static void TrackStatusUpdate(ServiceMemo? oldMemo, ServiceMemo newMemo)
        {
            if (newMemo == null) return;

            bool isMobile = newMemo.IsMobilePortalUpdate || 
                            string.Equals(newMemo.Source, "MobilePortal", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(newMemo.Source, "mobile_html", StringComparison.OrdinalIgnoreCase);

            // STRICT RULE 1: ONLY notify if update comes from mobile.html staff portal!
            if (!isMobile) return;

            string staffName = string.IsNullOrWhiteSpace(newMemo.TechnicianName) ? "Staff Member" : newMemo.TechnicianName;
            string oldStatus = oldMemo?.Status ?? "Pending";
            string newStatus = newMemo.Status;
            string oldNotes = oldMemo?.OrderUpdates ?? string.Empty;
            string newNotes = newMemo.OrderUpdates ?? string.Empty;

            // STRICT RULE 2: DO NOT notify if status and notes have NOT changed!
            if (string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(oldNotes, newNotes, StringComparison.Ordinal))
            {
                return;
            }

            lock (_notifications)
            {
                // DEDUPLICATION: Do not create duplicate notification for same MemoNumber, NewStatus, and Notes within last 60 seconds
                bool isDuplicate = _notifications.Any(n => 
                    string.Equals(n.MemoNumber, newMemo.MemoNumber, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(n.NewStatus, newStatus, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(n.UpdateNotes ?? "", newNotes, StringComparison.Ordinal) &&
                    (DateTime.Now - n.Timestamp).TotalSeconds < 60);

                if (isDuplicate) return;
            }

            var item = new NotificationItem
            {
                MemoNumber = newMemo.MemoNumber,
                CustomerName = newMemo.CustomerName,
                DeviceName = string.IsNullOrWhiteSpace(newMemo.DeviceName) ? newMemo.DeviceModel : newMemo.DeviceName,
                StaffName = staffName,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                UpdateNotes = newMemo.OrderUpdates,
                Timestamp = DateTime.Now,
                IsRead = false,
                Source = "MobilePortal"
            };

            AddNotification(item);
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var list = JsonSerializer.Deserialize<List<NotificationItem>>(json);
                    if (list != null)
                    {
                        _notifications = list;
                    }
                }
            }
            catch { }
        }

        private static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = JsonSerializer.Serialize(_notifications, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}
