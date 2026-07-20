namespace LicenseServer.Models
{
    public class LicenseKey
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Type { get; set; } = "Trial"; // Trial or Lifetime
        public int TrialDays { get; set; } = 30;
        public int MaxDevices { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public string Email { get; set; } = string.Empty;
        public bool CloudEnabled { get; set; } = false;
        
        public ICollection<DeviceActivation> Activations { get; set; } = new List<DeviceActivation>();
    }
}
