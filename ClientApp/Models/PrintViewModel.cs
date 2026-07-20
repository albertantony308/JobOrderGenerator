namespace ClientApp.Models
{
    public class PrintViewModel
    {
        public string CompanyName { get; set; } = "";
        public string CompanyAddress { get; set; } = "";
        public string CompanyContact { get; set; } = "";
        public string CompanyPhone { get; set; } = "";
        public string CompanyPhone2 { get; set; } = "";
        public bool HasPhone2 => !string.IsNullOrEmpty(CompanyPhone2);
        
        public string MemoNumber { get; set; } = "";
        public string Date { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string DeviceModel { get; set; } = "";
        public string IssueDescription { get; set; } = ""; // Renamed from Complaint
        public string CustomerAddress { get; set; } = "";
        public string Phone1 { get; set; } = "";
        public string Phone2 { get; set; } = "";
        public string TechnicianName { get; set; } = "";
        public string Brand { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public string Accessories { get; set; } = "";
        public string Diagnostics { get; set; } = "";
        public string EstimatedCost { get; set; } = ""; // Renamed from EstCost
        public string ItemizedCosts { get; set; } = "";
        public string TermsAndConditions { get; set; } = "";
        
        public bool ShowModel { get; set; } = true;
        public bool ShowDiagnostics { get; set; } = true;
        public bool ShowCost { get; set; } = true;

        public System.Collections.Generic.List<ClientApp.CustomTemplateDesignerWindow.DesignerBlock> CustomBlocks { get; set; } = new System.Collections.Generic.List<ClientApp.CustomTemplateDesignerWindow.DesignerBlock>();
        public bool IsHalfA4 { get; set; }
    }
}
