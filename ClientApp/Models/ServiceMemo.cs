using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ClientApp.Models
{
    [Table("service_memos")]
    public class ServiceMemo : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }
        
        [Column("memo_number")]
        public string MemoNumber { get; set; } = string.Empty;
        
        [Column("customer_name")]
        public string CustomerName { get; set; } = string.Empty;
        
        [Column("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;
        
        [Column("device_name")]
        public string DeviceName { get; set; } = string.Empty;
        
        [Column("device_model")]
        public string DeviceModel { get; set; } = string.Empty;
        
        [Column("issue_description")]
        public string IssueDescription { get; set; } = string.Empty;
        
        [Column("status")]
        public string Status { get; set; } = "Pending";
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [Column("estimated_cost")]
        public decimal EstimatedCost { get; set; }
        
        [Column("image_path")]
        public string ImagePath { get; set; } = string.Empty;
        
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        [Column("cloud_id")]
        public string CloudId { get; set; } = string.Empty;

        [Column("owner_key")]
        public string CloudOwnerKey { get; set; } = string.Empty;

        [Column("customer_address")]
        public string CustomerAddress { get; set; } = string.Empty;

        [Column("phone_1")]
        public string Phone1 { get; set; } = string.Empty;

        [Column("phone_2")]
        public string Phone2 { get; set; } = string.Empty;

        [Column("technician_name")]
        public string TechnicianName { get; set; } = string.Empty;

        [Column("brand")]
        public string Brand { get; set; } = string.Empty;

        [Column("serial_number")]
        public string SerialNumber { get; set; } = string.Empty;

        [Column("accessories")]
        public string Accessories { get; set; } = string.Empty;

        [Column("diagnostics")]
        public string Diagnostics { get; set; } = string.Empty;

        [Column("order_updates")]
        public string OrderUpdates { get; set; } = string.Empty;

        [Column("itemized_costs")]
        public string ItemizedCosts { get; set; } = string.Empty;

        [Column("return_date")]
        public DateTime? ReturnDate { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string ReturnDateDisplay
        {
            get
            {
                if (ReturnDate.HasValue && ReturnDate.Value > DateTime.MinValue.AddYears(1))
                {
                    return ReturnDate.Value.ToString("dd MMM yyyy");
                }
                return "Not Returned";
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool IsReturned => ReturnDate.HasValue && ReturnDate.Value > DateTime.MinValue.AddYears(1);

        [Column("is_repeated_device")]
        public bool IsRepeatedDevice { get; set; } = false;
    }

    public class ServiceMemoDto
    {
        public int Id { get; set; }
        public string MemoNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string IssueDescription { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public decimal EstimatedCost { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string CloudId { get; set; } = string.Empty;
        public string CloudOwnerKey { get; set; } = string.Empty;
        public string CustomerAddress { get; set; } = string.Empty;
        public string Phone1 { get; set; } = string.Empty;
        public string Phone2 { get; set; } = string.Empty;
        public string TechnicianName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Accessories { get; set; } = string.Empty;
        public string Diagnostics { get; set; } = string.Empty;
        public string OrderUpdates { get; set; } = string.Empty;
        public string ItemizedCosts { get; set; } = string.Empty;
        public DateTime? ReturnDate { get; set; }
        public bool IsRepeatedDevice { get; set; } = false;

        public static ServiceMemoDto FromModel(ServiceMemo m, bool syncImages = true)
        {
            return new ServiceMemoDto
            {
                Id = m.Id,
                MemoNumber = m.MemoNumber,
                CustomerName = m.CustomerName,
                PhoneNumber = m.PhoneNumber,
                DeviceName = m.DeviceName,
                DeviceModel = m.DeviceModel,
                IssueDescription = m.IssueDescription,
                Status = m.Status,
                CreatedAt = m.CreatedAt,
                EstimatedCost = m.EstimatedCost,
                ImagePath = syncImages ? m.ImagePath : string.Empty,
                UpdatedAt = m.UpdatedAt,
                CloudId = m.CloudId,
                CloudOwnerKey = m.CloudOwnerKey,
                CustomerAddress = m.CustomerAddress,
                Phone1 = m.Phone1,
                Phone2 = m.Phone2,
                TechnicianName = m.TechnicianName,
                Brand = m.Brand,
                SerialNumber = m.SerialNumber,
                Accessories = m.Accessories,
                Diagnostics = m.Diagnostics,
                OrderUpdates = m.OrderUpdates,
                ItemizedCosts = m.ItemizedCosts,
                ReturnDate = m.ReturnDate,
                IsRepeatedDevice = m.IsRepeatedDevice
            };
        }
    }

    public class CostItem : System.ComponentModel.INotifyPropertyChanged
    {
        private string _description = "";
        private decimal _cost;

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        public decimal Cost
        {
            get => _cost;
            set
            {
                _cost = value;
                OnPropertyChanged(nameof(Cost));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}
