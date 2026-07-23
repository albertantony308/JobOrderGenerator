using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Linq;
using System.Printing;
using ClientApp.Models;
using ClientApp.Services;

namespace ClientApp
{
    public partial class PrintPreviewWindow : Window
    {
        private ServiceMemo? _memo;
        private PrintViewModel _viewModel = new PrintViewModel();
        private string _currentArrangement = "Single";
        private double _currentMargin = 40;
        private string? _templateJson; // Persist template JSON to avoid losing it on option changes
        private bool _isInitialized = false;

        public bool RequestNavigationToBranding { get; set; } = false;
        
        public PrintPreviewWindow(ServiceMemo memo)
        {
            InitializeComponent();
            WindowDwmFixer.ApplyFix(this);
            _memo = memo;
            LoadData();

            // Fix WPF AllowsTransparency WindowChrome rendering glitch
            this.Loaded += (s, e) => {
                if (this.Height > SystemParameters.WorkArea.Height)
                    this.Height = SystemParameters.WorkArea.Height - 80;
                if (this.Width > SystemParameters.WorkArea.Width)
                    this.Width = SystemParameters.WorkArea.Width - 80;

                // Toggle width slightly to force DWM to redraw the WindowChrome on Windows 10
                var originalWidth = this.Width;
                this.Width = originalWidth - 1;
                this.Width = originalWidth;
            };
        }

        public PrintPreviewWindow(int memoId)
        {
            InitializeComponent();
            using (var db = new ClientApp.Data.LocalDbContext())
            {
                _memo = db.ServiceMemos.Find(memoId);
            }
            if (_memo != null) LoadData();
            
            // Fix WPF AllowsTransparency WindowChrome rendering glitch
            this.Loaded += (s, e) => {
                if (this.Height > SystemParameters.WorkArea.Height)
                    this.Height = SystemParameters.WorkArea.Height - 80;
                if (this.Width > SystemParameters.WorkArea.Width)
                    this.Width = SystemParameters.WorkArea.Width - 80;

                // Toggle width slightly to force DWM to redraw the WindowChrome on Windows 10
                var originalWidth = this.Width;
                this.Width = originalWidth + 1;
                this.Width = originalWidth;
            };
        }



