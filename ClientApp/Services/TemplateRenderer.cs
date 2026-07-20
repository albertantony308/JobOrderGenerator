using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Documents;
using System.Text.Json;

namespace ClientApp.Services
{
    public static class TemplateRenderer
    {
        public static Canvas Render(List<CustomTemplateDesignerWindow.DesignerBlock> blocks, object dataContext)
        {
            bool isHalf = (blocks != null && blocks.Count > 0) ? blocks[0].IsHalfA4 : false;
            
            // Normalize Y coordinates for Half A4 if they are all offset (e.g. designed for bottom half)
            if (isHalf && blocks != null && blocks.Count > 0)
            {
                double minY = blocks.Min(b => b.Y);
                if (minY > 100) // If everything is pushed down by more than 100 units
                {
                    foreach (var b in blocks)
                    {
                        b.Y -= minY;
                    }
                }
            }

            var canvas = new Canvas
            {
                Background = Brushes.White,
                Width = 794,
                Height = isHalf ? 561 : 1123,
                ClipToBounds = true
            };

            if (blocks == null) return canvas;

            foreach (var b in blocks)
            {
                if (!ShouldShowBlock(b, dataContext)) continue;

                var element = CreateElement(b, dataContext);
                if (element != null)
                {
                    Canvas.SetLeft(element, b.X);
                    Canvas.SetTop(element, b.Y);
                    canvas.Children.Add(element);
                }
            }

            return canvas;
        }

        private static FrameworkElement? CreateElement(CustomTemplateDesignerWindow.DesignerBlock b, object dataContext)
        {
            var border = new Border
            {
                Width = b.Width,
                Height = b.Height,
                Opacity = b.Opacity,
                Tag = b,
                Background = (b.Id != "line" && b.Id != "table" && b.Id != "rect" && b.Id != "circle" && b.Id != "triangle" && b.Id != "polygon" && b.Id != "logo" && b.Id != "custom_image" && b.Id != "image")
                    ? (string.IsNullOrEmpty(b.TableBackgroundColorHex) || b.TableBackgroundColorHex == "Transparent" ? Brushes.Transparent : GetBrush(b.TableBackgroundColorHex))
                    : Brushes.Transparent
            };

            if (b.Id == "line")
            {
                border.BorderBrush = GetBrush(b.ColorHex ?? "#000000");
                border.BorderThickness = new Thickness(0, 0, 0, b.Height);
                return border;
            }

            if (b.Id == "table")
            {
                return CreateTable(b, dataContext);
            }

            if (b.Id == "rect" || b.Id == "circle")
            {
                border.Background = GetBrush(b.ColorHex ?? "#CCCCCC");
                border.BorderBrush = GetBrush(b.BorderColorHex ?? "Transparent");
                border.BorderThickness = new Thickness(b.ShapeBorderThickness);
                border.CornerRadius = b.Id == "circle" ? new CornerRadius(b.Width / 2) : new CornerRadius(b.BorderRadius);
                return border;
            }

            if (b.Id == "triangle" || b.Id == "polygon")
            {
                var poly = new Polygon
                {
                    Stretch = Stretch.Fill,
                    Fill = GetBrush(b.ColorHex ?? "#CCCCCC"),
                    Stroke = GetBrush(b.BorderColorHex ?? "Transparent"),
                    StrokeThickness = b.ShapeBorderThickness,
                    Points = GetPolygonPoints(b.PolygonSides < 3 ? 3 : b.PolygonSides)
                };
                border.Child = poly;
                return border;
            }

            if (b.Id == "logo" || b.Id == "custom_image" || b.Id == "image")
            {
                string? path = b.ImagePath;
                if (string.IsNullOrEmpty(path) && b.Id == "logo")
                {
                    path = SettingsManager.Default.CompanyLogoPath;
                }

                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        if (path.StartsWith("data:image/"))
                        {
                            var base64Data = path.Split(',')[1];
                            var bytes = Convert.FromBase64String(base64Data);
                            bitmap.StreamSource = new System.IO.MemoryStream(bytes);
                        }
                        else
                        {
                            bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                        }
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                        bitmap.EndInit();

                        var img = new Image
                        {
                            Source = bitmap,
                            // Background/full-canvas images fill the block exactly;
                            // logo/custom_image blocks keep aspect ratio (Uniform)
                            Stretch = (b.Id == "image" && b.X == 0 && b.Y == 0) ? Stretch.Fill : Stretch.Uniform
                        };
                        border.Child = img;
                    }
                    catch { }
                }
                return border;
            }

