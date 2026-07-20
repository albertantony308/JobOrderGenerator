using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media.Imaging;
using ClientApp.Services;
using System.Windows.Documents;
using System.Windows.Media.Effects;

namespace ClientApp
{
    public partial class CustomTemplateDesignerWindow : Window
    {
        public class DesignerBlock
        {
            public string Id { get; set; } = "";
            public double X { get; set; } = 0;
            public double Y { get; set; } = 0;
            public double Width { get; set; } = 0;
            public double Height { get; set; } = 0;
            public string? ColorHex { get; set; } = "#1A73E8";
            public double FontSize { get; set; } = 12;
            public string FontFamily { get; set; } = "Inter";
            public double Opacity { get; set; } = 1.0;
            public bool IsBold { get; set; } = false;
            public bool IsItalic { get; set; } = false;
            public bool IsUnderlined { get; set; } = false;
            public string ImagePath { get; set; } = "";
            public string CustomText { get; set; } = "";
            public bool IsHalfA4 { get; set; } = false;
            public string TextAlignment { get; set; } = "Center";
            public int TableRows { get; set; } = 3;
            public int TableCols { get; set; } = 2;
            public string TableCellsJson { get; set; } = "";
            
            [JsonConverter(typeof(FlexibleStringConverter))]
            public string TableColumnWidths { get; set; } = "";

            [JsonPropertyName("TableColWidths")]
            [JsonConverter(typeof(FlexibleStringConverter))]
            public string? TableColWidths { set { if (!string.IsNullOrEmpty(value)) TableColumnWidths = value; } get => TableColumnWidths; }

            [JsonConverter(typeof(FlexibleStringConverter))]
            public string TableRowHeights { get; set; } = "";
            public string TableBackgroundColorHex { get; set; } = "Transparent";
            public string BorderColorHex { get; set; } = "Transparent";
            public string FormattedTextXaml { get; set; } = "";
            public double ShapeBorderThickness { get; set; } = 0;
            public double BorderRadius { get; set; } = 4;
            public int PolygonSides { get; set; } = 5;
            public string? VisibilityCondition { get; set; } = null; // e.g. "DiagnosticsNotEmpty"
        }

        public class TableCellData
        {
            public int Row { get; set; }
            public int Col { get; set; }
            public int RowSpan { get; set; } = 1;
            public int ColSpan { get; set; } = 1;
            public string Text { get; set; } = "";
            public string FormattedTextXaml { get; set; } = "";
            public string BackgroundColor { get; set; } = "Transparent";
            public string BorderColor { get; set; } = "#CCCCCC";
            public double BorderL { get; set; } = 1;
            public double BorderT { get; set; } = 1;
            public double BorderR { get; set; } = 1;
            public double BorderB { get; set; } = 1;
            public string BorderStyle { get; set; } = "Solid";
            public string TextAlignment { get; set; } = "Left";
        }

        public class PlaceholderInfo
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Icon { get; set; } = "";
        }

        public class ToolboxGroup
        {
            public string GroupName { get; set; } = "";
            public List<PlaceholderInfo> Items { get; set; } = new List<PlaceholderInfo>();
        }

        private UIElement? _selectedElement;
        private List<Border> _selectedElements = new List<Border>();
        private Point _selectionStartPoint;
        private bool _isDrawingSelectionBox = false;
        private System.Windows.Shapes.Rectangle? _selectionBox;
        private DesignerBlock? _selectedBlock;
        private UIElement? _draggingElement;
        private string? _originalFontFamilyName = null;
        private bool _isClosingAfterSave = false;
        private bool _isLoadingTemplate = false;
        private bool _isDirty = false;
        private Point _lastDragPos;
        private bool _isUpdatingUI = false;
        private Dictionary<Border, List<Border>> _elementHandles = new Dictionary<Border, List<Border>>();
        private List<Border> _selectedTableCells = new List<Border>();
        private bool _isDraggingCells = false;
        private Border? _selectedTableCell;
        private TableCellData? _selectedTableCellData;

        // Undo/Redo Stacks
        private Stack<string> _undoStack = new Stack<string>();
        private Stack<string> _redoStack = new Stack<string>();
        private bool _isApplyingState = false;
        private string? _currentTemplateName = null;
        private bool _isEditingExisting = false;

        public CustomTemplateDesignerWindow(string? initialJsonOrId = null, string? templateName = null)
        {
            InitializeComponent();
            LoadPlaceholders();
            
            _currentTemplateName = templateName;
            _isEditingExisting = !string.IsNullOrEmpty(templateName);

            if (!string.IsNullOrEmpty(initialJsonOrId))
            {
                if (initialJsonOrId.StartsWith("[") || initialJsonOrId.StartsWith("{"))
                {
                    LoadTemplateFromJson(initialJsonOrId);
                }
                else
                {
                    // It's a template ID
                    var blocks = GetStandardBlocks(initialJsonOrId);
                    if (blocks.Count > 0 && blocks[0].IsHalfA4) radioHalfA4.IsChecked = true;
                    else radioFullA4.IsChecked = true;
                    
                    DesignerCanvas.Children.Clear();
                    foreach (var b in blocks) AddBlockToCanvas(b);
                }
            }

            // Initial zoom fit
            this.Loaded += (s, e) => {
                if (this.Height > SystemParameters.WorkArea.Height)
                    this.Height = SystemParameters.WorkArea.Height - 80;
                if (this.Width > SystemParameters.WorkArea.Width)
                    this.Width = SystemParameters.WorkArea.Width - 80;

                double availableWidth = DesignerScroller.ActualWidth - 100;
                double scale = availableWidth / 794.0;
                ZoomSlider.Value = Math.Max(0.1, Math.Min(1.0, scale));
                
                // Fix WPF AllowsTransparency WindowChrome rendering glitch
                var originalWidth = this.Width;
                this.Width = originalWidth + 1;
                this.Width = originalWidth;
            };
            this.PreviewKeyDown += Window_KeyDown;
            
            // Initial state for Undo
            PushState();
            
            // Fix: Loading initial blocks triggers PushState which sets _isDirty to true.
            // Reset it here so it only asks to save if the user actually changes something.
            _undoStack.Clear();
            PushState(); 
            _isDirty = false;
        }

        private void PushState()
        {
            if (_isApplyingState) return;
            var blocks = GetCurrentBlocks();
            string json = JsonSerializer.Serialize(blocks);
            
            // Don't push duplicate states
            if (_undoStack.Count > 0 && _undoStack.Peek() == json) return;
            
            _undoStack.Push(json);
            _redoStack.Clear();
            UpdateUndoRedoButtons();
            
            // Any state change makes it dirty
            _isDirty = true;
        }

        private void UpdateUndoRedoButtons()
        {
            if (btnUndo != null) btnUndo.IsEnabled = _undoStack.Count > 1;
            if (btnRedo != null) btnRedo.IsEnabled = _redoStack.Count > 0;
        }

