using Microsoft.EntityFrameworkCore;
using ClientApp.Models;
using System.IO;
using System;

namespace ClientApp.Data
{
    public class LocalDbContext : DbContext
    {
        public DbSet<ServiceMemo> ServiceMemos { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            var dbPath = System.IO.Path.Join(path, "ServiceMemoApp", "local_memos.db");
            
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        public void Migrate()
        {
            Database.EnsureCreated();
            
            // Direct safety migration for new columns
            try { Database.ExecuteSqlRaw("ALTER TABLE service_memos ADD COLUMN source TEXT DEFAULT 'ClientApp';"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE ServiceMemos ADD COLUMN source TEXT DEFAULT 'ClientApp';"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE service_memos ADD COLUMN Source TEXT DEFAULT 'ClientApp';"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE ServiceMemos ADD COLUMN Source TEXT DEFAULT 'ClientApp';"); } catch { }

            try { Database.ExecuteSqlRaw("ALTER TABLE service_memos ADD COLUMN is_mobile_portal_update INTEGER DEFAULT 0;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE ServiceMemos ADD COLUMN is_mobile_portal_update INTEGER DEFAULT 0;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE service_memos ADD COLUMN IsMobilePortalUpdate INTEGER DEFAULT 0;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE ServiceMemos ADD COLUMN IsMobilePortalUpdate INTEGER DEFAULT 0;"); } catch { }

            try { Database.ExecuteSqlRaw("ALTER TABLE service_memos ADD COLUMN is_pending_cloud_push INTEGER DEFAULT 0;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE ServiceMemos ADD COLUMN is_pending_cloud_push INTEGER DEFAULT 0;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE service_memos ADD COLUMN IsPendingCloudPush INTEGER DEFAULT 0;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE ServiceMemos ADD COLUMN IsPendingCloudPush INTEGER DEFAULT 0;"); } catch { }

            // Manual migration for new columns - trying both cases for safety
            var columns = new[] 
            { 
                "MemoNumber", "memo_number",
                "CustomerName", "customer_name",
                "PhoneNumber", "phone_number",
                "DeviceName", "device_name",
                "DeviceModel", "device_model",
                "IssueDescription", "issue_description",
                "Status", "status",
                "ImagePath", "image_path",
                "CloudId", "cloud_id",
                "CloudOwnerKey", "owner_key",
                "CustomerAddress", "customer_address",
                "Phone1", "phone_1",
                "Phone2", "phone_2",
                "TechnicianName", "technician_name",
                "Brand", "brand",
                "SerialNumber", "serial_number",
                "Accessories", "accessories",
                "Diagnostics", "diagnostics",
                "OrderUpdates", "order_updates",
                "ReturnDate", "return_date",
                "IsRepeatedDevice", "is_repeated_device",
                "ItemizedCosts", "itemized_costs",
                "IsPendingCloudPush", "is_pending_cloud_push",
                "IsMobilePortalUpdate", "is_mobile_portal_update",
                "Source", "source"
            };

            // These columns hold nullable non-string types (DateTime?, decimal?) and must
            // stay NULL rather than being set to '' which would cause parse errors on read-back.
            var nullableColumns = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "ReturnDate", "return_date"
            };

            // Integer columns that should default to 0 (not empty string)
            var integerColumns = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "IsRepeatedDevice", "is_repeated_device",
                "IsPendingCloudPush", "is_pending_cloud_push",
                "IsMobilePortalUpdate", "is_mobile_portal_update"
            };

            var tables = new[] { "service_memos", "ServiceMemos" };

            // Dynamic PRAGMA table_info inspection to guarantee ALL missing columns are added automatically
            foreach (var table in tables)
            {
                try
                {
                    using var conn = Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"PRAGMA table_info({table});";
                    var existingCols = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            existingCols.Add(reader.GetString(1));
                        }
                    }

                    foreach (var col in columns)
                    {
                        if (!existingCols.Contains(col))
                        {
                            try
                            {
                                string colType = integerColumns.Contains(col) ? "INTEGER DEFAULT 0" : (col.Equals("source", StringComparison.OrdinalIgnoreCase) ? "TEXT DEFAULT 'ClientApp'" : "TEXT");
                                using var alterCmd = conn.CreateCommand();
                                alterCmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {col} {colType};";
                                alterCmd.ExecuteNonQuery();
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            foreach (var table in tables)
            {
                foreach (var col in columns)
                {
                    try
                    {
                        if (integerColumns.Contains(col))
                            Database.ExecuteSqlRaw(string.Format("ALTER TABLE {0} ADD COLUMN {1} INTEGER DEFAULT 0;", table, col));
                        else
                            Database.ExecuteSqlRaw(string.Format("ALTER TABLE {0} ADD COLUMN {1} TEXT;", table, col));
                    }
                    catch
                    {
                        // Column or Table likely already exists/doesn't exist
                    }

                    // Only default to empty string for text columns — skip nullable/integer typed columns
                    if (!nullableColumns.Contains(col) && !integerColumns.Contains(col))
                    {
                        try
                        {
                            Database.ExecuteSqlRaw(string.Format("UPDATE {0} SET {1} = '' WHERE {1} IS NULL;", table, col));
                        }
                        catch { }
                    }
                }

                // Cleanup: reset any accidental empty-string values back to NULL for nullable typed columns.
                // This corrects rows that were written with '' by a previous migration run.
                foreach (var col in nullableColumns)
                {
                    try
                    {
                        Database.ExecuteSqlRaw(string.Format("UPDATE {0} SET {1} = NULL WHERE {1} = '';", table, col));
                    }
                    catch { }
                }

                // Automatic Deduplication Cleanup: Delete duplicate records with the same MemoNumber (keep row with MAX id/rowid)
                try
                {
                    Database.ExecuteSqlRaw(string.Format(
                        "DELETE FROM {0} WHERE Id NOT IN (SELECT MAX(Id) FROM {0} WHERE MemoNumber IS NOT NULL AND MemoNumber != '' GROUP BY MemoNumber);", table));
                }
                catch { }
            }
        }

        public override int SaveChanges()
        {
            SetCloudOwnerKeyOnMemos();
            return base.SaveChanges();
        }

        public override System.Threading.Tasks.Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            SetCloudOwnerKeyOnMemos();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void SetCloudOwnerKeyOnMemos()
        {
            try
            {
                var key = Services.SettingsManager.Default.SubscriptionKey;
                if (string.IsNullOrEmpty(key)) return;

                foreach (var entry in ChangeTracker.Entries<ServiceMemo>())
                {
                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    {
                        if (string.IsNullOrEmpty(entry.Entity.CloudOwnerKey))
                        {
                            entry.Entity.CloudOwnerKey = key;
                        }
                    }
                }
            }
            catch { }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Ignore Postgrest internal types globally
            modelBuilder.Ignore<Postgrest.ClientOptions>();
            modelBuilder.Ignore<Postgrest.Models.BaseModel>();
            
            // BaseModel properties are handled with [NotMapped] in ServiceMemo.cs if needed,
            // but global ignore is safer for complex types.
            modelBuilder.Entity<ServiceMemo>().Ignore("BaseUrl");
            modelBuilder.Entity<ServiceMemo>().Ignore("BaseHeaders");
            modelBuilder.Entity<ServiceMemo>().Ignore("ClientOptions");

            // Seed some dummy data for the dashboard
            modelBuilder.Entity<ServiceMemo>().HasData(
                new ServiceMemo { Id = 1, MemoNumber = "SM-001", CustomerName = "John Doe", PhoneNumber = "555-0101", DeviceName = "MacBook Pro 14", DeviceModel = "A2442", IssueDescription = "Screen cracked after dropping.", Diagnostics = "Display assembly replacement needed.", Status = "Pending", EstimatedCost = 150.00m, ImagePath = "", UpdatedAt = DateTime.Now, CloudId = "", CloudOwnerKey = "" },
                new ServiceMemo { Id = 2, MemoNumber = "SM-002", CustomerName = "Jane Smith", PhoneNumber = "555-0102", DeviceName = "ThinkPad T14", DeviceModel = "20W0002HUS", IssueDescription = "Won't boot into Windows, gets stuck at Lenovo logo.", Diagnostics = "Possible NVMe failure, testing required.", Status = "In Progress", EstimatedCost = 85.00m, ImagePath = "", UpdatedAt = DateTime.Now, CloudId = "", CloudOwnerKey = "" },
                new ServiceMemo { Id = 3, MemoNumber = "SM-003", CustomerName = "Acme Corp", PhoneNumber = "555-0103", DeviceName = "PowerEdge R740", DeviceModel = "R740-101", IssueDescription = "RAID 5 controller reporting predictive failure on Drive 2.", Diagnostics = "Drive replacement and array rebuild.", Status = "Completed", EstimatedCost = 450.00m, ImagePath = "", UpdatedAt = DateTime.Now, CloudId = "", CloudOwnerKey = "" }
            );
        }
    }
}