        private void LoadData()
        {
            if (_memo == null) return;
            
            // Populate ViewModel
            _viewModel.CompanyName = SettingsManager.Default.CompanyName;
            _viewModel.CompanyAddress = SettingsManager.Default.CompanyAddress;
            _viewModel.CompanyPhone = SettingsManager.Default.CompanyPhone;
            _viewModel.CompanyPhone2 = SettingsManager.Default.CompanyPhone2;
            
            string contact = $"Phone: {SettingsManager.Default.CompanyPhone}";
            if (!string.IsNullOrEmpty(SettingsManager.Default.CompanyPhone2))
                contact += $" / {SettingsManager.Default.CompanyPhone2}";
            contact += $" | Web: {SettingsManager.Default.CompanyName.ToLower().Replace(" ", "")}.tech";
            _viewModel.CompanyContact = contact;
            _viewModel.MemoNumber = _memo.MemoNumber;
            _viewModel.Date = _memo.CreatedAt.ToString("dd-MM-yyyy");
            _viewModel.CustomerName = _memo.CustomerName;
            _viewModel.CustomerPhone = string.IsNullOrEmpty(_memo.PhoneNumber) ? "N/A" : _memo.PhoneNumber;
            _viewModel.CustomerAddress = _memo.CustomerAddress;
            _viewModel.Phone1 = _memo.Phone1;
            _viewModel.Phone2 = _memo.Phone2;
            _viewModel.TechnicianName = _memo.TechnicianName;
            _viewModel.DeviceName = _memo.DeviceName;
            _viewModel.Brand = _memo.Brand;
            _viewModel.DeviceModel = string.IsNullOrEmpty(_memo.DeviceModel) ? "N/A" : _memo.DeviceModel;
            _viewModel.SerialNumber = string.IsNullOrEmpty(_memo.SerialNumber) ? "N/A" : _memo.SerialNumber;
            _viewModel.Accessories = string.IsNullOrEmpty(_memo.Accessories) ? "N/A" : _memo.Accessories;
            _viewModel.IssueDescription = _memo.IssueDescription;
            _viewModel.Diagnostics = _memo.Diagnostics;
            _viewModel.TermsAndConditions = SettingsManager.Default.TermsAndConditions;
            _viewModel.EstimatedCost = _memo.EstimatedCost > 0 ? $"Rs. {_memo.EstimatedCost:N2}" : "TBD";
            _viewModel.ItemizedCosts = _memo.ItemizedCosts;
            
            // Load persistent settings
            chkShowModel.IsChecked = SettingsManager.Default.PrintIncludeModel;
            chkShowDiagnostics.IsChecked = SettingsManager.Default.PrintIncludeDiagnostics;
            chkShowCost.IsChecked = SettingsManager.Default.PrintIncludeCost;

            // Load persistent paper size settings
            string savedPaperSize = SettingsManager.Default.DefaultPaperSize ?? "A4";
            foreach (ListBoxItem item in PaperSizeList.Items)
            {
                if (item.Tag?.ToString() == savedPaperSize)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            // Load persistent margin setting
            double savedMargin = SettingsManager.Default.PrintMargin;
            foreach (ListBoxItem item in MarginsList.Items)
            {
                if (double.TryParse(item.Tag?.ToString(), out double tagMargin) && Math.Abs(tagMargin - savedMargin) < 1)
                {
                    item.IsSelected = true;
                    _currentMargin = savedMargin;
                    break;
                }
            }

            // Load persistent copies setting
            int savedCopies = SettingsManager.Default.DefaultPrintCopies;
            if (savedCopies <= 0) savedCopies = 1;
            foreach (ComboBoxItem item in cmbCopies.Items)
            {
                if (item.Tag?.ToString() == savedCopies.ToString())
                {
                    item.IsSelected = true;
                    break;
                }
            }

            string savedArrangement = SettingsManager.Default.PrintArrangement ?? "Single";
            foreach (ListBoxItem item in ArrangementList.Items)
            {
                if (item.Tag?.ToString() == savedArrangement)
                {
                    item.IsSelected = true;
                    _currentArrangement = savedArrangement;
                    break;
                }
            }

            UpdateOptions();
            
            // Apply correct template — ALL templates now use CustomTemplate renderer
            string templateId = SettingsManager.Default.SelectedTemplateId;
            string resourceKey = "CustomTemplate";
            
            string? customJson = null;
            if (templateId == "Custom")
            {
                customJson = SettingsManager.Default.CustomTemplateJson;
            }
            else if (templateId.StartsWith("UserDesign:"))
            {
                string designName = templateId.Replace("UserDesign:", "");
                var design = SettingsManager.Default.UserTemplates?.FirstOrDefault(d => d.Name == designName);
                if (design != null) customJson = design.JsonData;
            }
            else if (templateId.StartsWith("SystemTemplate:"))
            {
                string sysId = templateId.Replace("SystemTemplate:", "");
                customJson = ClientApp.Services.DefaultTemplateService.GetTemplateJson(sysId);
            }
            else
            {
                // Legacy or bare template IDs — resolve through DefaultTemplateService
                try { customJson = ClientApp.Services.DefaultTemplateService.GetTemplateJson(templateId); }
                catch { customJson = ClientApp.Services.DefaultTemplateService.GetTemplateJson("FullCorporate"); }
            }

            // We don't set PrintContent.Content here anymore as RenderCustomTemplate will handle it
            // but we keep the template resource ready
            _templateJson = customJson;
            _isInitialized = true;
            
            if (!string.IsNullOrEmpty(_templateJson))
            {
                RenderCustomTemplate(_templateJson);
            }
            else
            {
                // Fallback if no JSON
                PrintContent.ContentTemplate = FindResource(resourceKey) as DataTemplate;
                PrintContent.Content = _viewModel;
            }

            InitializePrinterList();
        }

        private void SetXaml(RichTextBox rtb, string xaml, string fallbackText, CustomTemplateDesignerWindow.DesignerBlock b)
        {
            if (!string.IsNullOrEmpty(xaml))
            {
                using (var ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xaml)))
                {
                    var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                    range.Load(ms, DataFormats.Xaml);
                }
            }
            else
            {
                rtb.Document.Blocks.Clear();
                var run = new Run(fallbackText) {
                    FontSize = b.FontSize,
                    FontFamily = new FontFamily(b.FontFamily),
                    FontWeight = b.IsBold ? FontWeights.Bold : FontWeights.Normal,
                    FontStyle = b.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                    TextDecorations = b.IsUnderlined ? TextDecorations.Underline : null,
                    Foreground = (Brush?)new BrushConverter().ConvertFromString((b.Id == "table" && string.IsNullOrEmpty(b.FormattedTextXaml)) ? "#000000" : (b.ColorHex ?? "#000000")) ?? Brushes.Black
                };
                var p = new Paragraph(run) { Margin = new Thickness(0) };
                if (Enum.TryParse(b.TextAlignment, out TextAlignment align)) p.TextAlignment = align;
                rtb.Document.Blocks.Add(p);
            }
        }
        