        private List<DesignerBlock> GetCurrentBlocks()
        {
            var blocks = new List<DesignerBlock>();
            bool isHalfA4 = radioHalfA4.IsChecked == true;
            foreach (UIElement child in DesignerCanvas.Children)
            {
                if (child is Border border && border.Tag is DesignerBlock block)
                {
                    block.X = Canvas.GetLeft(border);
                    block.Y = Canvas.GetTop(border);
                    // Prioritize explicit Width/Height over ActualWidth/Height to avoid race conditions during save
                    block.Width = !double.IsNaN(border.Width) ? border.Width : border.ActualWidth;
                    block.Height = !double.IsNaN(border.Height) ? border.Height : border.ActualHeight;
                    block.IsHalfA4 = isHalfA4;
                    blocks.Add(block);
                }
            }
            return blocks;
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count <= 1) return;
            _redoStack.Push(_undoStack.Pop());
            ApplyState(_undoStack.Peek());
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count == 0) return;
            string state = _redoStack.Pop();
            _undoStack.Push(state);
            ApplyState(state);
        }

        private void ApplyState(string json)
        {
            _isApplyingState = true;
            LoadTemplateFromJson(json);
            _isApplyingState = false;
            UpdateUndoRedoButtons();
        }

        private void LoadTemplateFromJson(string json)
        {
            try
            {
                List<DesignerBlock>? blocks = null;
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("blocks", out var blocksProp)) {
                    blocks = JsonSerializer.Deserialize<List<DesignerBlock>>(blocksProp.GetRawText(), options);
                } else {
                    blocks = JsonSerializer.Deserialize<List<DesignerBlock>>(json, options);
                }

                if (blocks != null)
                {
                    _isLoadingTemplate = true;
                    DesignerCanvas.Children.Clear();
                    foreach (var b in blocks)
                    {
                        AddBlockToCanvas(b);
                        if (b.IsHalfA4) radioHalfA4.IsChecked = true;
                        else radioFullA4.IsChecked = true;
                    }
                    _isLoadingTemplate = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading template: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void LoadPlaceholders()
        {
            var groups = new List<ToolboxGroup>
            {
                new ToolboxGroup
                {
                    GroupName = "BASIC ELEMENTS",
                    Items = new List<PlaceholderInfo>
                    {
                        new PlaceholderInfo { Id = "custom_text", Name = "Custom Text", Icon = "🔤" },
                        new PlaceholderInfo { Id = "logo", Name = "Brand Logo", Icon = "🖼️" },
                        new PlaceholderInfo { Id = "custom_image", Name = "Custom Image", Icon = "📷" },
                        new PlaceholderInfo { Id = "table", Name = "Data Table", Icon = "📊" },
                        new PlaceholderInfo { Id = "line", Name = "Separator Line", Icon = "➖" }
                    }
                },
                new ToolboxGroup
                {
                    GroupName = "DASHBOARD FIELDS",
                    Items = new List<PlaceholderInfo>
                    {
                        new PlaceholderInfo { Id = "memo_id", Name = "Order ID", Icon = "🔢" },
                        new PlaceholderInfo { Id = "date", Name = "Memo Date", Icon = "📅" },
                        new PlaceholderInfo { Id = "customer_name", Name = "Customer Name", Icon = "👤" },
                        new PlaceholderInfo { Id = "customer_phone", Name = "Customer Phone", Icon = "📞" },
                        new PlaceholderInfo { Id = "customer_address", Name = "Customer Address", Icon = "🏠" },
                        new PlaceholderInfo { Id = "customer", Name = "Customer Info", Icon = "👥" },
                        new PlaceholderInfo { Id = "device_name", Name = "Device Name", Icon = "💻" },
                        new PlaceholderInfo { Id = "brand", Name = "Device Brand", Icon = "🏷️" },
                        new PlaceholderInfo { Id = "model", Name = "Model Name", Icon = "📱" },
                        new PlaceholderInfo { Id = "device", Name = "Device Summary", Icon = "🔧" },
                        new PlaceholderInfo { Id = "serial_number", Name = "Serial Number", Icon = "🔑" },
                        new PlaceholderInfo { Id = "accessories", Name = "Accessories", Icon = "🔌" },
                        new PlaceholderInfo { Id = "issue", Name = "Issue Description", Icon = "📝" },
                        new PlaceholderInfo { Id = "diagnostics", Name = "Diagnostics", Icon = "🔬" },
                        new PlaceholderInfo { Id = "cost", Name = "Estimated Cost", Icon = "💰" },
                        new PlaceholderInfo { Id = "itemized_costs", Name = "Itemized Costs", Icon = "📋" },
                        new PlaceholderInfo { Id = "technician_name", Name = "Technician", Icon = "👨‍🔧" }
                    }
                },
                new ToolboxGroup
                {
                    GroupName = "COMPANY INFO",
                    Items = new List<PlaceholderInfo>
                    {
                        new PlaceholderInfo { Id = "name", Name = "Company Name", Icon = "🏢" },
                        new PlaceholderInfo { Id = "address", Name = "Business Address", Icon = "📍" },
                        new PlaceholderInfo { Id = "phone", Name = "Company Phone", Icon = "📞" },
                        new PlaceholderInfo { Id = "terms", Name = "Terms & Conditions", Icon = "⚖️" }
                    }
                },
                new ToolboxGroup
                {
                    GroupName = "SIGNATURES",
                    Items = new List<PlaceholderInfo>
                    {
                        new PlaceholderInfo { Id = "customer_signature", Name = "Customer Sig.", Icon = "✍️" },
                        new PlaceholderInfo { Id = "technician_signature", Name = "Technician Sig.", Icon = "✍️" },
                        new PlaceholderInfo { Id = "company_signature", Name = "Company Sig.", Icon = "✍️" },
                        new PlaceholderInfo { Id = "signatures", Name = "Signature Block", Icon = "✒️" }
                    }
                },
                new ToolboxGroup
                {
                    GroupName = "SHAPES",
                    Items = new List<PlaceholderInfo>
                    {
                        new PlaceholderInfo { Id = "rect", Name = "Rectangle", Icon = "⬜" },
                        new PlaceholderInfo { Id = "circle", Name = "Circle", Icon = "⭕" },
                        new PlaceholderInfo { Id = "triangle", Name = "Triangle", Icon = "🔺" },
                        new PlaceholderInfo { Id = "polygon", Name = "Polygon", Icon = "⬠" }
                    }
                }
            };
            PlaceholderList.ItemsSource = groups;
        }

        private void LoadSavedTemplate()
        {
            if (string.IsNullOrEmpty(SettingsManager.Default.CustomTemplateJson)) return;
            try
            {
                List<DesignerBlock>? blocks = null;
                var doc = JsonDocument.Parse(SettingsManager.Default.CustomTemplateJson);
                
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("blocks", out var blocksProp)) {
                    blocks = JsonSerializer.Deserialize<List<DesignerBlock>>(blocksProp.GetRawText());
                } else {
                    blocks = JsonSerializer.Deserialize<List<DesignerBlock>>(SettingsManager.Default.CustomTemplateJson);
                }

                if (blocks != null && blocks.Count > 0) 
                { 
                    if (blocks[0].IsHalfA4) radioHalfA4.IsChecked = true;
                    foreach (var b in blocks) AddBlockToCanvas(b); 
                }
            }
            catch { }
        }

        private void Placeholder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PlaceholderInfo info)
            {
                DragDrop.DoDragDrop(fe, info.Id, DragDropEffects.Copy);
            }
        }

        private void DesignerCanvas_DragEnter(object sender, DragEventArgs e) { DropPreview.Visibility = Visibility.Visible; }
        private void DesignerCanvas_DragLeave(object sender, DragEventArgs e) { DropPreview.Visibility = Visibility.Collapsed; }

        private void DesignerCanvas_DragOver(object sender, DragEventArgs e) 
        { 
            e.Effects = DragDropEffects.Copy; 
            Point pos = e.GetPosition(DesignerCanvas);
            Canvas.SetLeft(DropPreview, pos.X);
            Canvas.SetTop(DropPreview, pos.Y);
            e.Handled = true; 
        }

        private void DesignerCanvas_Drop(object sender, DragEventArgs e)
        {
            DropPreview.Visibility = Visibility.Collapsed;
            if (e.Data.GetDataPresent(typeof(string)))
            {
                string id = e.Data.GetData(typeof(string)) as string ?? "";
                Point pos = e.GetPosition(DesignerCanvas);

                string imagePath = "";
                string customText = "";
                
                if (id == "custom_image")
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp"
                    };
                    if (dialog.ShowDialog() == true) imagePath = dialog.FileName;
                    else return;
                }
                else if (id == "custom_text")
                {
                    customText = "New Custom Text";
                }

                var block = new DesignerBlock
                {
                    Id = id,
                    X = pos.X,
                    Y = pos.Y,
                    ImagePath = imagePath,
                    CustomText = customText,
                    IsHalfA4 = radioHalfA4.IsChecked == true,
                    ColorHex = "#000000", // Explicit default to Black to ensure visibility
                    TableBackgroundColorHex = "Transparent"
                };

                if (id == "rect" || id == "circle" || id == "triangle" || id == "polygon") { block.Width = 100; block.Height = 100; block.ColorHex = "#CCCCCC"; }
                if (id == "triangle") block.PolygonSides = 3;
                if (id == "line") { block.Width = 600; block.Height = 2; block.ColorHex = "#CCCCCC"; }
                if (id == "table") { block.Width = 400; block.Height = 120; block.ColorHex = "#000000"; }
                if (id == "customer_signature" || id == "technician_signature" || id == "company_signature" || id == "signatures") { block.Width = 250; block.Height = 80; }
                if (id == "image") 
                {
                    var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
                    if (ofd.ShowDialog() == true)
                    {
                        block.ImagePath = ofd.FileName;
                        block.Width = 150;
                        block.Height = 150;
                    }
                    else return;
                }
                
                AddBlockToCanvas(block);
            }
        }

        private void AddBlockToCanvas(DesignerBlock b)
        {
            if (string.IsNullOrEmpty(b.ColorHex))
            {
                b.ColorHex = "#000000"; // Only default to black if actually empty
            }

            if (DesignerCanvas == null) return;
            try
            {
                var border = new Border
                {
                    Width = b.Width > 0 ? b.Width : 100,
                    Height = b.Height > 0 ? b.Height : 40,
                    Background = (b.Id == "rect" || b.Id == "circle" || b.Id == "image" || b.Id == "triangle" || b.Id == "polygon" || b.Id == "line") 
                        ? Brushes.Transparent 
                        : (string.IsNullOrEmpty(b.TableBackgroundColorHex) || b.TableBackgroundColorHex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(b.TableBackgroundColorHex)!),
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(b.Id == "rect" || b.Id == "circle" || b.Id == "image" || b.Id == "triangle" || b.Id == "polygon" ? b.ShapeBorderThickness : 0),
                    CornerRadius = b.Id == "circle" ? new CornerRadius(b.Width/2) : new CornerRadius(b.BorderRadius),
                    Padding = new Thickness(0),
                    Tag = b,
                    Cursor = Cursors.SizeAll,
                    Opacity = b.Opacity
                };

            if (b.Id == "line")
            {
                border.BorderBrush = (Brush)new BrushConverter().ConvertFromString(b.ColorHex)!;
                border.BorderThickness = new Thickness(0, 0, 0, b.Height);
            }

            if (b.Id == "rect" || b.Id == "circle" || b.Id == "image")
            {
                border.BorderBrush = b.BorderColorHex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(b.BorderColorHex)!;
            }

            if (b.Id == "rect" || b.Id == "circle")
            {
                border.Background = b.ColorHex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(b.ColorHex)!;
                border.Opacity = b.Opacity;
            }
            else if (b.Id == "triangle" || b.Id == "polygon")
            {
                border.Background = null;
                border.BorderBrush = null;
                border.BorderThickness = new Thickness(0);
                var polygon = new System.Windows.Shapes.Polygon
                {
                    Stretch = Stretch.Fill,
                    Fill = b.ColorHex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(b.ColorHex)!,
                    Stroke = b.BorderColorHex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(b.BorderColorHex)!,
                    StrokeThickness = b.ShapeBorderThickness,
                    Opacity = b.Opacity
                };
                var points = new PointCollection();
                int sides = b.PolygonSides < 3 ? 3 : b.PolygonSides;
                for (int i = 0; i < sides; i++)
                {
                    double angle = 2 * Math.PI * i / sides - Math.PI / 2;
                    points.Add(new Point(50 + 50 * Math.Cos(angle), 50 + 50 * Math.Sin(angle)));
                }
                polygon.Points = points;
                border.Child = polygon;
            }
            else if (b.Id == "image" && !string.IsNullOrEmpty(b.ImagePath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    if (b.ImagePath.StartsWith("data:image/"))
                    {
                        var base64Data = b.ImagePath.Split(',')[1];
                        var bytes = Convert.FromBase64String(base64Data);
                        bitmap.StreamSource = new MemoryStream(bytes);
                    }
                    else
                    {
                        bitmap.UriSource = new Uri(b.ImagePath, UriKind.RelativeOrAbsolute);
                    }
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    var img = new Image
                    {
                        Source = bitmap,
                        Stretch = Stretch.Uniform
                    };
                    border.Child = img;
                    border.Background = null;
                    border.BorderThickness = new Thickness(0);
                }
                catch { }
            }
            if (b.Id == "table")
            {
                var tableGrid = new Grid { ShowGridLines = false, Background = Brushes.Transparent };
                
                var colWidths = string.IsNullOrEmpty(b.TableColumnWidths) ? Enumerable.Repeat("1*", b.TableCols).ToList() : b.TableColumnWidths.Split(',').ToList();
                var rowHeights = string.IsNullOrEmpty(b.TableRowHeights) ? Enumerable.Repeat("1*", b.TableRows).ToList() : b.TableRowHeights.Split(',').ToList();

                for(int i=0; i<b.TableCols; i++) 
                {
                    double w = 1; GridUnitType t = GridUnitType.Star;
                    if (i < colWidths.Count && colWidths[i].EndsWith("*")) { double.TryParse(colWidths[i].TrimEnd('*'), out w); }
                    else if (i < colWidths.Count) { double.TryParse(colWidths[i], out w); t = GridUnitType.Pixel; }
                    tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, t) });
                }
                for(int i=0; i<b.TableRows; i++) 
                {
                    double h = 1; GridUnitType t = GridUnitType.Star;
                    if (i < rowHeights.Count && rowHeights[i].EndsWith("*")) { double.TryParse(rowHeights[i].TrimEnd('*'), out h); }
                    else if (i < rowHeights.Count) { double.TryParse(rowHeights[i], out h); t = GridUnitType.Pixel; }
                    tableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(h, t) });
                }

                List<TableCellData> cells;
                if (string.IsNullOrEmpty(b.TableCellsJson))
                {
                    cells = new List<TableCellData>();
                    for (int r = 0; r < b.TableRows; r++)
                    {
                        for (int c = 0; c < b.TableCols; c++)
                        {
                            cells.Add(new TableCellData { Row = r, Col = c, Text = "" });
                        }
                    }
                    b.TableCellsJson = JsonSerializer.Serialize(cells);
                }
                else
                {
                    cells = JsonSerializer.Deserialize<List<TableCellData>>(b.TableCellsJson) ?? new List<TableCellData>();
                }

                foreach (var cellData in cells)
                {
                    var cellBorder = new Border 
                    { 
                        BorderBrush = (Brush)new BrushConverter().ConvertFromString(cellData.BorderColor)!, 
                        BorderThickness = new Thickness(cellData.BorderL, cellData.BorderT, cellData.BorderR, cellData.BorderB),
                        Background = cellData.BackgroundColor == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(cellData.BackgroundColor)!,
                        Tag = cellData
                    };
                    var cellTxt = new RichTextBox 
                    { 
                        HorizontalAlignment = HorizontalAlignment.Stretch, 
                        VerticalAlignment = VerticalAlignment.Stretch, 
                        AcceptsReturn = true,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(4),
                        Document = new FlowDocument { PagePadding = new Thickness(0) }
                    };
                    TextOptions.SetTextFormattingMode(cellTxt, TextFormattingMode.Display);
                    TextOptions.SetTextRenderingMode(cellTxt, TextRenderingMode.ClearType);
                    
                    SetXaml(cellTxt, cellData.FormattedTextXaml, cellData.Text, b, cellData.TextAlignment);

                    cellTxt.TextChanged += (s, e) => { 
                        if (_isUpdatingUI) return;
                        var range = new TextRange(cellTxt.Document.ContentStart, cellTxt.Document.ContentEnd);
                        cellData.Text = range.Text.TrimEnd('\r', '\n'); 
                        cellData.FormattedTextXaml = GetXaml(cellTxt);
                        SaveTableCells(b, cells); 
                        AutoExpandBlock(border, b);
                    };
                    
                    cellBorder.Child = cellTxt;
                    Grid.SetRow(cellBorder, cellData.Row); 
                    Grid.SetColumn(cellBorder, cellData.Col);
                    Grid.SetRowSpan(cellBorder, cellData.RowSpan);
                    Grid.SetColumnSpan(cellBorder, cellData.ColSpan);
                    cellTxt.IsHitTestVisible = false;
                    cellTxt.LostFocus += (s, e) => cellTxt.IsHitTestVisible = false;
                    
                    cellBorder.MouseLeftButtonDown += (s, e) => {
                        if (e.ClickCount == 2) {
                            cellTxt.IsHitTestVisible = true;
                            cellTxt.Focus();
                            e.Handled = true;
                            return;
                        }
                        _isDraggingCells = true;
                        SelectTableCell(cellBorder, cellData, b, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                        SelectElement(border);
                        Block_MouseDown(border, e);
                    };
                    cellBorder.MouseEnter += (s, e) => {
                        if (_isDraggingCells && e.LeftButton == MouseButtonState.Pressed) {
                            SelectTableCell(cellBorder, cellData, b, true);
                        }
                    };

                    tableGrid.Children.Add(cellBorder);
                    if (cellData.BorderStyle != "Solid") {
                        cellBorder.BorderBrush = Brushes.Transparent;
                        var dashArray = cellData.BorderStyle == "Dotted" ? new System.Windows.Media.DoubleCollection { 1, 2 } : new System.Windows.Media.DoubleCollection { 4, 2 };
                        var overlay = new System.Windows.Shapes.Rectangle {
                            Stroke = (Brush)new BrushConverter().ConvertFromString(cellData.BorderColor)!,
                            StrokeThickness = Math.Max(cellData.BorderL, Math.Max(cellData.BorderT, Math.Max(cellData.BorderR, cellData.BorderB))),
                            StrokeDashArray = dashArray,
                            IsHitTestVisible = false
                        };
                        Grid.SetRow(overlay, cellData.Row);
                        Grid.SetColumn(overlay, cellData.Col);
                        Grid.SetRowSpan(overlay, cellData.RowSpan);
                        Grid.SetColumnSpan(overlay, cellData.ColSpan);
                        tableGrid.Children.Add(overlay);
                    }
                }

                for (int c = 1; c < b.TableCols; c++)
                {
                    var splitter = new GridSplitter { Width = 3, HorizontalAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Cursor = Cursors.SizeWE };
                    Grid.SetColumn(splitter, c);
                    Grid.SetRowSpan(splitter, b.TableRows);
                    splitter.DragCompleted += (s, e) => { SaveTableLayout(b, tableGrid); };
                    tableGrid.Children.Add(splitter);
                }
                for (int r = 1; r < b.TableRows; r++)
                {
                    var splitter = new GridSplitter { Height = 3, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent, Cursor = Cursors.SizeNS };
                    Grid.SetRow(splitter, r);
                    Grid.SetColumnSpan(splitter, b.TableCols);
                    splitter.DragCompleted += (s, e) => { SaveTableLayout(b, tableGrid); };
                    tableGrid.Children.Add(splitter);
                }

                border.Child = tableGrid;
            }
            else if (b.Id != "line" && b.Id != "rect" && b.Id != "circle" && b.Id != "triangle" && b.Id != "polygon" && b.Id != "image")
            {
                string displayText = (tglPreviewPlaceholders != null && tglPreviewPlaceholders.IsChecked == true) ? GetPreviewText(b.Id) : GetPlaceholderText(b.Id);
                if (b.Id == "custom_text") displayText = b.CustomText;

                var txt = new RichTextBox
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    AcceptsReturn = true,
                    Document = new FlowDocument { PagePadding = new Thickness(0) },
                    IsHitTestVisible = false
                };
                TextOptions.SetTextFormattingMode(txt, TextFormattingMode.Display);
                TextOptions.SetTextRenderingMode(txt, TextRenderingMode.ClearType);
                
                var pStyle = new Style(typeof(Paragraph));
                pStyle.Setters.Add(new Setter(Paragraph.MarginProperty, new Thickness(0)));
                txt.Document.Resources.Add(typeof(Paragraph), pStyle);
                txt.Document.LineHeight = 1.0;

                string xamlToLoad = b.FormattedTextXaml;
                if (tglPreviewPlaceholders != null && tglPreviewPlaceholders.IsChecked == true && !string.IsNullOrEmpty(xamlToLoad)) {
                    xamlToLoad = xamlToLoad.Replace(GetPlaceholderText(b.Id), GetPreviewText(b.Id));
                }
                SetXaml(txt, xamlToLoad, displayText, b);

                txt.TextChanged += (s, e) => {
                    if (_isUpdatingUI) return;
                    if (b.Id == "custom_text" || b.Id == "name" || b.Id == "address" || b.Id == "phone" || b.Id == "memo_id" || b.Id == "date" || b.Id == "customer" || b.Id == "device" || b.Id == "issue" || b.Id == "diagnostics" || b.Id == "cost" || b.Id == "technician_name" || b.Id == "customer_signature" || b.Id == "technician_signature" || b.Id == "company_signature" || b.Id == "signatures")
                    {
                        var range = new TextRange(txt.Document.ContentStart, txt.Document.ContentEnd);
                        if (b.Id == "custom_text") b.CustomText = range.Text.TrimEnd('\r', '\n');
                        string xaml = GetXaml(txt);
                        if (b.Id != "custom_text")
                        {
                            string placeholderText = GetPlaceholderText(b.Id);
                            string previewText = GetPreviewText(b.Id);
                            
                            if (!string.IsNullOrEmpty(placeholderText)) xaml = xaml.Replace(placeholderText, "{" + b.Id + "}");
                            if (!string.IsNullOrEmpty(previewText)) xaml = xaml.Replace(previewText, "{" + b.Id + "}");
                            
                            if (b.Id == "name") { xaml = xaml.Replace(GetPlaceholderText("company_name"), "{name}").Replace(GetPreviewText("company_name"), "{name}"); }
                            if (b.Id == "address") { xaml = xaml.Replace(GetPlaceholderText("company_address"), "{address}").Replace(GetPreviewText("company_address"), "{address}"); }
                            if (b.Id == "phone") { xaml = xaml.Replace(GetPlaceholderText("company_phone"), "{phone}").Replace(GetPreviewText("company_phone"), "{phone}"); }
                            if (b.Id == "issue") { xaml = xaml.Replace(GetPlaceholderText("description"), "{issue}").Replace(GetPreviewText("description"), "{issue}"); }
                        }
                        b.FormattedTextXaml = xaml;
                        AutoExpandBlock(border, b);
                    }
                };

                txt.LostFocus += (s, e) => { txt.IsHitTestVisible = false; };
                
                bool isPlaceholder = !(b.Id == "custom_text" || b.Id == "table");
                if (isPlaceholder)
                {
                    txt.IsReadOnly = true;
                    txt.Cursor = Cursors.IBeam;
                }

                border.MouseLeftButtonDown += (s, e) => {
                    if (e.ClickCount >= 2 && isPlaceholder)
                    {
                        ShowToast("Placeholder values are not editable. Use 'Custom Text' for your own text.");
                        e.Handled = true;
                    }
                };

                txt.PreviewKeyDown += (s, e) => {
                    if (isPlaceholder)
                    {
                        bool isNavigation = (e.Key >= Key.Left && e.Key <= Key.Down) || e.Key == Key.PageUp || e.Key == Key.PageDown || e.Key == Key.Home || e.Key == Key.End;
                        bool isCopy = e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                        
                        if (!isNavigation && !isCopy)
                        {
                            ShowToast("Placeholder values are not editable. Use 'Custom Text' for your own text.");
                            e.Handled = true;
                        }
                    }
                };

                if (b.Id == "logo" || b.Id == "custom_image")
                {
                    string path = b.ImagePath;
                    if (string.IsNullOrEmpty(path) && b.Id == "logo") path = SettingsManager.Default.CompanyLogoPath;

                    if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                            bitmap.EndInit();

                            var img = new Image
                            {
                                Source = bitmap,
                                Stretch = Stretch.Uniform
                            };
                            border.Child = img;
                        }
                        catch { border.Child = txt; }
                    }
                    else
                    {
                        border.Child = txt;
                    }
                }
                else
                {
                    border.Child = txt;
                }
            }
            
            if (b.Id != "line" && b.Id != "rect" && b.Id != "circle" && b.Id != "triangle" && b.Id != "polygon")
            {
                AutoExpandBlock(border, b);
            }

            Canvas.SetLeft(border, b.X);
            Canvas.SetTop(border, b.Y);

            var wrapper = new Grid();
            var handleList = new List<Border>();
            string[] cursors = { "SizeNWSE", "SizeNESW", "SizeNESW", "SizeNWSE" };
            HorizontalAlignment[] hAligns = { HorizontalAlignment.Left, HorizontalAlignment.Right, HorizontalAlignment.Left, HorizontalAlignment.Right };
            VerticalAlignment[] vAligns = { VerticalAlignment.Top, VerticalAlignment.Top, VerticalAlignment.Bottom, VerticalAlignment.Bottom };

            for (int i = 0; i < 4; i++)
            {
                var h = new Border
                {
                    Width = 10, Height = 10,
                    Background = Brushes.RoyalBlue,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    HorizontalAlignment = hAligns[i],
                    VerticalAlignment = vAligns[i],
                    Margin = new Thickness(hAligns[i] == HorizontalAlignment.Left ? -5 : 0, vAligns[i] == VerticalAlignment.Top ? -5 : 0, hAligns[i] == HorizontalAlignment.Right ? -5 : 0, vAligns[i] == VerticalAlignment.Bottom ? -5 : 0),
                    Cursor = (Cursor)typeof(Cursors).GetProperty(cursors[i])!.GetValue(null)!,
                    Visibility = Visibility.Collapsed
                };
                
                int index = i;
                bool isResizing = false;
                Point lastPos = new Point();

                h.MouseLeftButtonDown += (s, e) => {
                    isResizing = true;
                    if (double.IsNaN(border.Width)) border.Width = border.ActualWidth;
                    if (double.IsNaN(border.Height)) border.Height = border.ActualHeight;
                    lastPos = e.GetPosition(DesignerCanvas);
                    h.CaptureMouse();
                    e.Handled = true;
                };
                h.MouseMove += (s, e) => {
                    if (isResizing)
                    {
                        var curr = e.GetPosition(DesignerCanvas);
                        double dx = curr.X - lastPos.X;
                        double dy = curr.Y - lastPos.Y;
                        
                        if (index == 0) {
                            double nw = Math.Max(20, border.Width - dx);
                            if (border.Width - dx > 20) { Canvas.SetLeft(border, Canvas.GetLeft(border) + dx); border.Width = nw; }
                            
                            if (b.Id != "line") {
                                double nh = Math.Max(20, border.Height - dy);
                                if (border.Height - dy > 20) { Canvas.SetTop(border, Canvas.GetTop(border) + dy); border.Height = nh; }
                            }
                        }
                        else if (index == 1) {
                            border.Width = Math.Max(20, border.Width + dx);
                            
                            if (b.Id != "line") {
                                double nh = Math.Max(20, border.Height - dy);
                                if (border.Height - dy > 20) { Canvas.SetTop(border, Canvas.GetTop(border) + dy); border.Height = nh; }
                            }
                        }
                        else if (index == 2) {
                            double nw = Math.Max(20, border.Width - dx);
                            if (border.Width - dx > 20) { Canvas.SetLeft(border, Canvas.GetLeft(border) + dx); border.Width = nw; }
                            
                            if (b.Id != "line") {
                                border.Height = Math.Max(20, border.Height + dy);
                            }
                        }
                        else if (index == 3) {
                            border.Width = Math.Max(20, border.Width + dx);
                            if (b.Id != "line") {
                                border.Height = Math.Max(20, border.Height + dy);
                            }
                        }
                        
                        b.Width = border.Width; 
                        if (b.Id != "line") b.Height = border.Height;
                        b.X = Canvas.GetLeft(border); b.Y = Canvas.GetTop(border);
                        lastPos = curr;
                    }
                };
                h.MouseLeftButtonUp += (s, e) => { isResizing = false; h.ReleaseMouseCapture(); };
                handleList.Add(h);
            }

            var content = border.Child;
            border.Child = null;
            if (content != null) wrapper.Children.Add(content);
            foreach (var h in handleList) wrapper.Children.Add(h);
            
            if (b.Id == "table") {
                var btnAddRow = new Button { Content = "+Row", Width = 30, Height = 20, FontSize=9, Background = Brushes.BlueViolet, Foreground = Brushes.White };
                var btnAddCol = new Button { Content = "+Col", Width = 30, Height = 20, FontSize=9, Background = Brushes.BlueViolet, Foreground = Brushes.White };
                btnAddRow.Click += (s, e) => { AddTableRow_Click(s!, e!); e.Handled = true; };
                btnAddCol.Click += (s, e) => { AddTableCol_Click(s!, e!); e.Handled = true; };
                var rowBtnBorder = new Border { Child = btnAddRow, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(15,0,0,-24), Visibility = Visibility.Collapsed };
                var colBtnBorder = new Border { Child = btnAddCol, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0,-24,15,0), Visibility = Visibility.Collapsed };
                handleList.Add(rowBtnBorder);
                handleList.Add(colBtnBorder);
                wrapper.Children.Add(rowBtnBorder);
                wrapper.Children.Add(colBtnBorder);
            }

            border.Child = wrapper;
            _elementHandles[border] = handleList;

            
            border.MouseLeftButtonDown += (s, e) => { 
                if (e.ClickCount == 2 && b.Id != "line" && b.Id != "rect" && b.Id != "circle" && b.Id != "image" && b.Id != "table" && b.Id != "triangle" && b.Id != "polygon") {
                    var wrapperGrid = border.Child as Grid;
                    if (wrapperGrid != null) {
                        var txt = wrapperGrid.Children.OfType<RichTextBox>().FirstOrDefault();
                        if (txt != null) {
                            txt.IsHitTestVisible = true;
                            txt.Focus();
                        }
                    }
                    e.Handled = true;
                    return;
                }
                
                bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                SelectElement(border, shift); 
                Block_MouseDown(s, e); 
            };
            border.MouseMove += Block_MouseMove;
            border.MouseLeftButtonUp += Block_MouseUp;

            var cm = new ContextMenu();
            var miBringFront = new MenuItem { Header = "Bring to Front" };
            miBringFront.Click += (s, ev) => { Panel.SetZIndex(border, GetMaxZIndex() + 1); };
            var miSendBack = new MenuItem { Header = "Send to Back" };
            miSendBack.Click += (s, ev) => { Panel.SetZIndex(border, GetMinZIndex() - 1); };
            var miDelete = new MenuItem { Header = "Delete" };
            miDelete.Click += (s, ev) => { 
                DesignerCanvas.Children.Remove(border); 
                if (_selectedElement == border) { PropertiesPanel.Visibility = Visibility.Hidden; _selectedElement = null; } 
            };
            cm.Items.Add(miBringFront);
            cm.Items.Add(miSendBack);
            cm.Items.Add(new Separator());

            if (b.Id != "line" && b.Id != "rect" && b.Id != "circle" && b.Id != "image" && b.Id != "triangle" && b.Id != "polygon" && b.Id != "table")
            {
                var miBg = new MenuItem { Header = "Background Color" };
                
                var miBgTransparent = new MenuItem { Header = "Transparent" };
                miBgTransparent.Click += (s, ev) => {
                    b.TableBackgroundColorHex = "Transparent";
                    border.Background = Brushes.Transparent;
                    PushState();
                };
                
                var miBgWhite = new MenuItem { Header = "White" };
                miBgWhite.Click += (s, ev) => {
                    b.TableBackgroundColorHex = "#FFFFFF";
                    border.Background = Brushes.White;
                    PushState();
                };

                var miBgLightGray = new MenuItem { Header = "Light Gray" };
                miBgLightGray.Click += (s, ev) => {
                    b.TableBackgroundColorHex = "#F3F4F6";
                    border.Background = (Brush)new BrushConverter().ConvertFromString("#F3F4F6")!;
                    PushState();
                };

                var miBgLightYellow = new MenuItem { Header = "Light Yellow" };
                miBgLightYellow.Click += (s, ev) => {
                    b.TableBackgroundColorHex = "#FEF3C7";
                    border.Background = (Brush)new BrushConverter().ConvertFromString("#FEF3C7")!;
                    PushState();
                };

                miBg.Items.Add(miBgTransparent);
                miBg.Items.Add(miBgWhite);
                miBg.Items.Add(miBgLightGray);
                miBg.Items.Add(miBgLightYellow);
                cm.Items.Add(miBg);
                cm.Items.Add(new Separator());
            }

            cm.Items.Add(miDelete);
            border.ContextMenu = cm;

            DesignerCanvas.Children.Add(border);
            AutoExpandBlock(border, b);
            PushState();
            } catch { }
        }

        private void AutoExpandBlock(Border border, DesignerBlock b)
        {
            if (b.Id == "rect" || b.Id == "circle" || b.Id == "image" || b.Id == "triangle" || b.Id == "polygon" || b.Id == "line") return;

            border.UpdateLayout();
            
            if (border.Child is RichTextBox rtb)
            {
                if (rtb.Document != null) rtb.Document.PagePadding = new Thickness(2);
                rtb.Padding = new Thickness(0);

                border.Dispatcher.BeginInvoke(new Action(() => {
                    if (b.Width <= 0)
                    {
                        rtb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        b.Width = Math.Max(180, rtb.DesiredSize.Width + 20); 
                        border.Width = b.Width;
                    }

                    double width = b.Width;
                    
                    rtb.Measure(new Size(width, double.PositiveInfinity));
                    double neededHeight = rtb.DesiredSize.Height + 20; 
                    
                    if (rtb.Document != null)
                        rtb.Document.TextAlignment = (TextAlignment)Enum.Parse(typeof(TextAlignment), b.TextAlignment);
                    
                    border.MinHeight = 20; 
                    
                    if (neededHeight > b.Height || b.Height < 10) 
                    {
                        b.Height = neededHeight;
                        border.Height = neededHeight;
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            else if (border.Child is Grid g && b.Id == "table")
            {
                if (b.Width <= 0) { b.Width = 400; border.Width = 400; }
                
                g.Measure(new Size(b.Width, double.PositiveInfinity));
                double neededHeight = g.DesiredSize.Height + 5;
                border.MinHeight = neededHeight;
                if (!_isLoadingTemplate && neededHeight > b.Height)
                {
                    b.Height = neededHeight;
                    border.Height = neededHeight;
                }
            }
        }

        private void CanvasSize_Checked(object sender, RoutedEventArgs e)
        {
            if (CanvasBorder == null) return;
            bool isHalf = radioHalfA4.IsChecked ?? false;
            CanvasBorder.Height = isHalf ? 561 : 1123;
            
            foreach (UIElement child in DesignerCanvas.Children)
            {
                double top = Canvas.GetTop(child);
                if (isHalf && top > 500) Canvas.SetTop(child, 450);
            }
        }

        private void SelectElement(UIElement el, bool isMultiSelect = false)
        {
            if (!isMultiSelect) DeselectAll(hideProperties: false);
            
            if (el is Border selectedBorder) {
                if (!isMultiSelect) _selectedElements.Clear();
                if (!_selectedElements.Contains(selectedBorder)) _selectedElements.Add(selectedBorder);
                
                _selectedElement = el;
                this.Focus();
                PropertiesPanel.Visibility = Visibility.Visible;
                
                if (selectedBorder.Tag is DesignerBlock b)
                {
                    foreach(var selectedEl in _selectedElements) {
                        if (_elementHandles.ContainsKey(selectedEl)) {
                            foreach (var h in _elementHandles[selectedEl]) h.Visibility = Visibility.Collapsed;
                            foreach (var h in _elementHandles[selectedEl]) h.Visibility = Visibility.Visible;
                        }
                        
                        selectedEl.Effect = new DropShadowEffect {
                            Color = Colors.BlueViolet,
                            BlurRadius = 0,
                            ShadowDepth = 0,
                            Opacity = 1,
                            RenderingBias = RenderingBias.Quality
                        };
                        
                        selectedEl.BorderBrush = Brushes.BlueViolet;
                        selectedEl.BorderThickness = new Thickness(1);
                        selectedEl.Margin = new Thickness(-1);
                    }

                    if (lblTextColor != null) lblTextColor.Text = "TEXT COLOR";
                    
                    if (tglBold == null) return;

                    _selectedBlock = b;
                    _isUpdatingUI = true;
                    
                    bool isCustom = b.Id == "custom_text";
                    lblCustomText.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
                    txtCustomText.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
                    if (isCustom) txtCustomText.Text = b.CustomText;

                    tglAlignLeft.IsChecked = b.TextAlignment == "Left";
                    tglAlignCenter.IsChecked = b.TextAlignment == "Center";
                    tglAlignRight.IsChecked = b.TextAlignment == "Right";

                    lblSelectedElement.Text = _selectedElements.Count > 1 ? "MULTIPLE SELECTED" : b.Id.ToUpper().Replace("_", " ");
                    sliderFontSize.Value = b.FontSize;
                    sliderOpacity.Value = b.Opacity;
                    tglBold.IsChecked = b.IsBold;
                    tglItalic.IsChecked = b.IsItalic;
                    tglUnderline.IsChecked = b.IsUnderlined;
                    
                    foreach (ComboBoxItem item in comboFontFamily.Items)
                    {
                        if (item.Content.ToString() == b.FontFamily)
                        {
                            comboFontFamily.SelectedItem = item;
                            comboFontFamily.Foreground = (Brush)new BrushConverter().ConvertFromString("#1A73E8")!;
                            break;
                        }
                    }
                    
                    if (b.Id == "table" && !isMultiSelect) {
                        TablePropertiesPanel.Visibility = Visibility.Visible;
                    } else {
                        TablePropertiesPanel.Visibility = Visibility.Collapsed;
                        if (_selectedTableCell != null) _selectedTableCell.Opacity = 1.0;
                        _selectedTableCell = null;
                        _selectedTableCellData = null;
                    }
                    
                    if (b.Id == "rect" || b.Id == "circle" || b.Id == "image" || b.Id == "triangle" || b.Id == "polygon") {
                        ShapePropertiesPanel.Visibility = Visibility.Visible;
                        if (lblTextColor != null) lblTextColor.Text = "BACKGROUND COLOR";
                        sliderShapeBorderRadius.Value = b.BorderRadius;
                        sliderShapeBorderWeight.Value = b.ShapeBorderThickness;
                        if (b.Id == "polygon" || b.Id == "triangle") {
                            lblPolygonSides.Visibility = Visibility.Visible;
                            lblPolygonSidesValue.Visibility = Visibility.Visible;
                            sliderPolygonSides.Visibility = Visibility.Visible;
                            sliderPolygonSides.Value = b.PolygonSides;
                            lblPolygonSidesValue.Text = b.PolygonSides.ToString();
                            lblShapeBorderRadius.Visibility = Visibility.Collapsed;
                            sliderShapeBorderRadius.Visibility = Visibility.Collapsed;
                        } else {
                            lblPolygonSides.Visibility = Visibility.Collapsed;
                            lblPolygonSidesValue.Visibility = Visibility.Collapsed;
                            sliderPolygonSides.Visibility = Visibility.Collapsed;
                        lblShapeBorderRadius.Visibility = Visibility.Visible;
                        sliderShapeBorderRadius.Visibility = Visibility.Visible;
                    }
                } else {
                    ShapePropertiesPanel.Visibility = Visibility.Collapsed;
                }
                    
                    UpdateColorUI("Text", b.ColorHex ?? "#000000");
                    UpdateColorUI("ShapeBorder", b.BorderColorHex ?? "Transparent");
                    if (b.Id == "table") {
                        UpdateColorUI("TableBg", b.TableBackgroundColorHex ?? "Transparent");
                        UpdateColorUI("TableBorder", b.BorderColorHex ?? "#000000");
                    }
                    
                    _isUpdatingUI = false;
                }
            }
        }

        private void FontFamily_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUI) return;
            if (comboFontFamily.SelectedItem is ComboBoxItem item)
            {
                string font = item.Content.ToString()!;
                _originalFontFamilyName = null;

                bool handledBySelection = false;
                if (Keyboard.FocusedElement is RichTextBox rtb && !rtb.Selection.IsEmpty)
                {
                    ApplyRichTextProperty(TextElement.FontFamilyProperty, new FontFamily(font));
                    handledBySelection = true;
                }

                foreach(var border in _selectedElements) {
                    if (border.Tag is DesignerBlock block) {
                        block.FontFamily = font;
                        if (!handledBySelection)
                        {
                            var txt = GetRichTextBox(border);
                            if (txt != null) 
                            {
                                var tr = new TextRange(txt.Document.ContentStart, txt.Document.ContentEnd);
                                tr.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(font));
                            }
                            
                            if (block.Id == "table") {
                                foreach(var tb in GetTableRichTextBoxes(border)) 
                                {
                                    var tr = new TextRange(tb.Document.ContentStart, tb.Document.ContentEnd);
                                    tr.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(font));
                                }
                            }
                        }
                    }
                }
            }
            PushState();
        }

        private void FontItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is ComboBoxItem item && Keyboard.FocusedElement is RichTextBox rtb)
            {
                string font = item.Content.ToString()!;
                if (_originalFontFamilyName == null)
                {
                    var currentFont = rtb.Selection.GetPropertyValue(TextElement.FontFamilyProperty);
                    _originalFontFamilyName = currentFont?.ToString() ?? "Inter";
                }
                rtb.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(font));
            }
        }

        private void FontItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_originalFontFamilyName != null && Keyboard.FocusedElement is RichTextBox rtb)
            {
                rtb.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(_originalFontFamilyName));
            }
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            if (sender is Button btn)
            {
                string hex = btn.Tag.ToString()!;
                var color = hex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(hex)!;
                foreach(var border in _selectedElements) {
                    if (border.Tag is DesignerBlock block) {
                            if (ApplyRichTextProperty(TextElement.ForegroundProperty, color)) continue;

                            block.ColorHex = hex;
                            if (block.Id != "table") border.BorderBrush = color;
                            
                            var txt = GetRichTextBox(border);
                            if (txt != null) 
                            {
                                var tr = new TextRange(txt.Document.ContentStart, txt.Document.ContentEnd);
                                tr.ApplyPropertyValue(TextElement.ForegroundProperty, color);
                            }
                            
                            if (block.Id == "table") {
                                foreach(var tb in GetTableRichTextBoxes(border)) {
                                    var tr = new TextRange(tb.Document.ContentStart, tb.Document.ContentEnd);
                                    tr.ApplyPropertyValue(TextElement.ForegroundProperty, color);
                                }
                            }
                            
                            if (block.Id == "line") border.BorderBrush = color;
                            if (block.Id == "rect" || block.Id == "circle") border.Background = color;
                            if (block.Id == "triangle" || block.Id == "polygon") {
                                if (border.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is System.Windows.Shapes.Polygon poly) {
                                    poly.Fill = color;
                                }
                            }
                            
                            UpdateColorUI("Text", hex);
                        }
                    }
                }
                PushState();
        }

        private void UpdateColorUI(string type, string hex)
        {
            _isUpdatingUI = true;
            try {
                if (type == "Text") {
                    txtTextColorHex.Text = hex;
                    previewTextColor.Background = hex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(hex)!;
                } else if (type == "ShapeBorder") {
                    txtShapeBorderColorHex.Text = hex;
                    previewShapeBorderColor.Background = hex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(hex)!;
                } else if (type == "TableBg") {
                    txtTableBgColorHex.Text = hex;
                    previewTableBgColor.Background = hex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(hex)!;
                } else if (type == "TableBorder") {
                    txtTableBorderColorHex.Text = hex;
                    previewTableBorderColor.Background = hex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(hex)!;
                } else if (type == "CellBg") {
                    txtCellBgColorHex.Text = hex;
                    previewCellBgColor.Background = hex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(hex)!;
                } else if (type == "CellBorder") {
                    txtCellBorderColorHex.Text = hex;
                    previewCellBorderColor.Background = hex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(hex)!;
                }
            } catch {}
            _isUpdatingUI = false;
        }

        private void FontSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUI) return;
            foreach(var border in _selectedElements) {
                if (border.Tag is DesignerBlock block) {
                    if (ApplyRichTextProperty(TextElement.FontSizeProperty, e.NewValue)) continue;

                    block.FontSize = e.NewValue;
                    var txt = GetRichTextBox(border);
                    if (txt != null) 
                    {
                        var tr = new TextRange(txt.Document.ContentStart, txt.Document.ContentEnd);
                        tr.ApplyPropertyValue(TextElement.FontSizeProperty, e.NewValue);
                    }
                    
                    if (block.Id == "table") {
                        foreach(var tb in GetTableRichTextBoxes(border)) 
                        {
                            var tr = new TextRange(tb.Document.ContentStart, tb.Document.ContentEnd);
                            tr.ApplyPropertyValue(TextElement.FontSizeProperty, e.NewValue);
                        }
                    }
                }
            }
            PushState();
        }

        private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUI) return;
            foreach(var border in _selectedElements) {
                if (border.Tag is DesignerBlock block) {
                    block.Opacity = e.NewValue;
                    border.Opacity = block.Opacity;
                }
            }
            PushState();
        }

        private void Style_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            bool bBold = tglBold.IsChecked ?? false;
            bool bItal = tglItalic.IsChecked ?? false;
            bool bUndr = tglUnderline.IsChecked ?? false;

            bool handledBySelection = false;
            if (Keyboard.FocusedElement is RichTextBox rtb && !rtb.Selection.IsEmpty)
            {
                ApplyRichTextProperty(TextElement.FontWeightProperty, bBold ? FontWeights.Bold : FontWeights.Normal);
                ApplyRichTextProperty(TextElement.FontStyleProperty, bItal ? FontStyles.Italic : FontStyles.Normal);
                ApplyRichTextProperty(Inline.TextDecorationsProperty, bUndr ? TextDecorations.Underline : null);
                handledBySelection = true;
            }

            foreach(var border in _selectedElements) {
                if (border.Tag is DesignerBlock block) {
                    block.IsBold = bBold;
                    block.IsItalic = bItal;
                    block.IsUnderlined = bUndr;
                    
                    if (!handledBySelection)
                    {
                        var txt = GetRichTextBox(border);
                        if (txt != null)
                        {
                            var tr = new TextRange(txt.Document.ContentStart, txt.Document.ContentEnd);
                            tr.ApplyPropertyValue(TextElement.FontWeightProperty, bBold ? FontWeights.Bold : FontWeights.Normal);
                            tr.ApplyPropertyValue(TextElement.FontStyleProperty, bItal ? FontStyles.Italic : FontStyles.Normal);
                            tr.ApplyPropertyValue(Inline.TextDecorationsProperty, bUndr ? TextDecorations.Underline : null);
                        }
                        if (block.Id == "table") {
                            foreach(var tb in GetTableRichTextBoxes(border)) {
                                var tr = new TextRange(tb.Document.ContentStart, tb.Document.ContentEnd);
                                tr.ApplyPropertyValue(TextElement.FontWeightProperty, bBold ? FontWeights.Bold : FontWeights.Normal);
                                tr.ApplyPropertyValue(TextElement.FontStyleProperty, bItal ? FontStyles.Italic : FontStyles.Normal);
                                tr.ApplyPropertyValue(Inline.TextDecorationsProperty, bUndr ? TextDecorations.Underline : null);
                            }
                        }
                    }
                }
            }
            PushState();
        }

        private void CustomText_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI) return;
            foreach(var border in _selectedElements) {
                if (border.Tag is DesignerBlock block) {
                    block.CustomText = txtCustomText.Text;
                    block.FormattedTextXaml = "";
                    var txt = GetRichTextBox(border);
                    if (txt != null) SetXaml(txt, "", txtCustomText.Text, block);
                }
            }
        }

        private RichTextBox? GetRichTextBox(Border b)
        {
            if (b.Child is RichTextBox t) return t;
            if (b.Child is Grid g)
            {
                foreach (var child in g.Children) if (child is RichTextBox t2) return t2;
            }
            return null;
        }

        private List<RichTextBox> GetTableRichTextBoxes(Border b)
        {
            var list = new List<RichTextBox>();
            if (b.Tag is DesignerBlock block && block.Id == "table" && b.Child is Grid wrapperGrid)
            {
                foreach (var wChild in wrapperGrid.Children)
                {
                    if (wChild is Grid tableGrid)
                    {
                        foreach (var tChild in tableGrid.Children)
                        {
                            if (tChild is Border cellBorder && cellBorder.Child is RichTextBox cellTxt)
                            {
                                list.Add(cellTxt);
                            }
                        }
                    }
                }
            }
            return list;
        }

        private string GetXaml(RichTextBox rtb)
        {
            var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
            using (var ms = new System.IO.MemoryStream())
            {
                range.Save(ms, DataFormats.Xaml);
                return System.Text.Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private void SetXaml(RichTextBox rtb, string xaml, string fallbackText, DesignerBlock b, string? forcedAlignment = null)
        {
            if (!string.IsNullOrEmpty(xaml))
            {
                using (var ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xaml)))
                {
                    var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                    range.Load(ms, DataFormats.Xaml);
                }
                rtb.Document.TextAlignment = (TextAlignment)Enum.Parse(typeof(TextAlignment), b.TextAlignment);
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
                    Foreground = (Brush)new BrushConverter().ConvertFromString(b.ColorHex ?? "#000000")!
                };
                var p = new Paragraph(run) { Margin = new Thickness(0) };
                p.TextAlignment = (TextAlignment)Enum.Parse(typeof(TextAlignment), forcedAlignment ?? b.TextAlignment);
                rtb.Document.Blocks.Add(p);
            }
        }

        private bool ApplyRichTextProperty(DependencyProperty property, object? value)
        {
            if (Keyboard.FocusedElement is RichTextBox rtb)
            {
                rtb.Selection.ApplyPropertyValue(property, value);
                var args = new TextChangedEventArgs(RichTextBox.TextChangedEvent, UndoAction.None);
                rtb.RaiseEvent(args);
                return true;
            }
            return false;
        }

        private void Alignment_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBlock == null || _selectedElement == null || !(sender is ToggleButton btn)) return;
            string align = btn.Tag.ToString() ?? "Left";
            _selectedBlock.TextAlignment = align;
            
            _isUpdatingUI = true;
            tglAlignLeft.IsChecked = align == "Left";
            tglAlignCenter.IsChecked = align == "Center";
            tglAlignRight.IsChecked = align == "Right";
            _isUpdatingUI = false;

            if (_selectedBlock.Id == "table") {
                foreach(var tb in GetTableRichTextBoxes((Border)_selectedElement)) {
                    var tr = new TextRange(tb.Document.ContentStart, tb.Document.ContentEnd);
                    tr.ApplyPropertyValue(Block.TextAlignmentProperty, (TextAlignment)Enum.Parse(typeof(TextAlignment), align));
                }
            } else {
                if (ApplyRichTextProperty(Block.TextAlignmentProperty, (TextAlignment)Enum.Parse(typeof(TextAlignment), align))) return;

                var txt = GetRichTextBox((Border)_selectedElement);
                if (txt != null)
                {
                    var tr = new TextRange(txt.Document.ContentStart, txt.Document.ContentEnd);
                    tr.ApplyPropertyValue(Block.TextAlignmentProperty, (TextAlignment)Enum.Parse(typeof(TextAlignment), align));
                }
            }
            PushState();
        }

        private void CellAlignment_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTableCellData == null || _selectedBlock == null || !(sender is ToggleButton btn)) return;
            string align = btn.Tag.ToString() ?? "Left";
            
            _isUpdatingUI = true;
            foreach (var border in _selectedTableCells) {
                if (border.Tag is TableCellData data) {
                    data.TextAlignment = align;
                    if (border.Child is RichTextBox rtb) {
                        rtb.Document.TextAlignment = (TextAlignment)Enum.Parse(typeof(TextAlignment), align);
                    }
                }
            }

            tglCellAlignLeft.IsChecked = align == "Left";
            tglCellAlignCenter.IsChecked = align == "Center";
            tglCellAlignRight.IsChecked = align == "Right";
            _isUpdatingUI = false;

            var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock.TableCellsJson)!;
            foreach(var selBorder in _selectedTableCells) {
                var selData = (TableCellData)selBorder.Tag;
                var match = cells.FirstOrDefault(c => c.Row == selData.Row && c.Col == selData.Col);
                if (match != null) match.TextAlignment = align;
            }
            _selectedBlock.TableCellsJson = JsonSerializer.Serialize(cells);
            PushState();
        }


        private void ShapeBorderColor_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            if (sender is Button btn)
            {
                string hex = btn.Tag.ToString()!;
                var color = hex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(hex)!;
                foreach(var border in _selectedElements) {
                    if (border.Tag is DesignerBlock block && (block.Id == "rect" || block.Id == "circle" || block.Id == "image" || block.Id == "triangle" || block.Id == "polygon")) {
                        block.BorderColorHex = hex;
                        if (block.Id == "triangle" || block.Id == "polygon") {
                            if (border.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is System.Windows.Shapes.Polygon poly) {
                                poly.Stroke = color;
                            }
                        } else {
                            border.BorderBrush = color;
                        }
                    }
                }
            }
        }

        private void ShapeBorderWeight_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUI) return;
            foreach(var border in _selectedElements) {
                if (border.Tag is DesignerBlock block && (block.Id == "rect" || block.Id == "circle" || block.Id == "image" || block.Id == "triangle" || block.Id == "polygon")) {
                    block.ShapeBorderThickness = e.NewValue;
                    if (block.Id == "triangle" || block.Id == "polygon") {
                        if (border.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is System.Windows.Shapes.Polygon poly) {
                            poly.StrokeThickness = e.NewValue;
                        }
                    } else {
                        border.BorderThickness = new Thickness(e.NewValue);
                    }
                }
            }
        }

        private void ShapeBorderRadius_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUI) return;
            foreach(var border in _selectedElements) {
                if (border.Tag is DesignerBlock block && (block.Id == "rect" || block.Id == "circle" || block.Id == "image")) {
                    block.BorderRadius = e.NewValue;
                    border.CornerRadius = block.Id == "circle" ? new CornerRadius(block.Width/2) : new CornerRadius(e.NewValue);
                }
            }
        }

        private void PolygonSides_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblPolygonSidesValue != null) lblPolygonSidesValue.Text = ((int)e.NewValue).ToString();
            if (_isUpdatingUI) return;
            foreach(var border in _selectedElements) {
                if (border.Tag is DesignerBlock block && (block.Id == "triangle" || block.Id == "polygon")) {
                    block.PolygonSides = (int)e.NewValue;
                    if (border.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is System.Windows.Shapes.Polygon poly) {
                        var points = new PointCollection();
                        int sides = block.PolygonSides < 3 ? 3 : block.PolygonSides;
                        for (int i = 0; i < sides; i++)
                        {
                            double angle = 2 * Math.PI * i / sides - Math.PI / 2;
                            points.Add(new Point(50 + 50 * Math.Cos(angle), 50 + 50 * Math.Sin(angle)));
                        }
                        poly.Points = points;
                    }
                }
            }
        }

        private void TableBackgroundColor_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            if (sender is Button btn)
            {
                string hex = btn.Tag.ToString()!;
                var color = hex == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(hex)!;
                foreach(var border in _selectedElements) {
                    if (border.Tag is DesignerBlock block) {
                        bool isTextOrTable = block.Id == "table" || (block.Id != "line" && block.Id != "rect" && block.Id != "circle" && block.Id != "triangle" && block.Id != "polygon" && block.Id != "image" && block.Id != "logo" && block.Id != "custom_image");
                        if (isTextOrTable) {
                            block.TableBackgroundColorHex = hex;
                            border.Background = color;
                        }
                    }
                }
            }
        }

        private void TableBorderColor_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            if (sender is Button btn)
            {
                string color = btn.Tag.ToString()!;
                foreach(var border in _selectedElements.ToList()) {
                    if (border.Tag is DesignerBlock block && block.Id == "table") {
                        var cells = JsonSerializer.Deserialize<List<TableCellData>>(block.TableCellsJson) ?? new List<TableCellData>();
                        foreach(var cell in cells) {
                            cell.BorderColor = color;
                        }
                        block.TableCellsJson = JsonSerializer.Serialize(cells);
                        RefreshSelectedTable();
                    }
                }
            }
        }

        private void TableBorderThicknessMinus_Click(object sender, RoutedEventArgs e) { UpdateTableBorderThickness(-1); }
        private void TableBorderThicknessPlus_Click(object sender, RoutedEventArgs e) { UpdateTableBorderThickness(1); }

        private void UpdateTableBorderThickness(double delta) {
            if (_isUpdatingUI) return;
            foreach(var border in _selectedElements.ToList()) {
                if (border.Tag is DesignerBlock block && block.Id == "table") {
                    var cells = JsonSerializer.Deserialize<List<TableCellData>>(block.TableCellsJson) ?? new List<TableCellData>();
                    double currentThickness = cells.Count > 0 ? cells[0].BorderL : 1;
                    double newThickness = Math.Max(0, currentThickness + delta);
                    txtTableBorderAll.Text = newThickness.ToString();
                    foreach(var cell in cells) {
                        cell.BorderL = newThickness;
                        cell.BorderT = newThickness;
                        cell.BorderR = newThickness;
                        cell.BorderB = newThickness;
                    }
                    block.TableCellsJson = JsonSerializer.Serialize(cells);
                    RefreshSelectedTable();
                }
            }
        }

        private void PreviewPlaceholders_Changed(object sender, RoutedEventArgs e)
        {
            if (DesignerCanvas == null) return;
            bool preview = tglPreviewPlaceholders.IsChecked == true;
            _isUpdatingUI = true;
            foreach (UIElement child in DesignerCanvas.Children)
            {
                if (child is Border border && border.Tag is DesignerBlock b)
                {
                    if (b.Id != "line" && b.Id != "rect" && b.Id != "circle" && b.Id != "image" && b.Id != "table" && b.Id != "custom_text")
                    {
                        var txt = GetRichTextBox(border);
                        if (txt != null)
                        {
                            string xamlToLoad = b.FormattedTextXaml;
                            if (preview && !string.IsNullOrEmpty(xamlToLoad)) {
                                xamlToLoad = xamlToLoad.Replace(GetPlaceholderText(b.Id), GetPreviewText(b.Id));
                            }
                            SetXaml(txt, xamlToLoad, preview ? GetPreviewText(b.Id) : GetPlaceholderText(b.Id), b);
                        }
                    }
                }
            }
            _isUpdatingUI = false;
        }

        private string GetPlaceholderText(string id)
        {
            return id switch
            {
                "name"                 => "🏢 Company Name",
                "company_name"         => "🏢 Company Name",
                "address"              => "📍 Business Address",
                "company_address"      => "📍 Business Address",
                "phone"                => "📞 Contact Info",
                "company_phone"        => "📞 Contact Info",
                "memo_id"              => "🔖 Order ID",
                "date"                 => "📅 Date",
                "customer_name"        => "👤 Customer Name",
                "customer_phone"       => "📱 Customer Phone",
                "customer_address"     => "🏠 Customer Address",
                "brand"                => "📦 Device Brand",
                "model"                => "📱 Model Name",
                "device_name"          => "💻 Device Name",
                "product_name"         => "💻 Device Name",
                "serial_number"        => "🔢 Serial Number",
                "accessories"          => "🎒 Accessories",
                "issue"                => "⚠️ Issue Description",
                "description"          => "⚠️ Issue Description",
                "issue_description"    => "⚠️ Issue Description",
                "diagnostics"          => "🔬 Diagnostics",
                "cost"                 => "💰 Estimated Cost",
                "itemized_costs"       => "📋 Itemized Costs Table",
                "technician_name"      => "🔧 Technician Name",
                "terms"                => "📋 Terms & Conditions",
                "customer"             => "👤 Customer Info",
                "device"               => "💻 Device Details",
                "signatures"           => "✍️ Signature Block",
                "customer_signature"   => "✍️ Customer Signature",
                "technician_signature" => "✍️ Technician Signature",
                "company_signature"    => "✍️ Company Signature",
                _ => $"[ {id.Replace("_", " ").ToUpper()} ]"
            };
        }

        private string GetPreviewText(string id)
        {
            var s = SettingsManager.Default;
            return id switch
            {
                "name"                 => !string.IsNullOrEmpty(s.CompanyName) ? s.CompanyName : "[Company Name]",
                "company_name"         => !string.IsNullOrEmpty(s.CompanyName) ? s.CompanyName : "[Company Name]",
                "address"              => !string.IsNullOrEmpty(s.CompanyAddress) ? s.CompanyAddress : "[Address]",
                "company_address"      => !string.IsNullOrEmpty(s.CompanyAddress) ? s.CompanyAddress : "[Address]",
                "phone"                => (!string.IsNullOrEmpty(s.CompanyPhone) ? s.CompanyPhone : "[Phone]") + (!string.IsNullOrEmpty(s.CompanyPhone2) ? " | " + s.CompanyPhone2 : ""),
                "memo_id"              => "MEMO-12345",
                "date"                 => DateTime.Now.ToString("dd MMM yyyy"),
                "customer_name"        => "John Doe",
                "customer_phone"       => "+1 234 567 890",
                "customer_address"     => "123 Main Street, City, Country",
                "brand"                => "Apple",
                "model"                => "MacBook Pro M2",
                "device_name"          => "Laptop",
                "product_name"         => "Laptop",
                "serial_number"        => "C02XG123JL4M",
                "accessories"          => "Power Adapter, Case",
                "issue"                => "Screen flickering intermittently when at high brightness.",
                "diagnostics"          => "Internal cable reseated. LVDS connector replaced.",
                "cost"                 => "Total: $150.00",
                "itemized_costs"       => "- Screen replacement: Rs. 120.00\n- Labor charge: Rs. 30.00\n----------------------------------------\nTotal: Rs. 150.00",
                "technician_name"      => "Alex Rivera",
                "terms"                => !string.IsNullOrEmpty(s.TermsAndConditions) ? s.TermsAndConditions : "[Terms and Conditions]",
                "customer_signature"   => "________________________\nCustomer Signature",
                "technician_signature" => "________________________\nTechnician Signature",
                "company_signature"    => "________________________\nCompany Signature",
                "customer"             => "John Doe\njohn@example.com\n+1 234 567 890",
                "device"               => "MacBook Pro M3 - Serial: C02XG123JL4M",
                "signatures"           => "________________________\nCustomer Signature\n\n________________________\nTechnician Signature",
                _ => $"[{id.Replace("_", " ")} preview]"
            };
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            ImportCurrent_Click(sender, e);
        }

        private void ImportCurrent_Click(object sender, RoutedEventArgs e)
        {
            if (DesignerCanvas.Children.Count > 0)
            {
                if (MessageBox.Show("This will clear your current design and reset it to the starting layout. Continue?", "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            string currentId = SettingsManager.Default.SelectedTemplateId;
            if (currentId == "Custom" || currentId.StartsWith("UserDesign:"))
            {
                string? json = null;
                if (currentId == "Custom") json = SettingsManager.Default.CustomTemplateJson;
                else {
                    string name = currentId.Replace("UserDesign:", "");
                    json = SettingsManager.Default.UserTemplates?.FirstOrDefault(t => t.Name == name)?.JsonData;
                }

                if (!string.IsNullOrEmpty(json)) LoadTemplateFromJson(json);
                else MessageBox.Show("No saved layout data found.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var blocks = GetStandardBlocks(currentId);
                DesignerCanvas.Children.Clear();
                foreach (var b in blocks)
                {
                    AddBlockToCanvas(b);
                }
            }
            PushState();
        }



        private List<DesignerBlock> GetStandardBlocks(string? templateId)
        {
            var list = new List<DesignerBlock>();
            if (string.IsNullOrEmpty(templateId)) return list;
            bool isHalf = templateId.StartsWith("Half");
            double pH = isHalf ? 561 : 1123;
            double m = 50;
            double cW = 794 - 2 * m; // 694

            if (isHalf)
            {
                double y = 30;
                list.Add(new DesignerBlock { Id = "name", X = m, Y = y, Width = cW, Height = 40, FontSize = 20, IsBold = true, TextAlignment = "Center", ColorHex = "#1A73E8" });
                y += 45;
                list.Add(new DesignerBlock { Id = "address", X = m, Y = y, Width = cW, Height = 28, FontSize = 10, TextAlignment = "Center", ColorHex = "#666666" });
                y += 30;
                list.Add(new DesignerBlock { Id = "line", X = m, Y = y, Width = cW, Height = 1.5, ColorHex = "#EEEEEE" });
                y += 12;
                list.Add(new DesignerBlock { Id = "memo_id", X = m, Y = y, Width = cW / 2, Height = 30, FontSize = 16, IsBold = true, ColorHex = "#1A73E8" });
                list.Add(new DesignerBlock { Id = "date", X = m + cW / 2, Y = y, Width = cW / 2, Height = 30, FontSize = 11, TextAlignment = "Right", ColorHex = "#666666" });
                y += 38;
                list.Add(new DesignerBlock { Id = "customer_name", X = m, Y = y, Width = cW, Height = 35, FontSize = 14, IsBold = true, ColorHex = "#000000" });
                y += 40;
                list.Add(new DesignerBlock { Id = "model", X = m, Y = y, Width = cW, Height = 30, FontSize = 13, ColorHex = "#1A73E8" });
                y += 38;
                list.Add(new DesignerBlock { Id = "issue", X = m, Y = y, Width = cW, Height = 80, FontSize = 11, ColorHex = "#333333" });
                y += 88;
                list.Add(new DesignerBlock { Id = "cost", X = m, Y = y, Width = cW, Height = 35, FontSize = 18, IsBold = true, TextAlignment = "Right", ColorHex = "#1A73E8" });
                list.Add(new DesignerBlock { Id = "signatures", X = m, Y = pH - 70, Width = cW, Height = 60, FontSize = 10, TextAlignment = "Center", ColorHex = "#666666" });
            }
            else
            {
                double y = 50;
                list.Add(new DesignerBlock { Id = "name", X = m, Y = y, Width = cW, Height = 50, FontSize = 26, IsBold = true, TextAlignment = "Center", ColorHex = "#1A73E8" });
                y += 55;
                list.Add(new DesignerBlock { Id = "address", X = m, Y = y, Width = cW, Height = 30, FontSize = 12, TextAlignment = "Center", ColorHex = "#666666" });
                y += 35;
                list.Add(new DesignerBlock { Id = "phone", X = m, Y = y, Width = cW, Height = 30, FontSize = 12, TextAlignment = "Center", ColorHex = "#666666" });
                y += 40;
                list.Add(new DesignerBlock { Id = "line", X = m, Y = y, Width = cW, Height = 2, ColorHex = "#DDDDDD" });
                y += 18;
                list.Add(new DesignerBlock { Id = "memo_id", X = m, Y = y, Width = 300, Height = 35, FontSize = 18, IsBold = true, ColorHex = "#1A73E8" });
                list.Add(new DesignerBlock { Id = "date", X = 794 - m - 300, Y = y, Width = 300, Height = 35, FontSize = 12, TextAlignment = "Right", ColorHex = "#666666" });
                y += 50;
                list.Add(new DesignerBlock { Id = "customer_name", X = m, Y = y, Width = cW, Height = 40, FontSize = 15, IsBold = true, ColorHex = "#000000" });
                y += 50;
                list.Add(new DesignerBlock { Id = "model", X = m, Y = y, Width = cW, Height = 40, FontSize = 16, IsBold = true, ColorHex = "#1A73E8" });
                y += 50;
                list.Add(new DesignerBlock { Id = "issue", X = m, Y = y, Width = cW, Height = 150, FontSize = 12, ColorHex = "#333333" });
                y += 165;
                list.Add(new DesignerBlock { Id = "diagnostics", X = m, Y = y, Width = cW, Height = 120, FontSize = 12, ColorHex = "#D93025" });
                y += 135;
                list.Add(new DesignerBlock { Id = "cost", X = m, Y = y, Width = cW, Height = 50, FontSize = 22, IsBold = true, TextAlignment = "Right", ColorHex = "#1A73E8" });
                list.Add(new DesignerBlock { Id = "signatures", X = m, Y = pH - 120, Width = cW, Height = 100, FontSize = 11, TextAlignment = "Center", ColorHex = "#666666" });
            }

            foreach (var b in list) b.IsHalfA4 = isHalf;
            return list;
        }


        private void AddElement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string id = btn.Tag.ToString() ?? "";
                var block = new DesignerBlock { Id = id, X = 100, Y = 100 };
                if (id == "rect" || id == "circle" || id == "triangle" || id == "polygon") { block.Width = 100; block.Height = 100; block.ColorHex = "#CCCCCC"; }
                if (id == "triangle") block.PolygonSides = 3;
                if (id == "line") { block.Width = 600; block.Height = 2; block.ColorHex = "#CCCCCC"; }
                if (id == "table") { block.Width = 400; block.Height = 120; block.ColorHex = "#EEEEEE"; }
                if (id == "customer_signature" || id == "technician_signature" || id == "company_signature" || id == "signatures") { block.Width = 300; block.Height = 120; }
                
                // Give 'heavy' content more initial weight/width
                if (id == "diagnostics" || id == "terms" || id == "issue" || id == "issues" || id == "problem_reported" || id == "customer_address") 
                {
                    block.Width = 450; 
                }

                AddBlockToCanvas(block);
                PushState();
            }
        }

        private void AddTable_Click(object sender, RoutedEventArgs e)
        {
            var block = new DesignerBlock
            {
                Id = "table",
                X = 100, Y = 100,
                Width = 400, Height = 120,
                ColorHex = "#EEEEEE"
            };
            AddBlockToCanvas(block);
            PushState();
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedElements.Count > 0)
            {
                foreach(var el in _selectedElements) {
                    DesignerCanvas.Children.Remove(el);
                }
                PropertiesPanel.Visibility = Visibility.Hidden;
                _selectedElements.Clear();
                _selectedElement = null;
                PushState();
            }
        }

        private void Block_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _draggingElement = sender as UIElement;
            _lastDragPos = e.GetPosition(DesignerCanvas);
            if (_draggingElement != null) _draggingElement.CaptureMouse();
            e.Handled = true;
        }

        private void Block_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(DesignerCanvas);
                double dx = pos.X - _lastDragPos.X;
                double dy = pos.Y - _lastDragPos.Y;
                
                bool multi = _selectedElements.Count > 1 && _draggingElement is Border db && _selectedElements.Contains(db);
                var elementsToMove = multi ? _selectedElements : new List<Border> { (Border)_draggingElement };

                foreach(var el in elementsToMove) {
                    double nx = Canvas.GetLeft(el) + dx;
                    double ny = Canvas.GetTop(el) + dy;
                    nx = Math.Max(0, Math.Min(nx, DesignerCanvas.ActualWidth - el.ActualWidth));
                    ny = Math.Max(0, Math.Min(ny, (radioHalfA4.IsChecked == true ? 561 : 1123) - el.ActualHeight));
                    Canvas.SetLeft(el, nx);
                    Canvas.SetTop(el, ny);
                    if (el.Tag is DesignerBlock b) { b.X = nx; b.Y = ny; }
                }
                _lastDragPos = pos;
            }
        }

        private void Block_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggingElement != null)
            {
                _draggingElement.ReleaseMouseCapture();
                _draggingElement = null;
                PushState(); // Save state after drag
            }
            _isDraggingCells = false;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (sender != null) _isClosingAfterSave = false;

            if (DesignerCanvas.Children.Count == 0)
            {
                MessageBox.Show("Please add at least one element to the canvas before saving.", "Empty Canvas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_isEditingExisting && !string.IsNullOrEmpty(_currentTemplateName))
            {
                // Quick save existing
                PerformSave(_currentTemplateName, true);
                if (_isClosingAfterSave) this.Close();
            }
            else
            {
                SaveAs_Click(sender ?? new Button(), e ?? new RoutedEventArgs());
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (DesignerCanvas.Children.Count == 0)
            {
                MessageBox.Show("Please add at least one element to the canvas before exporting.", "Empty Canvas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var blocks = GetCurrentBlocks();
                string json = System.Text.Json.JsonSerializer.Serialize(blocks);

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "MemoBud Layout Design (*.mbld)|*.mbld",
                    DefaultExt = ".mbld",
                    Title = "Export Layout Design"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(saveFileDialog.FileName, json);
                    ShowToast("Layout exported successfully!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting layout: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (DesignerCanvas.Children.Count == 0)
            {
                MessageBox.Show("Please add at least one element to the canvas before saving.", "Empty Canvas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            txtTemplateName.Text = _currentTemplateName ?? "";
            UnsavedChangesOverlay.Visibility = Visibility.Collapsed;
            TemplateNamingOverlay.Visibility = Visibility.Visible;
            txtTemplateName.Focus();
        }

        private void CancelNaming_Click(object sender, RoutedEventArgs e)
        {
            TemplateNamingOverlay.Visibility = Visibility.Collapsed;
        }

        private void txtTemplateName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmSave_Click(new Button(), new RoutedEventArgs());
            }
        }

        private void ConfirmSave_Click(object sender, RoutedEventArgs e)
        {
            string templateName = txtTemplateName.Text.Trim();
            if (string.IsNullOrEmpty(templateName))
            {
                MessageBox.Show("Please enter a name for your template.", "Naming Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            PerformSave(templateName, false);
        }

        private void PerformSave(string templateName, bool isQuickSave)
        {
            try
            {
                var blocks = GetCurrentBlocks();
                string json = System.Text.Json.JsonSerializer.Serialize(blocks);
                
                if (SettingsManager.Default.UserTemplates == null)
                    SettingsManager.Default.UserTemplates = new List<UserTemplate>();

                var existing = SettingsManager.Default.UserTemplates.FirstOrDefault(t => t.Name == templateName);
                if (existing != null)
                {
                    if (isQuickSave)
                    {
                        existing.JsonData = json;
                    }
                    else if (MessageBox.Show($"A template named '{templateName}' already exists. Do you want to overwrite it?", "Overwrite Template", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        existing.JsonData = json;
                    }
                    else
                    {
                        return; // Cancel save
                    }
                }
                else
                {
                    SettingsManager.Default.UserTemplates.Add(new UserTemplate 
                    { 
                        Name = templateName, 
                        JsonData = json 
                    });
                }

                // Also set as the current custom template for immediate use
                SettingsManager.Default.CustomTemplateJson = json;
                SettingsManager.Default.SelectedTemplateId = "UserDesign:" + templateName;
                SettingsManager.Save();
                
                if (this.Owner is MainWindow main) main.RefreshCustomTemplates();

                _currentTemplateName = templateName;
                _isEditingExisting = true;
                _isDirty = false;

                TemplateNamingOverlay.Visibility = Visibility.Collapsed;
                UnsavedChangesOverlay.Visibility = Visibility.Collapsed;
                
                if (!isQuickSave)
                    MessageBox.Show($"Template '{templateName}' saved successfully to your layouts!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show($"Changes to '{templateName}' saved successfully!", "Quick Save", MessageBoxButton.OK, MessageBoxImage.Information);
                
                if (_isClosingAfterSave) {
                    this.DialogResult = true;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving template: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
        private void Clear_Click(object sender, RoutedEventArgs e) { DesignerCanvas.Children.Clear(); PropertiesPanel.Visibility = Visibility.Hidden; PushState(); }





        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isClosingAfterSave || this.DialogResult != null) return;
            
            if (_isDirty)
            {
                UnsavedChangesOverlay.Visibility = Visibility.Visible;
                e.Cancel = true;
            }
        }

        private void ExitWithoutSave_Click(object sender, RoutedEventArgs e)
        {
            _isClosingAfterSave = true; // Use this to bypass the prompt
            this.DialogResult = false;
            this.Close();
        }

        private void CancelExit_Click(object sender, RoutedEventArgs e)
        {
            UnsavedChangesOverlay.Visibility = Visibility.Collapsed;
        }

        private void SaveAndExit_Click(object sender, RoutedEventArgs e)
        {
            _isClosingAfterSave = true;
             Save_Click(new Button(), new RoutedEventArgs());
        }
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (ZoomSlider != null) ZoomSlider.Value = Math.Min(3.0, ZoomSlider.Value + 0.1);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (ZoomSlider != null) ZoomSlider.Value = Math.Max(0.1, ZoomSlider.Value - 0.1);
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CanvasScale != null)
            {
                CanvasScale.ScaleX = e.NewValue;
                CanvasScale.ScaleY = e.NewValue;
            }
        }

        private void CanvasContainer_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (ZoomSlider != null)
            {
                double zoom = ZoomSlider.Value * e.DeltaManipulation.Scale.X;
                ZoomSlider.Value = Math.Max(0.1, Math.Min(3.0, zoom));
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                Point mousePos = e.GetPosition(DesignerScroller);
                double oldZoom = ZoomSlider.Value;
                
                if (e.Delta > 0) ZoomSlider.Value = Math.Min(3.0, ZoomSlider.Value + 0.1);
                else ZoomSlider.Value = Math.Max(0.1, ZoomSlider.Value - 0.1);

                double newZoom = ZoomSlider.Value;
                double ratio = newZoom / oldZoom;

                // Adjust scroll to keep mouse point fixed
                double offsetX = (mousePos.X + DesignerScroller.HorizontalOffset) * ratio - mousePos.X;
                double offsetY = (mousePos.Y + DesignerScroller.VerticalOffset) * ratio - mousePos.Y;

                DesignerScroller.ScrollToHorizontalOffset(offsetX);
                DesignerScroller.ScrollToVerticalOffset(offsetY);
                
                e.Handled = true;
            }
        }

        private Point _lastPanMousePos;
        private bool _isPanning = false;

        private void Scroller_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers == ModifierKeys.Alt))
            {
                _isPanning = true;
                _lastPanMousePos = e.GetPosition(this);
                DesignerScroller.Cursor = Cursors.Hand;
                DesignerScroller.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Scroller_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point currentPos = e.GetPosition(this);
                double dx = currentPos.X - _lastPanMousePos.X;
                double dy = currentPos.Y - _lastPanMousePos.Y;

                DesignerScroller.ScrollToHorizontalOffset(DesignerScroller.HorizontalOffset - dx);
                DesignerScroller.ScrollToVerticalOffset(DesignerScroller.VerticalOffset - dy);

                _lastPanMousePos = currentPos;
            }
        }

        private void Scroller_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                DesignerScroller.ReleaseMouseCapture();
                DesignerScroller.Cursor = Cursors.Arrow;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Maximize_Click(sender, e);
            }
            else
            {
                this.DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            UpdateWindowLayout();
        }

        private void UpdateWindowLayout()
        {
            if (MainBorder == null) return;

            if (this.WindowState == WindowState.Maximized)
            {
                MainBorder.Margin = new Thickness(0);
                MainBorder.CornerRadius = new CornerRadius(0);
                if (MaximizeIcon != null) MaximizeIcon.Data = Geometry.Parse("M0,2 H8 V10 H0 Z M2,0 H10 V8 H8 V2 H2 Z");
            }
            else
            {
                MainBorder.Margin = new Thickness(20);
                MainBorder.CornerRadius = new CornerRadius(16);
                if (MaximizeIcon != null) MaximizeIcon.Data = Geometry.Parse("M0,0 H10 V10 H0 Z M2,2 V8 H8 V2 Z");
            }
        }

        private void DesignerCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == DesignerCanvas)
            {
                DeselectAll();

                _selectionStartPoint = e.GetPosition(DesignerCanvas);
                _isDrawingSelectionBox = true;
                DesignerCanvas.CaptureMouse();

                if (_selectionBox == null)
                {
                    _selectionBox = new System.Windows.Shapes.Rectangle
                    {
                        Stroke = new SolidColorBrush(Color.FromRgb(26, 115, 232)), // #1A73E8
                        StrokeThickness = 1.5,
                        StrokeDashArray = new DoubleCollection(new double[] { 4, 3 }),
                        Fill = new SolidColorBrush(Color.FromArgb(40, 26, 115, 232)), // 15% opacity selection fill
                        Visibility = Visibility.Collapsed
                    };
                    DesignerCanvas.Children.Add(_selectionBox);
                }

                Canvas.SetLeft(_selectionBox, _selectionStartPoint.X);
                Canvas.SetTop(_selectionBox, _selectionStartPoint.Y);
                _selectionBox.Width = 0;
                _selectionBox.Height = 0;
                _selectionBox.Visibility = Visibility.Visible;

                e.Handled = true;
            }
        }

        private void DesignerCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawingSelectionBox && _selectionBox != null)
            {
                var currentPoint = e.GetPosition(DesignerCanvas);

                double x = Math.Min(_selectionStartPoint.X, currentPoint.X);
                double y = Math.Min(_selectionStartPoint.Y, currentPoint.Y);
                double width = Math.Abs(_selectionStartPoint.X - currentPoint.X);
                double height = Math.Abs(_selectionStartPoint.Y - currentPoint.Y);

                // Constrain selection bounds within the canvas boundary
                x = Math.Max(0, Math.Min(x, DesignerCanvas.ActualWidth));
                y = Math.Max(0, Math.Min(y, DesignerCanvas.ActualHeight));
                width = Math.Min(width, DesignerCanvas.ActualWidth - x);
                height = Math.Min(height, DesignerCanvas.ActualHeight - y);

                Canvas.SetLeft(_selectionBox, x);
                Canvas.SetTop(_selectionBox, y);
                _selectionBox.Width = width;
                _selectionBox.Height = height;
            }
        }

        private void DesignerCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawingSelectionBox)
            {
                _isDrawingSelectionBox = false;
                DesignerCanvas.ReleaseMouseCapture();

                if (_selectionBox != null)
                {
                    _selectionBox.Visibility = Visibility.Collapsed;

                    double boxLeft = Canvas.GetLeft(_selectionBox);
                    double boxTop = Canvas.GetTop(_selectionBox);
                    double boxWidth = _selectionBox.Width;
                    double boxHeight = _selectionBox.Height;

                    var selectionRect = new Rect(boxLeft, boxTop, boxWidth, boxHeight);

                    // Find and select all borders representing memos elements that touch the selection rectangle
                    var elementsToSelect = new List<Border>();
                    foreach (UIElement child in DesignerCanvas.Children)
                    {
                        if (child is Border b && b.Tag is DesignerBlock block)
                        {
                            double left = Canvas.GetLeft(b);
                            double top = Canvas.GetTop(b);
                            if (double.IsNaN(left)) left = 0;
                            if (double.IsNaN(top)) top = 0;

                            var elementRect = new Rect(left, top, b.ActualWidth, b.ActualHeight);

                            if (selectionRect.IntersectsWith(elementRect))
                            {
                                elementsToSelect.Add(b);
                            }
                        }
                    }

                    if (elementsToSelect.Count > 0)
                    {
                        DeselectAll(hideProperties: false);
                        foreach (var el in elementsToSelect)
                        {
                            SelectElement(el, isMultiSelect: true);
                        }
                    }
                }
            }
        }

        private void DeselectAll(bool hideProperties = true)
        {
            foreach (var hList in _elementHandles.Values)
            {
                foreach (var h in hList) h.Visibility = Visibility.Collapsed;
            }
            
            // Disable all text editing/selection on deselect
            foreach (UIElement child in DesignerCanvas.Children)
            {
                if (child is Border b)
                {
                    if (b.Child is RichTextBox rtb) rtb.IsHitTestVisible = false;
                    else if (b.Child is Grid g)
                    {
                        foreach (var gc in g.Children)
                        {
                            if (gc is Border cb && cb.Child is RichTextBox crtb) crtb.IsHitTestVisible = false;
                        }
                    }
                }
            }

            foreach(var el in _selectedElements) {
                el.Effect = null;
                el.Margin = new Thickness(0);
                if (el.Tag is DesignerBlock block)
                {
                    if (block.Id == "table") {
                        el.BorderBrush = Brushes.Transparent;
                        el.BorderThickness = new Thickness(0);
                    } else if (block.Id == "line") {
                        el.BorderBrush = (block.ColorHex == null || block.ColorHex == "Transparent") ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(block.ColorHex)!;
                        el.BorderThickness = new Thickness(0, 0, 0, block.Height);
                    } else if (block.Id == "rect" || block.Id == "circle" || block.Id == "triangle" || block.Id == "polygon") {
                        el.BorderBrush = (block.BorderColorHex == null || block.BorderColorHex == "Transparent") ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(block.BorderColorHex)!;
                        el.BorderThickness = new Thickness(block.ShapeBorderThickness);
                    } else {
                        el.BorderBrush = Brushes.Transparent;
                        el.BorderThickness = new Thickness(0);
                    }
                }
            }
            if (hideProperties) {
                _selectedElements.Clear();
                _selectedElement = null;
                _selectedBlock = null;
                PropertiesPanel.Visibility = Visibility.Hidden;
                if (_selectedTableCell != null) {
                    _selectedTableCell.Opacity = 1.0;
                }
                _selectedTableCell = null;
                _selectedTableCellData = null;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { Save_Click(new Button(), new RoutedEventArgs()); return; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { Undo_Click(new Button(), new RoutedEventArgs()); return; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) { Redo_Click(new Button(), new RoutedEventArgs()); return; }
            
            // Clipboard
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C) { CopySelected(); return; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V) { Paste(); return; }

            // If editing text, don't move elements with arrows or delete unless Ctrl is held
            if (e.Key == Key.Escape) { this.Focus(); e.Handled = true; return; }
            if ((Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is RichTextBox) && Keyboard.Modifiers == ModifierKeys.None) return;
            
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                if (_selectedElements.Count > 0)
                {
                    foreach(var el in _selectedElements) { DesignerCanvas.Children.Remove(el); }
                    PropertiesPanel.Visibility = Visibility.Hidden;
                    _selectedElements.Clear();
                    _selectedElement = null;
                    PushState();
                }
            }
            else if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right)
            {
                // Build list of elements to move: multi-selection or single selection
                var toMove = new List<UIElement>();
                if (_selectedElements.Count > 0)
                    toMove.AddRange(_selectedElements);
                else if (_selectedElement != null)
                    toMove.Add(_selectedElement);

                if (toMove.Count > 0)
                {
                    double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
                    double dx = e.Key == Key.Left ? -step : (e.Key == Key.Right ? step : 0);
                    double dy = e.Key == Key.Up ? -step : (e.Key == Key.Down ? step : 0);
                    
                    foreach(var el in toMove) {
                        double nx = Canvas.GetLeft(el) + dx;
                        double ny = Canvas.GetTop(el) + dy;
                        Canvas.SetLeft(el, nx);
                        Canvas.SetTop(el, ny);
                        if (el is Border brd && brd.Tag is DesignerBlock b) { b.X = nx; b.Y = ny; }
                    }
                    _isDirty = true;
                    e.Handled = true;
                }
                else
                {
                    // Navigation: select first element if none selected
                    if (DesignerCanvas.Children.Count > 0)
                    {
                        SelectElement(DesignerCanvas.Children[0]);
                        e.Handled = true;
                    }
                }
            }
            else if (e.Key == Key.Tab)
            {
                // Tab navigation
                if (DesignerCanvas.Children.Count > 0)
                {
                    int index = 0;
                    if (_selectedElement != null)
                    {
                        index = DesignerCanvas.Children.IndexOf(_selectedElement);
                        index = (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) ? index - 1 : index + 1;
                        if (index >= DesignerCanvas.Children.Count) index = 0;
                        if (index < 0) index = DesignerCanvas.Children.Count - 1;
                    }
                    SelectElement(DesignerCanvas.Children[index]);
                    e.Handled = true;
                }
            }
        }

        private int GetMaxZIndex()
        {
            int max = 0;
            foreach (UIElement child in DesignerCanvas.Children) {
                int z = Panel.GetZIndex(child);
                if (z > max) max = z;
            }
            return max;
        }

        private int GetMinZIndex()
        {
            int min = 0;
            foreach (UIElement child in DesignerCanvas.Children) {
                int z = Panel.GetZIndex(child);
                if (z < min) min = z;
            }
            return min;
        }

        private void SaveTableCells(DesignerBlock b, List<TableCellData> cells)
        {
            b.TableCellsJson = JsonSerializer.Serialize(cells);
        }

        private void SaveTableLayout(DesignerBlock b, Grid g)
        {
            b.TableColumnWidths = string.Join(",", g.ColumnDefinitions.Select(cd => cd.Width.ToString()));
            b.TableRowHeights = string.Join(",", g.RowDefinitions.Select(rd => rd.Height.ToString()));
        }

        private void SelectTableCell(Border cellBorder, TableCellData data, DesignerBlock tableBlock, bool add = false)
        {
            if (!add) {
                foreach(var c in _selectedTableCells) c.Opacity = 1.0;
                _selectedTableCells.Clear();
            }
            
            if (!_selectedTableCells.Contains(cellBorder)) {
                _selectedTableCells.Add(cellBorder);
                cellBorder.BorderBrush = Brushes.BlueViolet; // Use highlight border instead of opacity
                cellBorder.BorderThickness = new Thickness(Math.Max(data.BorderL, 2), Math.Max(data.BorderT, 2), Math.Max(data.BorderR, 2), Math.Max(data.BorderB, 2));
            }
            
            _selectedTableCell = cellBorder;
            _selectedTableCellData = data;
            
            _isUpdatingUI = true;
            txtCellText.Text = data.Text;
            txtBorderL.Text = data.BorderL.ToString();
            txtBorderT.Text = data.BorderT.ToString();
            txtBorderR.Text = data.BorderR.ToString();
            txtBorderB.Text = data.BorderB.ToString();
            
            lblSelectedCellName.Text = _selectedTableCells.Count > 1 ? $"Selected: {_selectedTableCells.Count} cells" : $"Selected: Row {data.Row + 1}, Col {data.Col + 1}";
            
            btnMergeCells.Visibility = _selectedTableCells.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            btnUnmergeCells.Visibility = (_selectedTableCells.Count == 1 && (data.RowSpan > 1 || data.ColSpan > 1)) ? Visibility.Visible : Visibility.Collapsed;

            // Sync Color UI
            UpdateColorUI("CellBg", data.BackgroundColor ?? "Transparent");
            UpdateColorUI("CellBorder", data.BorderColor ?? "#000000");
            
            tglCellAlignLeft.IsChecked = data.TextAlignment == "Left";
            tglCellAlignCenter.IsChecked = data.TextAlignment == "Center";
            tglCellAlignRight.IsChecked = data.TextAlignment == "Right";
            
            _isUpdatingUI = false;
        }

        private void CellText_Changed(object sender, TextChangedEventArgs e) {
            if (_isUpdatingUI) return;
            if (_selectedTableCellData != null && _selectedTableCell != null) {
                if (_selectedTableCell.Child is TextBox txt) {
                    txt.Text = txtCellText.Text;
                    var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock!.TableCellsJson)!;
                    var match = cells.FirstOrDefault(c => c.Row == _selectedTableCellData.Row && c.Col == _selectedTableCellData.Col);
                    if (match != null) { match.Text = txt.Text; SaveTableCells(_selectedBlock, cells); }
                }
            }
        }
        
        private void CellBackgroundColor_Click(object sender, RoutedEventArgs e) {
            if (_isUpdatingUI) return;
            if (_selectedTableCells.Count > 0 && sender is Button btn) {
                string color = btn.Tag.ToString()!;
                if (_selectedBlock != null) {
                    var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock.TableCellsJson)!;
                    foreach(var cell in _selectedTableCells) {
                        if (cell.Tag is TableCellData data) {
                            data.BackgroundColor = color;
                            cell.Background = color == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(color)!;
                            var match = cells.FirstOrDefault(c => c.Row == data.Row && c.Col == data.Col);
                            if (match != null) match.BackgroundColor = color;
                        }
                    }
                    SaveTableCells(_selectedBlock, cells);
                }
            }
        }

        private void CellBorderColor_Click(object sender, RoutedEventArgs e) {
            if (_isUpdatingUI) return;
            if (_selectedTableCells.Count > 0 && sender is Button btn) {
                string color = btn.Tag.ToString()!;
                if (_selectedBlock != null) {
                    var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock.TableCellsJson)!;
                    foreach(var cell in _selectedTableCells) {
                        if (cell.Tag is TableCellData data) {
                            data.BorderColor = color;
                            cell.BorderBrush = color == "Transparent" ? Brushes.Transparent : (Brush)new BrushConverter().ConvertFromString(color)!;
                            var match = cells.FirstOrDefault(c => c.Row == data.Row && c.Col == data.Col);
                            if (match != null) match.BorderColor = color;
                        }
                    }
                    SaveTableCells(_selectedBlock, cells);
                }
            }
        }

        private void CellBorder_Changed(object sender, TextChangedEventArgs e) {
            if (_isUpdatingUI) return;
            if (_selectedTableCellData != null && _selectedTableCell != null && _selectedBlock != null) {
                if (double.TryParse(txtBorderL.Text, out double l)) _selectedTableCellData.BorderL = l;
                if (double.TryParse(txtBorderT.Text, out double t)) _selectedTableCellData.BorderT = t;
                if (double.TryParse(txtBorderR.Text, out double r)) _selectedTableCellData.BorderR = r;
                if (double.TryParse(txtBorderB.Text, out double b)) _selectedTableCellData.BorderB = b;
                
                _selectedTableCell.BorderThickness = new Thickness(_selectedTableCellData.BorderL, _selectedTableCellData.BorderT, _selectedTableCellData.BorderR, _selectedTableCellData.BorderB);
                
                var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock.TableCellsJson)!;
                var match = cells.FirstOrDefault(c => c.Row == _selectedTableCellData.Row && c.Col == _selectedTableCellData.Col);
                if (match != null) {
                    match.BorderL = _selectedTableCellData.BorderL;
                    match.BorderT = _selectedTableCellData.BorderT;
                    match.BorderR = _selectedTableCellData.BorderR;
                    match.BorderB = _selectedTableCellData.BorderB;
                    SaveTableCells(_selectedBlock, cells);
                }
            }
        }

        private void CellBorderStyle_Changed(object sender, SelectionChangedEventArgs e) {
            if (_isUpdatingUI || _selectedTableCells.Count == 0 || _selectedBlock == null) return;
            if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item) {
                string style = item.Tag.ToString()!;
                var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock.TableCellsJson)!;
                foreach(var cell in _selectedTableCells) {
                    if (cell.Tag is TableCellData data) {
                        data.BorderStyle = style;
                        var match = cells.FirstOrDefault(c => c.Row == data.Row && c.Col == data.Col);
                        if (match != null) match.BorderStyle = style;
                    }
                }
                SaveTableCells(_selectedBlock, cells);
            }
        }

        private void TableBorderStyle_Changed(object sender, SelectionChangedEventArgs e) {
            if (_isUpdatingUI) return;
            if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item) {
                string style = item.Tag.ToString()!;
                foreach(var border in _selectedElements.ToList()) {
                    if (border.Tag is DesignerBlock block && block.Id == "table") {
                        var cells = JsonSerializer.Deserialize<List<TableCellData>>(block.TableCellsJson) ?? new List<TableCellData>();
                        foreach(var cell in cells) {
                            cell.BorderStyle = style;
                        }
                        block.TableCellsJson = JsonSerializer.Serialize(cells);
                        RefreshSelectedTable();
                    }
                }
            }
        }

        private void BorderThicknessPlus_Click(object sender, RoutedEventArgs e) { UpdateBorderThickness(sender, 1); }
        private void BorderThicknessMinus_Click(object sender, RoutedEventArgs e) { UpdateBorderThickness(sender, -1); }
        
        private void UpdateBorderThickness(object sender, double delta) {
            if (_isUpdatingUI || _selectedTableCells.Count == 0 || _selectedBlock == null) return;
            if (sender is Button btn && btn.Tag != null) {
                string edge = btn.Tag.ToString()!;
                var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock.TableCellsJson)!;
                
                foreach(var cell in _selectedTableCells) {
                    var data = cell.Tag as TableCellData;
                    if (data != null) {
                        if (edge == "L") data.BorderL = Math.Max(0, data.BorderL + delta);
                        if (edge == "T") data.BorderT = Math.Max(0, data.BorderT + delta);
                        if (edge == "R") data.BorderR = Math.Max(0, data.BorderR + delta);
                        if (edge == "B") data.BorderB = Math.Max(0, data.BorderB + delta);
                        
                        cell.BorderThickness = new Thickness(data.BorderL, data.BorderT, data.BorderR, data.BorderB);
                        
                        var match = cells.FirstOrDefault(c => c.Row == data.Row && c.Col == data.Col);
                        if (match != null) {
                            match.BorderL = data.BorderL; match.BorderT = data.BorderT; match.BorderR = data.BorderR; match.BorderB = data.BorderB;
                        }
                    }
                }
                SaveTableCells(_selectedBlock, cells);
                
                _isUpdatingUI = true;
                if (_selectedTableCellData != null) {
                    txtBorderL.Text = _selectedTableCellData.BorderL.ToString();
                    txtBorderT.Text = _selectedTableCellData.BorderT.ToString();
                    txtBorderR.Text = _selectedTableCellData.BorderR.ToString();
                    txtBorderB.Text = _selectedTableCellData.BorderB.ToString();
                }
                _isUpdatingUI = false;
            }
        }

        private void MergeCells_Click(object sender, RoutedEventArgs e) {
            if (_selectedTableCells.Count > 1 && _selectedBlock != null) {
                var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock.TableCellsJson)!;
                var selectedData = _selectedTableCells.Select(cb => cb.Tag as TableCellData).Where(d => d != null).ToList();
                
                int minRow = selectedData.Min(d => d!.Row);
                int maxRow = selectedData.Max(d => d!.Row + d.RowSpan - 1);
                int minCol = selectedData.Min(d => d!.Col);
                int maxCol = selectedData.Max(d => d!.Col + d.ColSpan - 1);
                
                int expectedArea = (maxRow - minRow + 1) * (maxCol - minCol + 1);
                int actualArea = selectedData.Sum(d => d!.RowSpan * d!.ColSpan);
                
                if (expectedArea == actualArea) {
                    var topLeft = selectedData.First(d => d!.Row == minRow && d.Col == minCol);
                    var match = cells.First(c => c.Row == minRow && c.Col == minCol);
                    
                    match.RowSpan = maxRow - minRow + 1;
                    match.ColSpan = maxCol - minCol + 1;
                    
                    foreach(var d in selectedData) {
                        if (d != topLeft) {
                            var toRemove = cells.FirstOrDefault(c => c.Row == d!.Row && c.Col == d.Col);
                            if (toRemove != null) cells.Remove(toRemove);
                        }
                    }
                    
                    SaveTableCells(_selectedBlock, cells);
                    RefreshSelectedTable();
                }
            }
        }

        private void UnmergeCells_Click(object sender, RoutedEventArgs e) {
            if (_selectedTableCellData != null && _selectedBlock != null) {
                if (_selectedTableCellData.RowSpan > 1 || _selectedTableCellData.ColSpan > 1) {
                    var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock.TableCellsJson)!;
                    var match = cells.FirstOrDefault(c => c.Row == _selectedTableCellData.Row && c.Col == _selectedTableCellData.Col);
                    if (match != null) {
                        int rSpan = match.RowSpan;
                        int cSpan = match.ColSpan;
                        match.RowSpan = 1;
                        match.ColSpan = 1;
                        
                        for (int r = match.Row; r < match.Row + rSpan; r++) {
                            for (int c = match.Col; c < match.Col + cSpan; c++) {
                                if (r == match.Row && c == match.Col) continue;
                                cells.Add(new TableCellData { Row = r, Col = c, Text = "" });
                            }
                        }
                        SaveTableCells(_selectedBlock, cells);
                        RefreshSelectedTable();
                    }
                }
            }
        }

        private void AddTableRow_Click(object sender, RoutedEventArgs e) {
            if (_selectedBlock != null) {
                _selectedBlock.TableRows++;
                var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock.TableCellsJson)!;
                for (int c = 0; c < _selectedBlock.TableCols; c++) {
                    cells.Add(new TableCellData { Row = _selectedBlock.TableRows - 1, Col = c, Text = "" });
                }
                SaveTableCells(_selectedBlock, cells);
                _selectedBlock.TableRowHeights += string.IsNullOrEmpty(_selectedBlock.TableRowHeights) ? "1*" : ",1*";
                RefreshSelectedTable();
            }
        }
        
        private void AddTableCol_Click(object sender, RoutedEventArgs e) {
            if (_selectedBlock != null) {
                _selectedBlock.TableCols++;
                var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock.TableCellsJson)!;
                for (int r = 0; r < _selectedBlock.TableRows; r++) {
                    cells.Add(new TableCellData { Row = r, Col = _selectedBlock.TableCols - 1, Text = "" });
                }
                SaveTableCells(_selectedBlock, cells);
                _selectedBlock.TableColumnWidths += string.IsNullOrEmpty(_selectedBlock.TableColumnWidths) ? "1*" : ",1*";
                RefreshSelectedTable();
            }
        }

        private void RemoveTableRow_Click(object sender, RoutedEventArgs e) {
            if (_selectedBlock != null && _selectedBlock.TableRows > 1) {
                var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock.TableCellsJson)!;
                if (cells.Any(c => c.Row + c.RowSpan > _selectedBlock.TableRows - 1 && c.Row != _selectedBlock.TableRows - 1)) return;
                
                cells.RemoveAll(c => c.Row == _selectedBlock.TableRows - 1);
                _selectedBlock.TableRows--;
                SaveTableCells(_selectedBlock, cells);
                
                var heights = _selectedBlock.TableRowHeights.Split(',').ToList();
                if (heights.Count > 0) heights.RemoveAt(heights.Count - 1);
                _selectedBlock.TableRowHeights = string.Join(",", heights);
                
                RefreshSelectedTable();
            }
        }

        private void RemoveTableCol_Click(object sender, RoutedEventArgs e) {
            if (_selectedBlock != null && _selectedBlock.TableCols > 1) {
                var cells = JsonSerializer.Deserialize<List<TableCellData>>(_selectedBlock.TableCellsJson)!;
                if (cells.Any(c => c.Col + c.ColSpan > _selectedBlock.TableCols - 1 && c.Col != _selectedBlock.TableCols - 1)) return;
                
                cells.RemoveAll(c => c.Col == _selectedBlock.TableCols - 1);
                _selectedBlock.TableCols--;
                SaveTableCells(_selectedBlock, cells);
                
                var widths = _selectedBlock.TableColumnWidths.Split(',').ToList();
                if (widths.Count > 0) widths.RemoveAt(widths.Count - 1);
                _selectedBlock.TableColumnWidths = string.Join(",", widths);
                
                RefreshSelectedTable();
            }
        }

        private void RefreshSelectedTable() {
            if (_selectedElement != null && _selectedBlock != null) {
                DesignerCanvas.Children.Remove(_selectedElement);
                AddBlockToCanvas(_selectedBlock);
                if (DesignerCanvas.Children.Count > 0) {
                    var newEl = DesignerCanvas.Children[DesignerCanvas.Children.Count - 1];
                    SelectElement(newEl);
                }
                _selectedTableCell = null;
                _selectedTableCellData = null;
            }
        }

        private string _activeColorTarget = "Text";
        private double _currentHue = 200.0;
        private double _currentSaturation = 1.0;
        private bool _isUpdatingColorPicker = false;

        private void ColorHex_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            if (e.Key == System.Windows.Input.Key.Enter) {
                if (sender is TextBox tb) {
                    string hex = tb.Text.Trim();
                    if (string.IsNullOrEmpty(hex)) return;

                    if (!hex.StartsWith("#")) {
                        hex = "#" + hex;
                    }

                    if (hex.Length == 4) {
                        hex = "#" + hex[1] + hex[1] + hex[2] + hex[2] + hex[3] + hex[3];
                    }

                    if (hex.Length == 7 && System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9A-Fa-f]{6}$")) {
                        tb.Text = hex;
                        AddRecentColor(hex);
                        string type = tb.Tag?.ToString() ?? "Text";
                        try {
                            var btn = new Button { Tag = hex };
                            if (type == "Text") Color_Click(btn, e);
                            else if (type == "ShapeBorder") ShapeBorderColor_Click(btn, e);
                            else if (type == "TableBg") TableBackgroundColor_Click(btn, e);
                            else if (type == "TableBorder") TableBorderColor_Click(btn, e);
                            else if (type == "CellBg") CellBackgroundColor_Click(btn, e);
                            else if (type == "CellBorder") CellBorderColor_Click(btn, e);
                        } catch {}
                    }
                }
                e.Handled = true;
            }
        }

        private (double hue, double saturation, double lightness) RgbToHsl(Color color) {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            double s = 0;
            double l = (max + min) / 2.0;

            if (delta > 0) {
                s = l < 0.5 ? delta / (max + min) : delta / (2.0 - max - min);

                if (max == r) {
                    h = (g - b) / delta + (g < b ? 6 : 0);
                } else if (max == g) {
                    h = (b - r) / delta + 2;
                } else if (max == b) {
                    h = (r - g) / delta + 4;
                }

                h /= 6.0;
            }

            return (h * 360.0, s, l);
        }

        private void ColorHex_TextChanged(object sender, TextChangedEventArgs e) {
            if (_isUpdatingUI) return;
            if (sender is TextBox tb) {
                string hex = tb.Text;
                string type = tb.Tag?.ToString() ?? "Text";
                if (hex.Length == 7 && hex.StartsWith("#")) {
                    AddRecentColor(hex);
                    try {
                        var color = (Brush)new BrushConverter().ConvertFromString(hex)!;
                        var btn = new Button { Tag = hex };
                        if (type == "Text") Color_Click(btn, e);
                        else if (type == "ShapeBorder") ShapeBorderColor_Click(btn, e);
                        else if (type == "TableBg") TableBackgroundColor_Click(btn, e);
                        else if (type == "TableBorder") TableBorderColor_Click(btn, e);
                        else if (type == "CellBg") CellBackgroundColor_Click(btn, e);
                        else if (type == "CellBorder") CellBorderColor_Click(btn, e);
                    } catch {}
                }
            }
        }

        private void OpenColorPicker_Click(object sender, RoutedEventArgs e) {
            if (sender is Button btn) {
                _activeColorTarget = btn.Tag.ToString()!;
                string currentHex = _activeColorTarget switch {
                    "Text" => txtTextColorHex.Text,
                    "ShapeBorder" => txtShapeBorderColorHex.Text,
                    "TableBg" => txtTableBgColorHex.Text,
                    "TableBorder" => txtTableBorderColorHex.Text,
                    "CellBg" => txtCellBgColorHex.Text,
                    "CellBorder" => txtCellBorderColorHex.Text,
                    _ => "#1A73E8"
                };

                if (string.IsNullOrEmpty(currentHex) || currentHex == "Transparent") {
                    currentHex = "#1A73E8";
                }

                txtColorPickerHex.Text = currentHex;
                lblColorPickerHexDisplay.Text = currentHex;

                try {
                    var parsedColor = (Color)ColorConverter.ConvertFromString(currentHex);
                    ColorPickerPreview.Background = new SolidColorBrush(parsedColor);
                    var (hue, sat, lightness) = RgbToHsl(parsedColor);
                    
                    _isUpdatingColorPicker = true;
                    _currentHue = hue;
                    _currentSaturation = sat;
                    sliderColorShade.Value = lightness * 100.0;
                    _isUpdatingColorPicker = false;
                } catch {
                    _currentHue = 200.0;
                    _currentSaturation = 1.0;
                    sliderColorShade.Value = 50;
                }

                ColorPickerOverlay.Visibility = Visibility.Visible;
            }
        }

        private void CancelColorPicker_Click(object sender, RoutedEventArgs e) {
            ColorPickerOverlay.Visibility = Visibility.Collapsed;
        }

        private void ApplyColorPicker_Click(object sender, RoutedEventArgs e) {
            string hex = txtColorPickerHex.Text;
            if (hex.Length == 7 && hex.StartsWith("#")) {
                AddRecentColor(hex);
                var btn = new Button { Tag = hex };
                if (_activeColorTarget == "Text") {
                    txtTextColorHex.Text = hex;
                    Color_Click(btn, e);
                } else if (_activeColorTarget == "ShapeBorder") {
                    txtShapeBorderColorHex.Text = hex;
                    ShapeBorderColor_Click(btn, e);
                } else if (_activeColorTarget == "TableBg") {
                    txtTableBgColorHex.Text = hex;
                    TableBackgroundColor_Click(btn, e);
                } else if (_activeColorTarget == "TableBorder") {
                    txtTableBorderColorHex.Text = hex;
                    TableBorderColor_Click(btn, e);
                } else if (_activeColorTarget == "CellBg") {
                    txtCellBgColorHex.Text = hex;
                    CellBackgroundColor_Click(btn, e);
                } else if (_activeColorTarget == "CellBorder") {
                    txtCellBorderColorHex.Text = hex;
                    CellBorderColor_Click(btn, e);
                }
            }
            ColorPickerOverlay.Visibility = Visibility.Collapsed;
        }

        private bool _isSelectingColor = false;
        private void Spectrum_MouseDown(object sender, MouseButtonEventArgs e) {
            _isSelectingColor = true;
            UpdateColorFromSpectrum(e.GetPosition(ColorSpectrum));
            ColorSpectrum.CaptureMouse();
        }

        private void Spectrum_MouseMove(object sender, MouseEventArgs e) {
            if (_isSelectingColor) {
                UpdateColorFromSpectrum(e.GetPosition(ColorSpectrum));
            }
        }

        private void Spectrum_MouseUp(object sender, MouseButtonEventArgs e) {
            _isSelectingColor = false;
            ColorSpectrum.ReleaseMouseCapture();
        }

        private void UpdateColorFromSpectrum(Point p) {
            double x = Math.Max(0, Math.Min(p.X, ColorSpectrum.ActualWidth));
            _currentHue = (x / ColorSpectrum.ActualWidth) * 360.0;
            UpdateColorFromHueAndShade();
        }

        private void ColorShade_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (_isUpdatingColorPicker) return;
            UpdateColorFromHueAndShade();
        }

        private void UpdateColorFromHueAndShade() {
            if (sliderColorShade == null || txtColorPickerHex == null || ColorPickerPreview == null || lblColorPickerHexDisplay == null) return;
            double lightness = sliderColorShade.Value / 100.0;
            var color = HslToRgb(_currentHue, _currentSaturation, lightness);
            string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            
            _isUpdatingColorPicker = true;
            txtColorPickerHex.Text = hex;
            ColorPickerPreview.Background = new SolidColorBrush(color);
            lblColorPickerHexDisplay.Text = hex;
            _isUpdatingColorPicker = false;
        }

        private List<string> _recentColors = new List<string>();

        private void AddRecentColor(string hex) {
            if (!_recentColors.Contains(hex)) {
                _recentColors.Insert(0, hex);
                if (_recentColors.Count > 10) _recentColors.RemoveAt(10);
                UpdateRecentColorsUI();
            }
        }

        private void UpdateRecentColorsUI() {
            var panels = new[] { panelRecentTextColor, panelRecentShapeBorderColor, panelRecentTableBgColor, panelRecentCellBgColor, panelRecentCellBorderColor };
            var labels = new[] { lblRecentText, lblRecentShapeBorder, lblRecentTableBg, lblRecentCellBg, lblRecentCellBorder };
            
            for(int i=0; i<panels.Length; i++) {
                if (panels[i] == null) continue;
                panels[i].Children.Clear();
                labels[i].Visibility = _recentColors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                
                foreach(var hex in _recentColors) {
                    var btn = new Button {
                        Style = (Style)FindResource("ColorDotStyle"),
                        Background = (Brush)new BrushConverter().ConvertFromString(hex)!,
                        Tag = hex,
                        Width = panels[i].Name.Contains("Cell") ? 18 : 24,
                        Height = panels[i].Name.Contains("Cell") ? 18 : 24
                    };
                    string target = labels[i].Name.Replace("lblRecent", "");
                    if (target == "Text") btn.Click += Color_Click;
                    else if (target == "ShapeBorder") btn.Click += ShapeBorderColor_Click;
                    else if (target == "TableBg") btn.Click += TableBackgroundColor_Click;
                    else if (target == "CellBg") btn.Click += CellBackgroundColor_Click;
                    else if (target == "CellBorder") btn.Click += CellBorderColor_Click;
                    
                    panels[i].Children.Add(btn);
                }
            }
        }

        private void ColorPickerHex_Changed(object sender, TextChangedEventArgs e) {
            if (_isUpdatingColorPicker) return;
            if (txtColorPickerHex == null || ColorPickerPreview == null || lblColorPickerHexDisplay == null || sliderColorShade == null) return;
            string hex = txtColorPickerHex.Text;
            if (hex.Length == 7 && hex.StartsWith("#")) {
                try {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    ColorPickerPreview.Background = new SolidColorBrush(color);
                    lblColorPickerHexDisplay.Text = hex;
                    var (hue, sat, lightness) = RgbToHsl(color);
                    
                    _isUpdatingColorPicker = true;
                    _currentHue = hue;
                    _currentSaturation = sat;
                    sliderColorShade.Value = lightness * 100.0;
                    _isUpdatingColorPicker = false;
                } catch { }
            }
        }

        private Color HslToRgb(double h, double s, double l) {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;
            double r = 0, g = 0, b = 0;

            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }

        private void CustomColorPicker_Click(object sender, RoutedEventArgs e) {
            // Replaced by new modern picker, but keeping signature if needed by other sections
            OpenColorPicker_Click(sender, e);
        }
        private string? _clipboardJson = null;


        private void CopySelected()
        {
            if (_selectedElements.Count == 0) return;
            var blocksToCopy = _selectedElements.Select(el => (DesignerBlock)el.Tag).ToList();
            _clipboardJson = JsonSerializer.Serialize(blocksToCopy);
        }

        private void Paste()
        {
            if (string.IsNullOrEmpty(_clipboardJson)) return;
            try {
                var blocksToPaste = JsonSerializer.Deserialize<List<DesignerBlock>>(_clipboardJson);
                if (blocksToPaste != null) {
                    DeselectAll();
                    foreach (var b in blocksToPaste) {
                        var newBlock = JsonSerializer.Deserialize<DesignerBlock>(JsonSerializer.Serialize(b))!; // Deep clone
                        newBlock.X += 20; 
                        newBlock.Y += 20;
                        AddBlockToCanvas(newBlock);
                    }
                    PushState();
                }
            } catch { }
        }

        private async void ShowToast(string message)
        {
            if (ToastNotification == null || ToastMessage == null || ToastTranslate == null) return;

            ToastMessage.Text = message;
            ToastNotification.Visibility = Visibility.Visible;

            // Simple animation
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(300));
            var slideUp = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
            ToastNotification.BeginAnimation(OpacityProperty, fadeIn);
            ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideUp);

            await System.Threading.Tasks.Task.Delay(4000);

            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(500));
            var slideDown = new System.Windows.Media.Animation.DoubleAnimation(20, TimeSpan.FromMilliseconds(500));
            fadeOut.Completed += (s, e) => ToastNotification.Visibility = Visibility.Collapsed;
            ToastNotification.BeginAnimation(OpacityProperty, fadeOut);
            ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideDown);
        }
    }

    public class FlexibleStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString() ?? "";
            }
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.Number)
                        list.Add(reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture));
                    else if (reader.TokenType == JsonTokenType.String)
                        list.Add(reader.GetString() ?? "");
                }
                return string.Join(",", list);
            }
            return "";
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
