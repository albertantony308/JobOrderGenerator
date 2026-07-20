namespace LicenseServer.Models
{
    public class DeviceActivation
    {
        public int Id { get; set; }
        public int LicenseKeyId { get; set; }
        public LicenseKey? LicenseKey { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string? DeviceName { get; set; }
        public DateTime ActivatedAt { get; set; }
    }
}
