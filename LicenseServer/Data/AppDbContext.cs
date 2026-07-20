using LicenseServer.Models;
using Microsoft.EntityFrameworkCore;

namespace LicenseServer.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<LicenseKey> LicenseKeys { get; set; }
        public DbSet<DeviceActivation> DeviceActivations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LicenseKey>().HasData(
                new LicenseKey { Id = 1, Key = "TRIAL-1234", Type = "Trial", TrialDays = 30, MaxDevices = 1 },
                new LicenseKey { Id = 2, Key = "LIFE-5678", Type = "Lifetime", MaxDevices = 5 }
            );
        }
    }
}
