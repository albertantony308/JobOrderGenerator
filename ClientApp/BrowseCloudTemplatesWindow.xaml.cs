using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClientApp.Services;
using ClientApp.Models;
using System.Text.Json;
using System.Net.Http;
using Postgrest.Attributes;
using Postgrest.Models;
using Postgrest;

namespace ClientApp
{
    public partial class BrowseCloudTemplatesWindow : Window
    {
        [Table("cloud_templates")]
        public class CloudTemplate : BaseModel
        {
            [PrimaryKey("id", false)]
            public string id { get; set; } = "";

            [Column("name")]
            public string name { get; set; } = "";

            [Column("json_data")]
            public string json_data { get; set; } = "";

            [Column("author")]
            public string author { get; set; } = "Community";

            [Column("is_half_a4")]
            public bool is_half_a4 { get; set; } = false;

            [Column("is_published")]
            public bool is_published { get; set; } = false;
        }

        private CloudTemplate? _selectedTemplate;

        public BrowseCloudTemplatesWindow()
        {
            InitializeComponent();
            WindowDwmFixer.ApplyFix(this);
            LoadCloudTemplates();
        }

        private async void LoadCloudTemplates()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            try
            {
                var client = SupabaseClientManager.Instance;
                if (client == null)
                {
                    MessageBox.Show("Cloud sync not configured. Please check SupabaseClientManager.cs", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    return;
                }

                // Fetch published templates from Supabase
                var response = await client.From<CloudTemplate>()
                                          .Where(x => x.is_published == true)
                                          .Get();
                
                var templates = response.Models;

                CloudTemplatesList.Children.Clear();
                if (templates.Count == 0)
                {
                    CloudTemplatesList.Children.Add(new TextBlock { 
                        Text = "No published templates found in the cloud.", 
                        Foreground = (Brush)TryFindResource("OnSurfaceVariantBrush"),
                        Margin = new Thickness(0, 40, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center 
                    });
                }
                else
                {
                    foreach (var t in templates)
                    {
                        CloudTemplatesList.Children.Add(CreateTemplateCard(t));
                    }
                }
            }
            catch (Exception ex)
            {
                // Show actual error to help debugging
                MessageBox.Show($"Cloud Sync Error: {ex.Message}\n\nPlease ensure your Supabase configuration is correct.", "Sync Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Fallback for demo purposes if Supabase is not reachable
                ShowDemoTemplates();
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowDemoTemplates()
        {
            CloudTemplatesList.Children.Clear();
            var demo = new CloudTemplate { 
                name = "Modern Gradient (Demo)", 
                author = "Antigravity", 
                json_data = DefaultTemplateService.GetTemplateJson("HalfModernDark"),
                is_half_a4 = true 
            };
            CloudTemplatesList.Children.Add(CreateTemplateCard(demo));
            
            var demo2 = new CloudTemplate { 
                name = "Minimalist Corporate (Demo)", 
                author = "Design Studio", 
                json_data = DefaultTemplateService.GetTemplateJson("FullCorporate"),
                is_half_a4 = false 
            };
            CloudTemplatesList.Children.Add(CreateTemplateCard(demo2));
        }

        private Border CreateTemplateCard(CloudTemplate t)
        {
            var border = new Border
            {
                Width = 200,
                Height = 280,
                Margin = new Thickness(0, 0, 20, 24),
                CornerRadius = new CornerRadius(24),
                Background = (Brush)TryFindResource("SurfaceContainerLowestBrush"),
                BorderBrush = (Brush)TryFindResource("OutlineBrush"),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = t
            };

            var stack = new StackPanel();
            
            var preview = new Border
            {
                Height = 160,
                Margin = new Thickness(12),
                CornerRadius = new CornerRadius(16),
                Background = Brushes.White,
                BorderBrush = (Brush)TryFindResource("OutlineBrush"),
                BorderThickness = new Thickness(0.5),
                ClipToBounds = true
            };
            
            try {
                List<CustomTemplateDesignerWindow.DesignerBlock>? blocks = null;
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var doc = JsonDocument.Parse(t.json_data);
                
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("blocks", out var blocksProp)) {
                    blocks = JsonSerializer.Deserialize<List<CustomTemplateDesignerWindow.DesignerBlock>>(blocksProp.GetRawText(), options);
                } else {
                    blocks = JsonSerializer.Deserialize<List<CustomTemplateDesignerWindow.DesignerBlock>>(t.json_data, options);
                }

                if (blocks != null) {
                    var demoVM = new PrintViewModel { 
                        IsHalfA4 = t.is_half_a4,
                        CompanyName = string.IsNullOrEmpty(SettingsManager.Default.CompanyName) ? "YOUR COMPANY NAME" : SettingsManager.Default.CompanyName,
                        CompanyAddress = string.IsNullOrEmpty(SettingsManager.Default.CompanyAddress) ? "123 Business Avenue, Suite 100\nNew York, NY 10001" : SettingsManager.Default.CompanyAddress,
                        CompanyPhone = SettingsManager.Default.CompanyPhone ?? "555-0123",
                        CompanyPhone2 = SettingsManager.Default.CompanyPhone2 ?? "",
                        MemoNumber = "SM-12345",
                        Date = DateTime.Now.ToString("MMM dd, yyyy"),
                        CustomerName = "Johnathan Doe",
                        CustomerPhone = "+1 (555) 000-1234",
                        CustomerAddress = "789 Residential Way, Apt 12B",
                        DeviceName = "MacBook Pro M3",
                        DeviceModel = "A2991 - Space Black",
                        Brand = "Apple",
                        SerialNumber = "C02XG123JL4M",
                        Accessories = "Laptop Case, Power Adapter, USB-C Cable",
                        IssueDescription = "Customer reports the screen flickering intermittently when at high brightness levels. System diagnostics required to check display cable and GPU stability.",
                        Diagnostics = "Internal cable reseated. Display panel tests passed. GPU stress test stable. Replaced LVDS connector as a precaution.",
                        EstimatedCost = "Rs. 249.00",
                        TechnicianName = "Alex Rivera",
                        TermsAndConditions = SettingsManager.Default.TermsAndConditions,
                        ShowModel = true,
                        ShowDiagnostics = true,
                        ShowCost = true
                    };
                    string contact = $"Phone: {demoVM.CompanyPhone}";
                    if (!string.IsNullOrEmpty(demoVM.CompanyPhone2))
                        contact += $" / {demoVM.CompanyPhone2}";
                    demoVM.CompanyContact = contact;

                    var canvas = TemplateRenderer.Render(blocks, demoVM);
                    var viewbox = new Viewbox { Child = canvas, Stretch = Stretch.Uniform, Margin = new Thickness(5) };
                    preview.Child = viewbox;
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Card Preview Error: {ex.Message}");
            }

            var name = new TextBlock
            {
                Text = t.name,
                FontWeight = FontWeights.Black,
                Foreground = (Brush)TryFindResource("OnSurfaceBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(12, 8, 12, 2),
                FontSize = 14,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var author = new TextBlock
            {
                Text = $"by {t.author}",
                FontSize = 12,
                Foreground = (Brush)TryFindResource("OnSurfaceVariantBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(12, 0, 12, 12)
            };

            stack.Children.Add(preview);
            stack.Children.Add(name);
            stack.Children.Add(author);
            border.Child = stack;

            border.MouseLeftButtonDown += (s, e) => SelectTemplate(t, border);

            return border;
        }

        private void SelectTemplate(CloudTemplate t, Border border)
        {
            _selectedTemplate = t;
            
            foreach (Border child in CloudTemplatesList.Children.OfType<Border>())
            {
                child.BorderBrush = (Brush)TryFindResource("OutlineBrush");
                child.BorderThickness = new Thickness(1);
                child.Background = (Brush)TryFindResource("SurfaceContainerLowestBrush");
            }
            border.BorderBrush = (Brush)TryFindResource("PrimaryBrush");
            border.BorderThickness = new Thickness(2);
            border.Background = (Brush)TryFindResource("SecondaryContainerBrush");

            txtTemplateName.Text = t.name;
            txtTemplateAuthor.Text = $"Designed by {t.author}";
            btnDownload.IsEnabled = true;
            btnCustomize.IsEnabled = true;

            try {
                List<CustomTemplateDesignerWindow.DesignerBlock>? blocks = null;
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var doc = JsonDocument.Parse(t.json_data);
                
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("blocks", out var blocksProp)) {
                    blocks = JsonSerializer.Deserialize<List<CustomTemplateDesignerWindow.DesignerBlock>>(blocksProp.GetRawText(), options);
                } else {
                    blocks = JsonSerializer.Deserialize<List<CustomTemplateDesignerWindow.DesignerBlock>>(t.json_data, options);
                }

                if (blocks != null) {
                    var demoVM = new PrintViewModel { 
                        IsHalfA4 = t.is_half_a4,
                        CompanyName = string.IsNullOrEmpty(SettingsManager.Default.CompanyName) ? "YOUR COMPANY NAME" : SettingsManager.Default.CompanyName,
                        CompanyAddress = string.IsNullOrEmpty(SettingsManager.Default.CompanyAddress) ? "123 Business Avenue, Suite 100\nNew York, NY 10001" : SettingsManager.Default.CompanyAddress,
                        CompanyPhone = SettingsManager.Default.CompanyPhone ?? "555-0123",
                        CompanyPhone2 = SettingsManager.Default.CompanyPhone2 ?? "",
                        MemoNumber = "SM-12345",
                        Date = DateTime.Now.ToString("MMM dd, yyyy"),
                        CustomerName = "Johnathan Doe",
                        CustomerPhone = "+1 (555) 000-1234",
                        CustomerAddress = "789 Residential Way, Apt 12B",
                        DeviceName = "MacBook Pro M3",
                        DeviceModel = "A2991 - Space Black",
                        Brand = "Apple",
                        SerialNumber = "C02XG123JL4M",
                        Accessories = "Laptop Case, Power Adapter, USB-C Cable",
                        IssueDescription = "Customer reports the screen flickering intermittently when at high brightness levels. System diagnostics required to check display cable and GPU stability.",
                        Diagnostics = "Internal cable reseated. Display panel tests passed. GPU stress test stable. Replaced LVDS connector as a precaution.",
                        EstimatedCost = "Rs. 249.00",
                        TechnicianName = "Alex Rivera",
                        TermsAndConditions = SettingsManager.Default.TermsAndConditions,
                        ShowModel = true,
                        ShowDiagnostics = true,
                        ShowCost = true
                    };
                    string contact = $"Phone: {demoVM.CompanyPhone}";
                    if (!string.IsNullOrEmpty(demoVM.CompanyPhone2))
                        contact += $" / {demoVM.CompanyPhone2}";
                    demoVM.CompanyContact = contact;

                    var canvas = TemplateRenderer.Render(blocks, demoVM);
                    PreviewContent.Content = canvas;
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Preview Error: {ex.Message}");
            }
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplate == null) return;

            string localName = _selectedTemplate.name + " (Cloud)";
            
            if (SettingsManager.Default.UserTemplates == null)
                SettingsManager.Default.UserTemplates = new List<UserTemplate>();

            // Check for duplicates
            if (SettingsManager.Default.UserTemplates.Any(ut => ut.Name == localName))
            {
                localName += "_" + DateTime.Now.ToString("Hmm");
            }

            SettingsManager.Default.UserTemplates.Add(new UserTemplate
            {
                Name = localName,
                JsonData = _selectedTemplate.json_data
            });
            SettingsManager.Save();

            MessageBox.Show($"'{_selectedTemplate.name}' has been added to your local library and will now open in the designer.", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            
            // Automatically open in designer locally
            var designer = new CustomTemplateDesignerWindow(_selectedTemplate.json_data, localName);
            designer.Owner = this.Owner;
            designer.Show();

            this.DialogResult = true;
            this.Close();
        }

        private void Customize_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTemplate == null) return;

            // Open in designer directly without saving first
            // The user can choose to save it from the designer
            var designer = new CustomTemplateDesignerWindow(_selectedTemplate.json_data, _selectedTemplate.name + " (Custom)");
            designer.Owner = this.Owner;
            designer.Show();

            this.DialogResult = true;
            this.Close();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadCloudTemplates();
        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