            // High Fidelity Rich Text Support
            if (!string.IsNullOrEmpty(b.FormattedTextXaml))
            {
                var rtb = new RichTextBox
                {
                    Width = b.Width,
                    Height = b.Height,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    IsReadOnly = true,
                    IsHitTestVisible = false,
                    Document = new FlowDocument { PagePadding = new Thickness(2) }
                };
                TextOptions.SetTextFormattingMode(rtb, TextFormattingMode.Display);
                TextOptions.SetTextRenderingMode(rtb, TextRenderingMode.ClearType);
                // Force all paragraphs to have zero margin to avoid vertical alignment shifts
                var pStyle = new Style(typeof(Paragraph));
                pStyle.Setters.Add(new Setter(Paragraph.MarginProperty, new Thickness(0)));
                rtb.Document.Resources.Add(typeof(Paragraph), pStyle);
                rtb.Document.LineHeight = 1.0;

                try 
                {
                    string xaml = b.FormattedTextXaml;
                    // Attempt simple placeholder substitution in XAML if it doesn't break XML
                    // This allows dynamic data while preserving designer formatting
                    xaml = SubstitutePlaceholdersInXaml(xaml, dataContext);

                    using (var ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xaml)))
                    {
                        var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                        range.Load(ms, DataFormats.Xaml);
                    }
                    rtb.Document.TextAlignment = (TextAlignment)Enum.Parse(typeof(TextAlignment), b.TextAlignment ?? "Left");
                }
                catch 
                {
                    // Fallback to plain text if XAML fails
                    var tbFallback = new TextBlock { Text = GetPlaceholderValue(b.Id, b.CustomText, dataContext), TextWrapping = TextWrapping.Wrap };
                    border.Child = tbFallback;
                    return border;
                }

                border.Child = rtb;
                return border;
            }

            // Default: Simple Text Block
            var tb = new TextBlock
            {
                Width = b.Width,
                Height = b.Height,
                Padding = new Thickness(0),
                TextWrapping = TextWrapping.Wrap,
                FontSize = b.FontSize,
                FontFamily = new FontFamily(b.FontFamily),
                FontWeight = b.IsBold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = b.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                Foreground = GetBrush(b.ColorHex ?? "#000000"),
                Opacity = b.Opacity,
                TextAlignment = (TextAlignment)Enum.Parse(typeof(TextAlignment), b.TextAlignment ?? "Left"),
                Text = GetPlaceholderValue(b.Id, b.CustomText, dataContext)
            };
            
            if (b.IsUnderlined) tb.TextDecorations = TextDecorations.Underline;
            
            border.Child = tb;
            return border;
        }

        private static string SubstitutePlaceholdersInXaml(string xaml, object dataContext)
        {
            if (dataContext == null) return xaml;
            
            var placeholders = new[] { 
                "name", "company_name", "address", "company_address", "phone", "company_phone",
                "memo_id", "date", "customer_name", "customer_phone", "customer_address", 
                "brand", "model", "product_name", "device_name", "device", "serial_number", "accessories", "issue", "description", 
                "issue_description", "terms", "diagnostics", "technician_name", "cost", "itemized_costs", "costs_table"
            };
            
            foreach (var p in placeholders)
            {
                string tag = "{" + p + "}";
                if (xaml.Contains(tag))
                {
                    xaml = xaml.Replace(tag, GetPlaceholderValue(p, tag, dataContext));
                }
                
                string doubleTag = "{?" + p + "}"; // double braces replacement
                string realDoubleTag = "{{" + p + "}}";
                if (xaml.Contains(realDoubleTag))
                {
                    xaml = xaml.Replace(realDoubleTag, GetPlaceholderValue(p, realDoubleTag, dataContext));
                }
            }
            return xaml;
        }

        private static FrameworkElement CreateTable(CustomTemplateDesignerWindow.DesignerBlock b, object dataContext)
        {
            var grid = new Grid
            {
                Width = b.Width,
                Height = b.Height,
                Background = GetBrush(b.TableBackgroundColorHex ?? "Transparent")
            };

            var colWidths = string.IsNullOrEmpty(b.TableColumnWidths) ? Enumerable.Repeat("1*", b.TableCols).ToList() : b.TableColumnWidths.Split(',').ToList();
            var rowHeights = string.IsNullOrEmpty(b.TableRowHeights) ? Enumerable.Repeat("1*", b.TableRows).ToList() : b.TableRowHeights.Split(',').ToList();

            for (int i = 0; i < b.TableCols; i++)
            {
                double w = 1; GridUnitType t = GridUnitType.Star;
                if (i < colWidths.Count && colWidths[i].EndsWith("*")) double.TryParse(colWidths[i].TrimEnd('*'), out w);
                else if (i < colWidths.Count) { double.TryParse(colWidths[i], out w); t = GridUnitType.Pixel; }
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, t) });
            }
            for (int i = 0; i < b.TableRows; i++)
            {
                double h = 1; GridUnitType t = GridUnitType.Star;
                if (i < rowHeights.Count && rowHeights[i].EndsWith("*")) double.TryParse(rowHeights[i].TrimEnd('*'), out h);
                else if (i < rowHeights.Count) { double.TryParse(rowHeights[i], out h); t = GridUnitType.Pixel; }
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(h, t) });
            }

            if (!string.IsNullOrEmpty(b.TableCellsJson))
            {
                try
                {
                    var cells = JsonSerializer.Deserialize<List<CustomTemplateDesignerWindow.TableCellData>>(b.TableCellsJson);
                    if (cells != null)
                    {
                        foreach (var cell in cells)
                        {
                            var cellBorder = new Border
                            {
                                Background = GetBrush(cell.BackgroundColor ?? "Transparent"),
                                BorderBrush = GetBrush(cell.BorderColor ?? "#CCCCCC"),
                                BorderThickness = new Thickness(cell.BorderL, cell.BorderT, cell.BorderR, cell.BorderB),
                                Padding = new Thickness(0)
                            };

                            // Table cells also support Rich Text
                            if (!string.IsNullOrEmpty(cell.FormattedTextXaml))
                            {
                                var rtb = new RichTextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), IsReadOnly = true, IsHitTestVisible = false, Document = new FlowDocument { PagePadding = new Thickness(2) } };
                                string xaml = SubstitutePlaceholdersInXaml(cell.FormattedTextXaml, dataContext);
                                using (var ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xaml)))
                                {
                                    var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                                    range.Load(ms, DataFormats.Xaml);
                                }
                                rtb.Document.TextAlignment = (TextAlignment)Enum.Parse(typeof(TextAlignment), cell.TextAlignment ?? "Left");
                                cellBorder.Child = rtb;
                            }
                            else 
                            {
                                var cellTb = new TextBlock
                                {
                                    Text = GetPlaceholderValue("custom_text", cell.Text, dataContext),
                                    TextAlignment = (TextAlignment)Enum.Parse(typeof(TextAlignment), cell.TextAlignment ?? "Left"),
                                    VerticalAlignment = VerticalAlignment.Center,
                                    TextWrapping = TextWrapping.Wrap,
                                    FontSize = b.FontSize,
                                    Foreground = GetBrush(b.ColorHex ?? "#000000")
                                };
                                cellBorder.Child = cellTb;
                            }
                            
                            Grid.SetRow(cellBorder, cell.Row);
                            Grid.SetColumn(cellBorder, cell.Col);
                            Grid.SetRowSpan(cellBorder, cell.RowSpan);
                            Grid.SetColumnSpan(cellBorder, cell.ColSpan);
                            grid.Children.Add(cellBorder);
                        }
                    }
                }
                catch { }
            }

            return grid;
        }

        private static string GetPlaceholderValue(string id, string customText, object dataContext)
        {
            if (id == "custom_text")
            {
                // If custom_text contains a raw {placeholder} string, try to resolve it as a real ID.
                // This handles templates where placeholders were accidentally stored as custom_text.
                if (!string.IsNullOrWhiteSpace(customText))
                {
                    string trimmed = customText.Trim();
                    if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                    {
                        string extractedId = trimmed.Replace("{", "").Replace("}", "").Trim().ToLowerInvariant();
                        var resolved = GetPlaceholderValue(extractedId, customText, dataContext);
                        // Only use resolved value if it differs (i.e., was actually found)
                        if (resolved != customText && resolved != "{" + extractedId + "}")
                            return resolved;
                    }
                }
                return customText;
            }

            // Try to get property from dataContext (PrintViewModel)
            if (dataContext == null) return "{" + id + "}";

            if (id == "customer")
            {
                var nameVal = GetPlaceholderValue("customer_name", "", dataContext);
                var phoneVal = GetPlaceholderValue("customer_phone", "", dataContext);
                return $"{nameVal}\n{phoneVal}".Trim();
            }
            if (id == "device")
            {
                var nameVal = GetPlaceholderValue("device_name", "", dataContext);
                var brandVal = GetPlaceholderValue("brand", "", dataContext);
                var modelVal = GetPlaceholderValue("model", "", dataContext);
                var serialVal = GetPlaceholderValue("serial_number", "", dataContext);
                var detail = "";
                if (!string.IsNullOrEmpty(nameVal) && nameVal != "{device_name}" && nameVal != "N/A") detail += nameVal + " ";
                if (!string.IsNullOrEmpty(brandVal) && brandVal != "{brand}" && brandVal != "N/A") detail += brandVal + " ";
                if (!string.IsNullOrEmpty(modelVal) && modelVal != "{model}" && modelVal != "N/A") detail += modelVal;
                if (!string.IsNullOrEmpty(serialVal) && serialVal != "{serial_number}" && serialVal != "N/A") detail += $" (S/N: {serialVal})";
                return string.IsNullOrWhiteSpace(detail) ? "N/A" : detail.Trim();
            }
            if (id == "itemized_costs" || id == "costs_table")
            {
                var itemizedJson = GetPlaceholderValue("itemized_costs_json", "", dataContext);
                if (string.IsNullOrEmpty(itemizedJson) || itemizedJson.StartsWith("{"))
                {
                    var totalCostVal = GetPlaceholderValue("cost", "", dataContext);
                    return $"Repair / Service Charge: {totalCostVal}";
                }
                
                try
                {
                    var items = JsonSerializer.Deserialize<List<ClientApp.Models.CostItem>>(itemizedJson);
                    if (items == null || items.Count == 0)
                    {
                        var totalCostVal = GetPlaceholderValue("cost", "", dataContext);
                        return $"Repair / Service Charge: {totalCostVal}";
                    }

                    var lines = new List<string>();
                    foreach (var item in items)
                    {
                        lines.Add($"- {item.Description}: Rs. {item.Cost:N2}");
                    }
                    decimal total = items.Sum(i => i.Cost);
                    lines.Add("----------------------------------------");
                    lines.Add($"Total: Rs. {total:N2}");
                    return string.Join("\n", lines);
                }
                catch
                {
                    var totalCostVal = GetPlaceholderValue("cost", "", dataContext);
                    return $"Repair / Service Charge: {totalCostVal}";
                }
            }

            if (id == "signatures")
            {
                return "__________________________\nCustomer Signature\n\n__________________________\nTechnician Signature";
            }

            var propName = GetPropertyNameForId(id);
            if (propName == null) return customText;

            var prop = dataContext.GetType().GetProperty(propName);
            if (prop != null)
            {
                return prop.GetValue(dataContext)?.ToString() ?? "";
            }

            return "{" + id + "}";
        }

        private static string? GetPropertyNameForId(string id)
        {
            return id switch
            {
                "name" => "CompanyName",
                "company_name" => "CompanyName",
                "address" => "CompanyAddress",
                "company_address" => "CompanyAddress",
                "phone" => "CompanyContact",
                "company_phone" => "CompanyContact",
                "memo_id" => "MemoNumber",
                "order_id" => "MemoNumber",
                "order_number" => "MemoNumber",
                "id" => "MemoNumber",
                "date" => "Date",
                "order_date" => "Date",
                "memo_date" => "Date",
                "customer_name" => "CustomerName",
                "customer_phone" => "CustomerPhone",
                "customer_address" => "CustomerAddress",
                "brand" => "Brand",
                "model" => "DeviceModel",
                "product_name" => "DeviceName",
                "device_name" => "DeviceName",
                "device" => "DeviceName",
                "serial_number" => "SerialNumber",
                "accessories" => "Accessories",
                "issue" => "IssueDescription",
                "description" => "IssueDescription",
                "issue_description" => "IssueDescription",
                "terms" => "TermsAndConditions",
                "diagnostics" => "Diagnostics",
                "technician_name" => "TechnicianName",
                "cost" => "EstimatedCost",
                "itemized_costs_json" => "ItemizedCosts",
                _ => null
            };
        }

        private static Brush GetBrush(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex == "Transparent") return Brushes.Transparent;
            try { return (Brush)new BrushConverter().ConvertFromString(hex)!; }
            catch { return Brushes.Black; }
        }

        private static PointCollection GetPolygonPoints(int sides)
        {
            var points = new PointCollection();
            for (int i = 0; i < sides; i++)
            {
                double angle = 2 * Math.PI * i / sides - Math.PI / 2;
                points.Add(new Point(50 + 50 * Math.Cos(angle), 50 + 50 * Math.Sin(angle)));
            }
            return points;
        }
        private static bool ShouldShowBlock(CustomTemplateDesignerWindow.DesignerBlock b, object dataContext)
        {
            if (string.IsNullOrEmpty(b.VisibilityCondition)) return true;

            if (b.VisibilityCondition == "DiagnosticsNotEmpty")
            {
                var val = GetPlaceholderValue("diagnostics", "", dataContext);
                return !string.IsNullOrWhiteSpace(val) && val != "{diagnostics}";
            }

            return true;
        }
    }
}
