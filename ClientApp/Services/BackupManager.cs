using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using ClientApp.Data;
using ClientApp.Models;

namespace ClientApp.Services
{
    public static class BackupManager
    {
        public static void ExportBackup()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "JSON Backup File (*.json)|*.json",
                Title = "Export Local Database Backup",
                FileName = $"ServiceMemoBackup_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (var db = new LocalDbContext())
                    {
                        var memos = db.ServiceMemos.Where(m => m.Status != "Deleted").ToList();
                        var dtos = memos.Select(m => ServiceMemoDto.FromModel(m)).ToList();
                        var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(sfd.FileName, json);
                    }
                    MessageBox.Show("Backup exported successfully.", "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting backup: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public static void ImportBackup()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "JSON Backup File (*.json)|*.json",
                Title = "Import Local Database Backup"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(ofd.FileName);
                    var importedDtos = JsonSerializer.Deserialize<ServiceMemoDto[]>(json);

                    if (importedDtos != null && importedDtos.Length > 0)
                    {
                        using (var db = new LocalDbContext())
                        {
                            foreach (var dto in importedDtos)
                            {
                                // Assign new ID to prevent conflicts, or match if needed.
                                // We'll just check by MemoNumber
                                var existing = db.ServiceMemos.FirstOrDefault(m => m.MemoNumber == dto.MemoNumber);
                                if (existing == null)
                                {
                                    var newMemo = new ServiceMemo
                                    {
                                        Id = 0,
                                        MemoNumber = dto.MemoNumber,
                                        CustomerName = dto.CustomerName,
                                        PhoneNumber = dto.PhoneNumber,
                                        DeviceName = dto.DeviceName,
                                        DeviceModel = dto.DeviceModel,
                                        IssueDescription = dto.IssueDescription,
                                        Status = dto.Status,
                                        CreatedAt = dto.CreatedAt,
                                        EstimatedCost = dto.EstimatedCost,
                                        ImagePath = dto.ImagePath,
                                        UpdatedAt = dto.UpdatedAt,
                                        CloudId = dto.CloudId,
                                        CloudOwnerKey = dto.CloudOwnerKey,
                                        CustomerAddress = dto.CustomerAddress,
                                        Phone1 = dto.Phone1,
                                        Phone2 = dto.Phone2,
                                        TechnicianName = dto.TechnicianName,
                                        Brand = dto.Brand,
                                        SerialNumber = dto.SerialNumber,
                                        Accessories = dto.Accessories,
                                        Diagnostics = dto.Diagnostics,
                                        OrderUpdates = dto.OrderUpdates,
                                        ReturnDate = dto.ReturnDate
                                    };
                                    db.ServiceMemos.Add(newMemo);
                                }
                                else
                                {
                                    // Update existing
                                    existing.CustomerName = dto.CustomerName;
                                    existing.PhoneNumber = dto.PhoneNumber;
                                    existing.DeviceName = dto.DeviceName;
                                    existing.DeviceModel = dto.DeviceModel;
                                    existing.IssueDescription = dto.IssueDescription;
                                    existing.Status = dto.Status;
                                    existing.EstimatedCost = dto.EstimatedCost;
                                    existing.ImagePath = dto.ImagePath;
                                    existing.UpdatedAt = dto.UpdatedAt;
                                    existing.CloudId = dto.CloudId;
                                    existing.CloudOwnerKey = dto.CloudOwnerKey;
                                    existing.CustomerAddress = dto.CustomerAddress;
                                    existing.Phone1 = dto.Phone1;
                                    existing.Phone2 = dto.Phone2;
                                    existing.TechnicianName = dto.TechnicianName;
                                    existing.Brand = dto.Brand;
                                    existing.SerialNumber = dto.SerialNumber;
                                    existing.Accessories = dto.Accessories;
                                    existing.Diagnostics = dto.Diagnostics;
                                    existing.OrderUpdates = dto.OrderUpdates;
                                    existing.ReturnDate = dto.ReturnDate;
                                }
                            }
                            db.SaveChanges();
                        }
                        MessageBox.Show("Backup imported successfully. Please refresh the dashboard.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing backup: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private static DateTime _lastBackupTime = DateTime.UtcNow;
        private static System.Threading.Timer? _autoBackupTimer;

        public static void InitializeAutoBackup()
        {
            _lastBackupTime = DateTime.UtcNow;
            _autoBackupTimer?.Dispose();
            _autoBackupTimer = new System.Threading.Timer(OnAutoBackupTimerTick, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        private static void OnAutoBackupTimerTick(object? state)
        {
            if (!SettingsManager.Default.IsAutoBackupEnabled) return;

            var interval = SettingsManager.Default.AutoBackupIntervalMinutes;
            if (interval <= 0) interval = 10;

            if (DateTime.UtcNow - _lastBackupTime >= TimeSpan.FromMinutes(interval))
            {
                ExecuteAutoBackup();
                _lastBackupTime = DateTime.UtcNow;
            }
        }

        public static void ExecuteAutoBackup()
        {
            if (!SettingsManager.Default.IsAutoBackupEnabled) return;

            try
            {
                var backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp", "backups");
                Directory.CreateDirectory(backupDir);

                using (var db = new LocalDbContext())
                {
                    var memos = db.ServiceMemos.Where(m => m.Status != "Deleted").ToList();
                    var dtos = memos.Select(m => ServiceMemoDto.FromModel(m)).ToList();
                    var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                    
                    var fileName = Path.Combine(backupDir, $"AutoBackup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.WriteAllText(fileName, json);
                }

                var dirInfo = new DirectoryInfo(backupDir);
                var files = dirInfo.GetFiles("AutoBackup_*.json")
                                   .OrderByDescending(f => f.CreationTime)
                                   .Skip(5)
                                   .ToList();

                foreach (var file in files)
                {
                    try { file.Delete(); } catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto Backup Error: {ex.Message}");
            }
        }
    }
}