        private void Margins_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (MarginsList.SelectedItem is ListBoxItem item && double.TryParse(item.Tag?.ToString(), out double margin))
            {
                _currentMargin = margin;
                SettingsManager.Default.PrintMargin = margin;
                SettingsManager.Save();
                DrawMarginGuide(0,0,0);
                RenderCustomTemplate(_templateJson ?? "");
            }
        }

        private void Copies_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (cmbCopies.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int copies))
            {
                SettingsManager.Default.DefaultPrintCopies = copies;
                SettingsManager.Save();
            }
        }

        private void Arrangement_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || ArrangementList == null || ArrangementList.SelectedItem == null) return;
            
            if (ArrangementList.SelectedItem is ListBoxItem item && item.Tag != null)
            {
                _currentArrangement = item.Tag.ToString() ?? "Single";
                SettingsManager.Default.PrintArrangement = _currentArrangement;
                SettingsManager.Save();
                RenderCustomTemplate(_templateJson ?? "");
            }
        }

        private void PaperSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || PaperSizeList == null || PaperSizeList.SelectedItem == null) return;
            
            if (PaperSizeList.SelectedItem is ListBoxItem item && item.Tag != null)
            {
                string paperSize = item.Tag.ToString() ?? "A4";
                SettingsManager.Default.DefaultPaperSize = paperSize;
                SettingsManager.Save();
                RenderCustomTemplate(_templateJson ?? "");
            }
        }

        private void ListBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                SidebarScrollViewer.RaiseEvent(eventArg);
            }
        }

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _viewModel == null || ArrangementList == null || chkShowModel == null || chkShowDiagnostics == null || chkShowCost == null) return;
            
            SettingsManager.Default.PrintIncludeModel = chkShowModel.IsChecked ?? true;
            SettingsManager.Default.PrintIncludeDiagnostics = chkShowDiagnostics.IsChecked ?? true;
            SettingsManager.Default.PrintIncludeCost = chkShowCost.IsChecked ?? true;
            SettingsManager.Save();
            
            UpdateOptions();
            RenderCustomTemplate(_templateJson ?? "");
        }

        private void RenderCustomTemplate(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var blocks = System.Text.Json.JsonSerializer.Deserialize<List<CustomTemplateDesignerWindow.DesignerBlock>>(json);
                if (blocks == null || blocks.Count == 0) return;

                _viewModel.CustomBlocks = blocks;
                _viewModel.IsHalfA4 = blocks[0].IsHalfA4;

                UpdateOptions(); // Get latest arrangement info
                string arrangement = _currentArrangement;

                int rows = 1, cols = 1, copies = 1;
                bool isFullA4Layout = false;

                switch (arrangement)
                {
                    case "Single": rows = 1; cols = 1; isFullA4Layout = false; copies = 1; break;
                    case "2v": rows = 2; cols = 1; isFullA4Layout = true; copies = 2; break;
                    case "2h": rows = 1; cols = 2; isFullA4Layout = true; copies = 2; break;
                    case "4grid": rows = 2; cols = 2; isFullA4Layout = true; copies = 4; break;
                    case "8grid": rows = 4; cols = 2; isFullA4Layout = true; copies = 8; break;
                    case "12grid": rows = 4; cols = 3; isFullA4Layout = true; copies = 12; break;
                    case "HalfA4Top": rows = 2; cols = 1; isFullA4Layout = true; copies = 1; break;
                    case "HalfA4Bottom": rows = 2; cols = 1; isFullA4Layout = true; copies = 1; break;
                    case "RotatedHalf": rows = 2; cols = 1; isFullA4Layout = true; copies = 1; break;
                }

                var paperSize = SettingsManager.Default.DefaultPaperSize ?? "A4";

                // Ensure PrintArea dimensions match. Multi-copy layouts ALWAYS use full page size.
                if (paperSize == "A5")
                {
                    PrintArea.Width = 560;
                    if (isFullA4Layout || !_viewModel.IsHalfA4) PrintArea.Height = 794;
                    else PrintArea.Height = 397;
                }
                else if (paperSize == "A6")
                {
                    PrintArea.Width = 397;
                    if (isFullA4Layout || !_viewModel.IsHalfA4) PrintArea.Height = 560;
                    else PrintArea.Height = 280;
                }
                else if (paperSize == "Letter")
                {
                    PrintArea.Width = 816;
                    if (isFullA4Layout || !_viewModel.IsHalfA4) PrintArea.Height = 1056;
                    else PrintArea.Height = 528;
                }
                else
                {
                    PrintArea.Width = 794;
                    if (isFullA4Layout || !_viewModel.IsHalfA4) PrintArea.Height = 1123;
                    else PrintArea.Height = 561;
                }

                // Draw margin guides on the overlay (never touches content layout)
                DrawMarginGuide(PrintArea.Width, PrintArea.Height, _currentMargin);

                if (arrangement == "Single")
                {
                    var canvas = ClientApp.Services.TemplateRenderer.Render(blocks, _viewModel);
                    PrintContent.ContentTemplate = null; // Clear to avoid white screen conflict
                    
                    if (paperSize != "A4")
                    {
                        double targetWidth = 794;
                        double targetHeight = _viewModel.IsHalfA4 ? 561 : 1123;

                        if (paperSize == "A5")
                        {
                            targetWidth = 560;
                            targetHeight = _viewModel.IsHalfA4 ? 397 : 794;
                        }
                        else if (paperSize == "A6")
                        {
                            targetWidth = 397;
                            targetHeight = _viewModel.IsHalfA4 ? 280 : 560;
                        }
                        else if (paperSize == "Letter")
                        {
                            targetWidth = 816;
                            targetHeight = _viewModel.IsHalfA4 ? 528 : 1056;
                        }

                        var vb = new Viewbox { 
                            Child = canvas, 
                            Stretch = Stretch.Uniform,
                            Width = targetWidth,
                            Height = targetHeight
                        };
                        PrintContent.Content = vb;
                    }
                    else
                    {
                        PrintContent.Content = canvas;
                    }
                    return;
                }

                // Grid layout for multi-copy or specific placement
                var layoutGrid = new Grid { VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };
                for (int r = 0; r < rows; r++) layoutGrid.RowDefinitions.Add(new RowDefinition());
                for (int c = 0; c < cols; c++) layoutGrid.ColumnDefinitions.Add(new ColumnDefinition());

                int startRow = (arrangement == "HalfA4Bottom") ? 1 : 0;
                
                for (int i = 0; i < copies; i++)
                {
                    var canvas = ClientApp.Services.TemplateRenderer.Render(blocks, _viewModel);
                    if (canvas != null)
                    {
                        // Wrap in Viewbox for auto-scaling and add padding
                        var vb = new Viewbox { 
                            Child = canvas, 
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(arrangement == "RotatedHalf" ? 5 : 15) 
                        };

                        if (arrangement == "RotatedHalf")
                        {
                            vb.LayoutTransform = new System.Windows.Media.RotateTransform(90);
                        }
                        
                        int r = (i / cols) + startRow;
                        int c = i % cols;
                        
                        if (r < rows)
                        {
                            Grid.SetRow(vb, r);
                            Grid.SetColumn(vb, c);
                            layoutGrid.Children.Add(vb);
                        }
                    }
                }

                PrintContent.ContentTemplate = null; // Clear to avoid white screen conflict
                PrintContent.Content = layoutGrid;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Render Error: " + ex.Message);
                // Fallback to basic view if grid rendering fails - restore template
                PrintContent.ContentTemplate = FindResource("CustomTemplate") as DataTemplate;
                PrintContent.Content = _viewModel;
            }
        }

        private void DrawMarginGuide(double pageW, double pageH, double margin)
        {
            MarginGuideOverlay.Children.Clear();
            if (margin <= 0) return;

            // Four semi-transparent shaded strips representing the margin zones
            var marginColor = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 100, 149, 237)); // cornflower blue tint

            // Top strip
            var top = new System.Windows.Shapes.Rectangle { Width = pageW, Height = margin, Fill = marginColor };
            Canvas.SetLeft(top, 0); Canvas.SetTop(top, 0);
            MarginGuideOverlay.Children.Add(top);

            // Bottom strip
            var bottom = new System.Windows.Shapes.Rectangle { Width = pageW, Height = margin, Fill = marginColor };
            Canvas.SetLeft(bottom, 0); Canvas.SetTop(bottom, pageH - margin);
            MarginGuideOverlay.Children.Add(bottom);

            // Left strip (inner height only)
            var left = new System.Windows.Shapes.Rectangle { Width = margin, Height = pageH - margin * 2, Fill = marginColor };
            Canvas.SetLeft(left, 0); Canvas.SetTop(left, margin);
            MarginGuideOverlay.Children.Add(left);

            // Right strip (inner height only)
            var right = new System.Windows.Shapes.Rectangle { Width = margin, Height = pageH - margin * 2, Fill = marginColor };
            Canvas.SetLeft(right, pageW - margin); Canvas.SetTop(right, margin);
            MarginGuideOverlay.Children.Add(right);

            // Dashed guide border showing the printable area boundary
            var guide = new System.Windows.Shapes.Rectangle
            {
                Width = pageW - margin * 2,
                Height = pageH - margin * 2,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(140, 66, 133, 244)),
                StrokeThickness = 1,
                StrokeDashArray = new System.Windows.Media.DoubleCollection { 6, 4 },
                Fill = System.Windows.Media.Brushes.Transparent
            };
            Canvas.SetLeft(guide, margin); Canvas.SetTop(guide, margin);
            MarginGuideOverlay.Children.Add(guide);
        }

        private string GetPlaceholderText(string id)
        {
            return id switch
            {
                "logo" => "LOGO",
                "name" => _viewModel.CompanyName,
                "company_name" => _viewModel.CompanyName,
                "address" => _viewModel.CompanyAddress,
                "company_address" => _viewModel.CompanyAddress,
                "phone" => _viewModel.CompanyContact,
                "company_phone" => _viewModel.CompanyContact,
                "memo_id" => _viewModel.MemoNumber,
                "order_id" => _viewModel.MemoNumber,
                "order_number" => _viewModel.MemoNumber,
                "id" => _viewModel.MemoNumber,
                "date" => _viewModel.Date,
                "order_date" => _viewModel.Date,
                "memo_date" => _viewModel.Date,
                "customer" => $"{_viewModel.CustomerName}\n{_viewModel.CustomerPhone}",
                "customer_name" => _viewModel.CustomerName,
                "customer_phone" => _viewModel.CustomerPhone,
                "customer_address" => _viewModel.CustomerAddress,
                "device" => $"{_viewModel.DeviceName} ({_viewModel.DeviceModel})",
                "product_name" => _viewModel.DeviceName,
                "model" => !string.IsNullOrEmpty(_viewModel.DeviceName) ? $"{_viewModel.DeviceName} — {_viewModel.DeviceModel}" : _viewModel.DeviceModel,
                "brand" => _viewModel.Brand,
                "serial_number" => _viewModel.SerialNumber,
                "accessories" => _viewModel.Accessories,
                "issue" => _viewModel.IssueDescription,
                "description" => _viewModel.IssueDescription,
                "issue_description" => _viewModel.IssueDescription,
                "diagnostics" => _viewModel.Diagnostics,
                "cost" => _viewModel.EstimatedCost,
                "technician_name" => _viewModel.TechnicianName,
                "terms" => _viewModel.TermsAndConditions,
                "customer_signature" => "__________________________\nCustomer Signature",
                "technician_signature" => "__________________________\nTechnician Signature",
                "company_signature" => "__________________________\nCompany Signature",
                "signatures" => "__________________________\nCustomer Signature\n\n__________________________\nTechnician Signature",
                _ => ""
            };
        }

        private void UpdateOptions()
        {
            if (_memo == null) return;

            _viewModel.ShowModel = SettingsManager.Default.PrintIncludeModel;
            _viewModel.ShowDiagnostics = SettingsManager.Default.PrintIncludeDiagnostics && !string.IsNullOrEmpty(_memo.Diagnostics);
            _viewModel.ShowCost = SettingsManager.Default.PrintIncludeCost;
            
            // ViewModel update only; RenderCustomTemplate will handle the actual visual content
        }



        private void CustomizeMemoStyle_Click(object sender, RoutedEventArgs e)
        {
            RequestNavigationToBranding = true;
            this.Close();
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (_memo == null) return;

            try
            {
                var printDialog = new PrintDialog();

                // Pre-configure the print ticket based on the selected paper size
                try
                {
                    var paperSize = SettingsManager.Default.DefaultPaperSize ?? "A4";
                    if (paperSize == "A5")
                    {
                        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA5);
                    }
                    else if (paperSize == "A6")
                    {
                        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA6);
                    }
                    else if (paperSize == "Letter")
                    {
                        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaLetter);
                    }
                    else
                    {
                        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to pre-set PageMediaSize: {ex.Message}");
                }

                // Pre-configure number of copies
                try
                {
                    if (cmbCopies.SelectedItem is ComboBoxItem copyItem && int.TryParse(copyItem.Tag?.ToString(), out int copies))
                    {
                        printDialog.PrintTicket.CopyCount = copies;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to set CopyCount: {ex.Message}");
                }

                // DIRECT/SILENT PRINTING: Assign PrintQueue from selected printer dropdown
                string? selectedPrinter = PrinterList.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(selectedPrinter))
                {
                    try
                    {
                        var printServer = new System.Printing.LocalPrintServer();
                        var printQueues = printServer.GetPrintQueues(new[] { System.Printing.EnumeratedPrintQueueTypes.Local, System.Printing.EnumeratedPrintQueueTypes.Connections });
                        var selectedQueue = printQueues.FirstOrDefault(q => q.FullName == selectedPrinter);
                        if (selectedQueue != null)
                        {
                            printDialog.PrintQueue = selectedQueue;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to assign PrintQueue silently: {ex.Message}");
                    }
                }

                // Skip ShowDialog() and print immediately!
                // Use the user-selected margin
                double margin = _currentMargin;
                
                var paperSizeVal = SettingsManager.Default.DefaultPaperSize ?? "A4";
                double pageWidth = 794;
                double pageHeight = 1123;

                if (paperSizeVal == "A5")
                {
                    pageWidth = 560;
                    pageHeight = _viewModel.IsHalfA4 && _currentArrangement == "Single" ? 397 : 794;
                }
                else if (paperSizeVal == "A6")
                {
                    pageWidth = 397;
                    pageHeight = _viewModel.IsHalfA4 && _currentArrangement == "Single" ? 280 : 560;
                }
                else if (paperSizeVal == "Letter")
                {
                    pageWidth = 816;
                    pageHeight = _viewModel.IsHalfA4 && _currentArrangement == "Single" ? 528 : 1056;
                }
                else // A4
                {
                    pageWidth = 794;
                    pageHeight = _viewModel.IsHalfA4 && _currentArrangement == "Single" ? 561 : 1123;
                }

                double printableWidth = pageWidth - (margin * 2);
                
                FixedDocument fixedDoc = new FixedDocument();
                fixedDoc.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);

                FixedPage fixedPage = new FixedPage();
                fixedPage.Width = pageWidth;
                fixedPage.Height = pageHeight;
                fixedPage.Background = Brushes.White;

                double defaultWidth = 794;
                double defaultHeight = 1123;

                if (paperSizeVal == "A5")
                {
                    defaultWidth = 560;
                    defaultHeight = 794;
                    if (_viewModel.IsHalfA4 && _currentArrangement == "Single")
                    {
                        defaultHeight = 397;
                    }
                }
                else if (paperSizeVal == "A6")
                {
                    defaultWidth = 397;
                    defaultHeight = 560;
                    if (_viewModel.IsHalfA4 && _currentArrangement == "Single")
                    {
                        defaultHeight = 280;
                    }
                }
                else if (paperSizeVal == "Letter")
                {
                    defaultWidth = 816;
                    defaultHeight = 1056;
                    if (_viewModel.IsHalfA4 && _currentArrangement == "Single")
                    {
                        defaultHeight = 528;
                    }
                }
                else if (paperSizeVal == "A4")
                {
                    defaultWidth = 794;
                    defaultHeight = 1123;
                    if (_viewModel.IsHalfA4 && _currentArrangement == "Single")
                    {
                        defaultHeight = 561;
                    }
                }

                double areaWidth = PrintArea.ActualWidth > 0 ? PrintArea.ActualWidth : defaultWidth;
                double areaHeight = PrintArea.ActualHeight > 0 ? PrintArea.ActualHeight : defaultHeight;
                
                // Ensure layout is updated before rendering
                PrintArea.UpdateLayout();

                // Render PrintArea to a high-resolution 300 DPI bitmap to prevent pixelation on high-res prints/PDFs
                double scaleFactor = 300.0 / 96.0; // 3.125
                int pixelWidth = (int)Math.Max(1, Math.Ceiling(areaWidth * scaleFactor));
                int pixelHeight = (int)Math.Max(1, Math.Ceiling(areaHeight * scaleFactor));

                RenderTargetBitmap bmp = new RenderTargetBitmap(
                    pixelWidth,
                    pixelHeight,
                    300.0,
                    300.0,
                    PixelFormats.Pbgra32);

                // Temporarily hide the on-screen preview border during printing
                PrintArea.BorderThickness = new Thickness(0);
                PrintArea.UpdateLayout();

                DrawingVisual dv = new DrawingVisual();
                using (DrawingContext dc = dv.RenderOpen())
                {
                    VisualBrush vb = new VisualBrush(PrintArea);
                    dc.DrawRectangle(vb, null, new Rect(0, 0, areaWidth, areaHeight));
                }
                bmp.Render(dv);
                
                // Restore the preview border
                PrintArea.BorderThickness = new Thickness(1);

                ImageBrush imageBrush = new ImageBrush(bmp);
                imageBrush.Stretch = Stretch.Uniform;

                double scale = Math.Min(printableWidth / areaWidth, (pageHeight - (margin * 2)) / areaHeight);
                double finalWidth = areaWidth * scale;
                double finalHeight = areaHeight * scale;

                Rectangle rect = new Rectangle();
                rect.Width = finalWidth;
                rect.Height = finalHeight;
                rect.Fill = imageBrush;

                FixedPage.SetLeft(rect, (pageWidth - finalWidth) / 2);
                FixedPage.SetTop(rect, (pageHeight - finalHeight) / 2);

                fixedPage.Children.Add(rect);

                PageContent pageContent = new PageContent();
                ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);
                fixedDoc.Pages.Add(pageContent);

                printDialog.PrintDocument(fixedDoc.DocumentPaginator, $"Service Memo {(_memo?.MemoNumber ?? "")}");
                
                MessageBox.Show($"Job successfully sent directly to printer:\n{selectedPrinter ?? "Default Printer"}", "Print Sent", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not print: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private string FormatAsBullets(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            
            var lines = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 1) return input;

            return string.Join("\r\n", lines.Select(line => "• " + line.Trim().TrimStart('•', '-', '*').Trim()));
        }

        private void InitializePrinterList()
        {
            try
            {
                var printers = new System.Collections.Generic.List<string>();
                string defaultPrinter = "";

                // Get default printer
                try
                {
                    var printDocument = new System.Drawing.Printing.PrintDocument();
                    defaultPrinter = printDocument.PrinterSettings.PrinterName;
                }
                catch { }

                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    printers.Add(printer);
                }

                PrinterList.ItemsSource = printers;

                if (!string.IsNullOrEmpty(defaultPrinter) && printers.Contains(defaultPrinter))
                {
                    PrinterList.SelectedItem = defaultPrinter;
                }
                else if (printers.Count > 0)
                {
                    PrinterList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load printer list: {ex.Message}");
            }
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => ApplyZoom(0.1);
        private void ZoomOut_Click(object sender, RoutedEventArgs e) => ApplyZoom(-0.1);
        private void ResetZoom_Click(object sender, RoutedEventArgs e) { PreviewScale.ScaleX = 0.5; PreviewScale.ScaleY = 0.5; UpdateZoomText(); }

        private void ApplyZoom(double delta)
        {
            double newScale = PreviewScale.ScaleX + delta;
            if (newScale >= 0.2 && newScale <= 3.0)
            {
                PreviewScale.ScaleX = newScale;
                PreviewScale.ScaleY = newScale;
                UpdateZoomText();
            }
        }

        private void UpdateZoomText() => txtZoomPercent.Text = $"{(PreviewScale.ScaleX * 100):0}%";

        private void ZoomContainer_ManipulationDelta(object sender, System.Windows.Input.ManipulationDeltaEventArgs e)
        {
            double scale = PreviewScale.ScaleX * e.DeltaManipulation.Scale.X;
            if (scale >= 0.2 && scale <= 3.0)
            {
                PreviewScale.ScaleX = scale;
                PreviewScale.ScaleY = scale;
                UpdateZoomText();
            }
            e.Handled = true;
        }

        protected override void OnPreviewMouseWheel(System.Windows.Input.MouseWheelEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                ApplyZoom(e.Delta > 0 ? 0.1 : -0.1);
                e.Handled = true;
            }
            base.OnPreviewMouseWheel(e);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                Maximize_Click(sender, e);
            else if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
        }
        private Point _lastMousePosition;
        private bool _isPanning = false;

        private void PreviewScrollViewer_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (PreviewScale.ScaleX > 0.51)
            {
                _lastMousePosition = e.GetPosition(PreviewScrollViewer);
                _isPanning = true;
                PreviewScrollViewer.Cursor = System.Windows.Input.Cursors.Hand;
                PreviewScrollViewer.CaptureMouse();
            }
        }

        private void PreviewScrollViewer_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point currentPosition = e.GetPosition(PreviewScrollViewer);
                double deltaX = _lastMousePosition.X - currentPosition.X;
                double deltaY = _lastMousePosition.Y - currentPosition.Y;

                PreviewScrollViewer.ScrollToHorizontalOffset(PreviewScrollViewer.HorizontalOffset + deltaX);
                PreviewScrollViewer.ScrollToVerticalOffset(PreviewScrollViewer.VerticalOffset + deltaY);

                _lastMousePosition = currentPosition;
            }
        }

        private void PreviewScrollViewer_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                PreviewScrollViewer.ReleaseMouseCapture();
                PreviewScrollViewer.Cursor = null;
            }
        }
    }
}
