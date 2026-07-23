using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using ClientApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using ClientApp.Services;
using ClientApp.Models;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media.Imaging;

namespace ClientApp
{
    public partial class MainWindow : Window
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        private string _visualSelectedId = string.Empty;
        private bool _isStacked = false;
        private System.Windows.Threading.DispatcherTimer _clickTimer;
        private string _pendingClickId = string.Empty;
        private bool _hasShownLanWarning = false;
        private CancellationTokenSource? _startupSyncCts;
        private System.Threading.CancellationTokenSource? _cloudPollCts;

        public MainWindow()
        {
            _clickTimer = new System.Windows.Threading.DispatcherTimer();
            uint doubleClickTime = GetDoubleClickTime();
            if (doubleClickTime == 0) doubleClickTime = 500;
            _clickTimer.Interval = TimeSpan.FromMilliseconds(doubleClickTime + 200); // 200ms buffer to reliably prevent single-click selection during a double-click
            _clickTimer.Tick += ClickTimer_Tick;

            ThemeManager.Initialize();
            InitializeComponent();
            WindowDwmFixer.ApplyFix(this);
            this.Closing += MainWindow_Closing;
            
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (txtAppVersionDisplay != null && version != null)
            {
                txtAppVersionDisplay.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
            }

            bool isDark = SettingsManager.Default.IsDarkMode;
            chkDarkMode.IsChecked = isDark;
            LightPresetsGrid.Visibility = isDark ? Visibility.Collapsed : Visibility.Visible;
            DarkPresetsGrid.Visibility = isDark ? Visibility.Visible : Visibility.Collapsed;
            chkSettingsSyncImages.IsChecked = SettingsManager.Default.SyncImagesEnabled;
            
            // Initialize Connection/Sync Mode Selection
            string syncMode = SettingsManager.Default.SyncMode ?? "Hybrid";
            foreach (ComboBoxItem item in cmbSyncMode.Items)
            {
                if (item.Tag?.ToString() == syncMode)
                {
                    item.IsSelected = true;
                    break;
                }
            }
            
            sldFontSize.Value = SettingsManager.Default.AppFontSize;
            txtFontSizeValue.Text = $"{(int)sldFontSize.Value}px";
            ApplyFontSize(sldFontSize.Value);
            
            LoadBranding();
            LoadData();
            RefreshCustomTemplates();
            PopulateSystemTemplates();
            UpdatePreview(SettingsManager.Default.SelectedTemplateId);
            UpdateCloudSyncSidebarUI();

            // Hook Cloud status change events for real-time fallback notification
            CloudSyncService.CloudStatusChanged += () =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    UpdateCloudSyncSidebarUI();
                    if (CloudSyncService.IsCloudOffline)
                    {
                        SyncStatusText.Text = "Cloud Slow/Down (LAN Active)";
                        SyncStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
                    }
                    else if (SettingsManager.Default.IsCloudSyncEnabled)
                    {
                        SyncStatusText.Text = "Connected";
                        SyncStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
                    }
                    RefreshCloudOfflineToast();
                });
            };
            
            // Register and Start LAN sync service
            LanSyncService.PeersChanged += LanSyncService_PeersChanged;
            LanSyncService.SyncStateChanged += LanSyncService_SyncStateChanged;
            CloudSyncService.CloudOrderCompleted += CloudSyncService_CloudOrderCompleted;
            UpdateManager.Instance.LiveUpdateDetected += UpdateManager_LiveUpdateDetected;
            LanSyncService.Start();
            InitializeNetworkMonitoring();
            LanSyncService_PeersChanged();
            
            this.Loaded += async (s, e) =>
            {
                // Prevent window from spawning off-screen on small laptops
                if (this.Height > SystemParameters.WorkArea.Height)
                    this.Height = SystemParameters.WorkArea.Height - 40;
                if (this.Width > SystemParameters.WorkArea.Width)
                    this.Width = SystemParameters.WorkArea.Width - 40;

                // Toggle width slightly to force DWM to redraw the WindowChrome/Titlebar on Windows 10
                Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await Task.Delay(150);
                    var originalWidth = this.Width;
                    this.Width = originalWidth - 1;
                    await Task.Delay(20);
                    this.Width = originalWidth;
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                MigrateOldMemoNumbers();

                bool lanEnabled = SettingsManager.Default.SyncMode != "InternetOnly";

                if (lanEnabled)
                {
                    // ── Show startup sync overlay ──────────────────────────────────
                    panelStartupSync.Visibility = Visibility.Visible;

                    _startupSyncCts = new CancellationTokenSource();

                    // Forward live progress text to the UI
                    LanSyncService.StartupSyncProgressChanged += OnStartupSyncProgress;

                    try
                    {
                        await LanSyncService.PerformStartupSyncAsync(
                            discoveryWindowMs: 7000,
                            token: _startupSyncCts.Token);
                    }
                    finally
                    {
                        LanSyncService.StartupSyncProgressChanged -= OnStartupSyncProgress;
                        _startupSyncCts.Dispose();
                        _startupSyncCts = null;
                        DismissStartupOverlay();
                    }
                }

                // ── Cloud sync (runs after overlay dismissed) ──────────────────
                if (SettingsManager.Default.IsCloudSyncEnabled && SettingsManager.Default.SyncMode != "LocalOnly")
                {
                    await CloudSyncService.SyncWithCloudAsync();
                    LoadData();
                }

                // Start silent periodic background cloud polling
                StartCloudPolling();

                // Refresh license warning banner
                if (LicenseManager.CurrentStatus == null)
                {
                    LicenseManager.CurrentStatus = await new LicenseManager().VerifyLicenseStatusAsync();
                }
                RefreshLicenseWarningToast(LicenseManager.CurrentStatus);
            };
        }

        private void OnStartupSyncProgress(string message)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (txtStartupSyncProgress != null)
                    txtStartupSyncProgress.Text = message;
            });
        }

        private void DismissStartupOverlay()
        {
            this.Dispatcher.Invoke(() =>
            {
                panelStartupSync.Visibility = Visibility.Collapsed;
                LoadData(); // Refresh grid so synced records appear
            });
        }

        private void SkipStartupSync_Click(object sender, RoutedEventArgs e)
        {
            LanSyncService.MarkStartupSyncComplete();
            _startupSyncCts?.Cancel();
        }

        private void MigrateOldMemoNumbers()
        {
            string prefix = LicenseManager.GetDeviceOrderPrefix();
            using (var db = new LocalDbContext())
            {
                var oldMemos = db.ServiceMemos.ToList();
                bool changed = false;

                string imagesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp", "Images");

                foreach (var memo in oldMemos)
                {
                    string oldNum = memo.MemoNumber;
                    // Match old single-letter prefix (A-Z followed by digits) or "IN" prefix (IN followed by digits)
                    if (System.Text.RegularExpressions.Regex.IsMatch(oldNum, @"^[A-Z]\d+$") || oldNum.StartsWith("IN"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(oldNum, @"\d+");
                        if (match.Success)
                        {
                            string newNum = $"{prefix}{match.Value}";
                            
                            // Update images
                            if (!string.IsNullOrEmpty(memo.ImagePath))
                            {
                                var newPaths = new List<string>();
                                var paths = memo.ImagePath.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var oldPath in paths)
                                {
                                    if (File.Exists(oldPath))
                                    {
                                        try
                                        {
                                            string ext = Path.GetExtension(oldPath);
                                            string fileName = Path.GetFileNameWithoutExtension(oldPath);
                                            var indexMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"_(\d+)$");
                                            string newFileName = indexMatch.Success ? $"{newNum}_{indexMatch.Groups[1].Value}{ext}" : $"{newNum}{ext}";
                                            string newPath = Path.Combine(imagesDir, newFileName);
                                            
                                            if (File.Exists(newPath)) File.Delete(newPath);
                                            File.Move(oldPath, newPath);
                                            newPaths.Add(newPath);
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Failed to rename image: {ex.Message}");
                                            newPaths.Add(oldPath);
                                        }
                                    }
                                    else
                                    {
                                        newPaths.Add(oldPath);
                                    }
                                }
                                memo.ImagePath = string.Join("|", newPaths);
                            }

                            memo.MemoNumber = newNum;
                            memo.UpdatedAt = DateTime.Now;
                            db.ServiceMemos.Update(memo);
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    db.SaveChanges();
                    System.Diagnostics.Debug.WriteLine($"[MIGRATION] Successfully migrated old memo numbers to new prefix {prefix}.");
                }
            }
        }


        private void FontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtFontSizeValue == null) return;
            
            double newSize = e.NewValue;
            txtFontSizeValue.Text = $"{(int)newSize}px";
            ApplyFontSize(newSize);
            
            if (SettingsManager.Default != null)
            {
                SettingsManager.Default.AppFontSize = newSize;
                SettingsManager.Save();
            }
        }

        private void ApplyFontSize(double baseSize)
        {
            Application.Current.Resources["AppFontSize"] = baseSize;
            Application.Current.Resources["BodyMdFontSize"] = baseSize;
            Application.Current.Resources["BodySmFontSize"] = baseSize * 0.9;
            Application.Current.Resources["HeadlineLgFontSize"] = baseSize * 2.2;
            Application.Current.Resources["HeadlineMdFontSize"] = baseSize * 1.7;
            Application.Current.Resources["HeadlineXsFontSize"] = baseSize * 1.4;
            Application.Current.Resources["CaptionFontSize"] = baseSize * 0.85;
            Application.Current.Resources["LabelXsFontSize"] = baseSize * 0.7;
        }

        private void LoadBranding()
        {
            txtCompanyName.Text = SettingsManager.Default.CompanyName;
            txtCompanyPhone.Text = SettingsManager.Default.CompanyPhone;
            txtCompanyPhone2.Text = SettingsManager.Default.CompanyPhone2;
            txtCompanyAddress.Text = SettingsManager.Default.CompanyAddress;
            txtTermsAndConditions.Text = SettingsManager.Default.TermsAndConditions;
            
            if (!string.IsNullOrEmpty(SettingsManager.Default.CompanyLogoPath) && File.Exists(SettingsManager.Default.CompanyLogoPath))
            {
                try { imgBrandingLogo.Source = new BitmapImage(new Uri(SettingsManager.Default.CompanyLogoPath)); } catch { }
            }

            UpdateWorkspaceHeader();
            UpdateTemplateSelectionUI();
        }

        private void UpdateWorkspaceHeader()
        {
            string company = SettingsManager.Default.CompanyName?.Trim() ?? "";
            if (string.IsNullOrEmpty(company))
            {
                txtSidebarWorkspaceHeader.Text = "MemoBud Workspace";
            }
            else
            {
                txtSidebarWorkspaceHeader.Text = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase($"{company.ToLower()} workspace");
            }
        }

        private void CustomizationView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool shouldBeStacked = e.NewSize.Width < 1000;
            if (shouldBeStacked == _isStacked) return;
            
            _isStacked = shouldBeStacked;
            
            if (_isStacked)
            {
                CustCol0.Width = new GridLength(1, GridUnitType.Star);
                CustCol1.Width = new GridLength(0);
                CustRow0.Height = GridLength.Auto;
                CustRow1.Height = new GridLength(1, GridUnitType.Star);
                
                Grid.SetColumn(BrandingSection, 0);
                Grid.SetRow(BrandingSection, 0);
                BrandingSection.Margin = new Thickness(0, 0, 0, 32);
                
                Grid.SetColumn(TemplateSelectionSection, 0);
                Grid.SetRow(TemplateSelectionSection, 1);
            }
            else
            {
                CustCol0.Width = new GridLength(380);
                CustCol1.Width = new GridLength(1, GridUnitType.Star);
                CustRow0.Height = new GridLength(1, GridUnitType.Star);
                CustRow1.Height = new GridLength(0);
                
                Grid.SetColumn(BrandingSection, 0);
                Grid.SetRow(BrandingSection, 0);
                BrandingSection.Margin = new Thickness(0, 0, 32, 0);
                
                Grid.SetColumn(TemplateSelectionSection, 1);
                Grid.SetRow(TemplateSelectionSection, 0);
            }
        }

        private void UpdateTemplateSelectionUI()
        {
            Dispatcher.BeginInvoke(new Action(() => {
                try 
                {
                    string currentId = SettingsManager.Default.SelectedTemplateId;
                    if (RecentlyUsedList == null) return;
                    
                    RecentlyUsedList.Children.Clear();

                    // 1. Add Current Template First
                    if (IsValidTemplateId(currentId))
                    {
                        AddTemplateToRecentlyUsed(currentId, true);
                    }

                    // 2. Add History (Filtered & Unique)
                    if (SettingsManager.Default.PreviousTemplateIds != null)
                    {
                        var uniqueHistory = SettingsManager.Default.PreviousTemplateIds
                            .Where(id => id != currentId && IsValidTemplateId(id))
                            .Distinct()
                            .Take(6)
                            .ToList();

                        foreach (var prevId in uniqueHistory)
                        {
                            AddTemplateToRecentlyUsed(prevId, false);
                        }
                    }

                    // 3. Update Library Highlights (Search all Panels)
                    var panels = new[] { HalfA4TemplatesList, FullA4TemplatesList, CustomTemplatesList };
                    foreach (var panel in panels)
                    {
                        if (panel == null) continue;
                        foreach (var child in panel.Children)
                        {
                            if (child is Border b && b.Tag is string tid)
                            {
                                bool isActive = currentId == tid;
                                bool isVisual = _visualSelectedId == tid;

                                b.BorderThickness = new Thickness(isActive ? 3 : (isVisual ? 2 : 1));
                                b.BorderBrush = (Brush)TryFindResource(isActive ? "PrimaryBrush" : (isVisual ? "PrimaryBrush" : "OutlineBrush"));
                                b.Background = (Brush)TryFindResource(isActive ? "PrimaryContainerBrush" : "SurfaceContainerLowBrush");
                            }
                        }
                    }
                } 
                catch (Exception ex) 
                {
                    System.Diagnostics.Debug.WriteLine($"Update UI Error: {ex.Message}");
                }
            }));
        }

        private bool IsValidTemplateId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (id == "Custom" || id.StartsWith("CustomPDF:") || id.StartsWith("UserDesign:") || id.StartsWith("SystemTemplate:") || id == "NewTemplate") return true;
            
            var validIds = new[] { 
                "HalfCorporate", "HalfElegant", "HalfModernDark", "HalfTechnical",
                "FullCorporate", "FullElegant", "FullModernDark", "FullTechnical"
            };
            return validIds.Contains(id);
        }

        private void AddTemplateToRecentlyUsed(string id, bool isCurrent)
        {
            var border = new Border
            {
                Width = 160,
                Height = 200,
                Margin = new Thickness(0, 0, 16, 12),
                CornerRadius = new CornerRadius(16),
                Background = (Brush)TryFindResource("SurfaceContainerLowBrush"),
                BorderBrush = (Brush)TryFindResource(isCurrent ? "PrimaryBrush" : "OutlineBrush"),
                BorderThickness = new Thickness(isCurrent ? 2 : 1),
                Cursor = Cursors.Hand,
                ToolTip = GetTemplateDisplayName(id)
            };

            var grid = new Grid();
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            
            var preview = new Border
            {
                Height = 110,
                CornerRadius = new CornerRadius(10),
                Background = Brushes.White,
                ClipToBounds = true,
                Margin = new Thickness(12),
                BorderBrush = (Brush)TryFindResource("OutlineBrush"),
                BorderThickness = new Thickness(0.5)
            };

            var previewControl = GetTemplatePreviewControl(id);
            preview.Child = previewControl;

            var name = new TextBlock
            {
                Text = GetTemplateDisplayName(id),
                FontSize = 10,
                FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)TryFindResource("OnSurfaceBrush"),
                Margin = new Thickness(4, 0, 4, 0)
            };

            stack.Children.Add(preview);
            stack.Children.Add(name);
            grid.Children.Add(stack);

            if (isCurrent)
            {
                var badge = new Border
                {
                    Background = (Brush)TryFindResource("PrimaryBrush"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 50)
                };
                badge.Child = new TextBlock { Text = "IN USE", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                grid.Children.Add(badge);
            }

            border.Child = grid;
            border.MouseLeftButtonDown += (s, e) => ShowPreviewMode(id);
            RecentlyUsedList.Children.Add(border);
        }

        private void ClickTimer_Tick(object? sender, EventArgs e)
        {
            _clickTimer.Stop();
            if (!string.IsNullOrEmpty(_pendingClickId))
            {
                SettingsManager.Default.SelectedTemplateId = _pendingClickId;
                SettingsManager.Save();
                UpdateTemplateSelectionUI();
                _pendingClickId = string.Empty;
            }
        }

        private void Template_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string templateId)
            {
                // Single click logic: Visual selection ONLY
                _visualSelectedId = templateId;
                UpdateTemplateSelectionUI();

                // Double click logic: Preview (Selection ONLY via Preview Button)
                if (e.ClickCount == 2)
                {
                    if (templateId == "Custom") CustomizeTemplate_Click(sender, e);
                    else ShowPreviewMode(templateId);
                }
            }
        }

        private void ShowPreviewMode(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return;

            DesignerMainScroller.Visibility = Visibility.Collapsed;
            TemplatePreviewMode.Visibility = Visibility.Visible;
            
            PopulateSideList();
            UpdatePreview(templateId);
        }

        private void SelectTemplate_Click(object sender, RoutedEventArgs e)
        {
            string oldId = SettingsManager.Default.SelectedTemplateId;
            string newId = _currentPreviewId;

            if (oldId != newId)
            {
                // Add old to history if not already there
                if (SettingsManager.Default.PreviousTemplateIds == null)
                    SettingsManager.Default.PreviousTemplateIds = new System.Collections.Generic.List<string>();

                if (!SettingsManager.Default.PreviousTemplateIds.Contains(oldId))
                {
                    SettingsManager.Default.PreviousTemplateIds.Insert(0, oldId);
                }
            }

            SettingsManager.Default.SelectedTemplateId = newId;
            SettingsManager.Save();

            UpdateTemplateSelectionUI();
            UpdatePreview(newId);
            RefreshSidebarHighlights();
        }

        private void HidePreview_Click(object sender, RoutedEventArgs e)
        {
            DesignerMainScroller.Visibility = Visibility.Visible;
            TemplatePreviewMode.Visibility = Visibility.Collapsed;
        }

        private void PopulateSideList()
        {
            try
            {
                TemplateSideList.Children.Clear();
                
                var halfTemplates = new[] 
                { 
                    new { Id = "SystemTemplate:HalfCorporate", Name = "Corporate" },
                    new { Id = "SystemTemplate:HalfElegant", Name = "Elegant" },
                    new { Id = "SystemTemplate:HalfModernDark", Name = "Modern Dark" },
                    new { Id = "SystemTemplate:HalfTechnical", Name = "Technical" }
                };

                var fullTemplates = new[]
                {
                    new { Id = "SystemTemplate:FullCorporate", Name = "Corporate" },
                    new { Id = "SystemTemplate:FullElegant", Name = "Elegant" },
                    new { Id = "SystemTemplate:FullModernDark", Name = "Modern Dark" },
                    new { Id = "SystemTemplate:FullTechnical", Name = "Technical" }
                };

                AddSidebarHeader("HALF A4 (LANDSCAPE) TEMPLATES");
                foreach (var t in halfTemplates) AddTemplateToSidebar(t.Id, t.Name);

                AddSidebarHeader("A4/A5 TEMPLATES");
                foreach (var t in fullTemplates) AddTemplateToSidebar(t.Id, t.Name);

                var userDesigns = SettingsManager.Default.UserTemplates ?? new System.Collections.Generic.List<UserTemplate>();
                if (userDesigns.Count > 0)
                {
                    AddSidebarHeader("YOUR CUSTOM LAYOUTS");
                    foreach (var design in userDesigns)
                    {
                        AddTemplateToSidebar("UserDesign:" + design.Name, design.Name);
                    }
                }
            }
            catch { /* Fallback */ }
        }

        private void AddSidebarHeader(string text)
        {
            TemplateSideList.Children.Add(new TextBlock 
            { 
                Text = text, 
                FontSize = 10, 
                FontWeight = FontWeights.Bold, 
                Opacity = 0.5, 
                Margin = new Thickness(4, 16, 4, 8),
                Foreground = (Brush)TryFindResource("OnSurfaceBrush")
            });
        }

        private void AddTemplateToSidebar(string id, string displayName)
        {
            bool isActive = _currentPreviewId == id;
            
            var border = new Border
            {
                Tag = id,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 4, 0, 8),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(isActive ? 2 : 1),
                BorderBrush = isActive ? (Brush)TryFindResource("PrimaryBrush") : (Brush)TryFindResource("OutlineBrush"),
                Background = isActive ? (Brush)TryFindResource("PrimaryContainerBrush") : (Brush)TryFindResource("SurfaceContainerLowBrush")
            };

            var stack = new StackPanel();
            var previewBox = new Border
            {
                Height = 60,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.White,
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 0, 8),
                BorderBrush = (Brush)TryFindResource("OutlineBrush"),
                BorderThickness = new Thickness(0.5)
            };


            
            var previewControl = GetTemplatePreviewControl(id);
            previewBox.Child = previewControl;

            var label = new TextBlock
            {
                Text = displayName,
                FontSize = 11,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = isActive ? (Brush)TryFindResource("OnPrimaryContainerBrush") : (Brush)TryFindResource("OnSurfaceBrush")
            };

            stack.Children.Add(previewBox);
            stack.Children.Add(label);

            // Add "SELECTED" badge if it's the active template
            if (SettingsManager.Default.SelectedTemplateId == id)
            {
                var badge = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                badge.Child = new TextBlock { Text = "SELECTED", FontSize = 8, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                stack.Children.Add(badge);
            }

            border.Child = stack;

            border.MouseLeftButtonDown += (s, e) => UpdatePreview(id);
            border.MouseEnter += (s, e) => { if (_currentPreviewId != id) border.Background = (Brush)TryFindResource("SurfaceContainerHighestBrush"); };
            border.MouseLeave += (s, e) => { if (_currentPreviewId != id) border.Background = (Brush)TryFindResource("SurfaceContainerLowBrush"); };

            TemplateSideList.Children.Add(border);
        }

        private void RefreshSidebarHighlights()
        {
            foreach (var child in TemplateSideList.Children)
            {
                if (child is Border border && border.Tag is string tid)
                {
                    bool isPreviewing = tid == _currentPreviewId;
                    bool isSelected = tid == SettingsManager.Default.SelectedTemplateId;

                    border.BorderThickness = new Thickness(isPreviewing ? 2 : 1);
                    border.BorderBrush = isPreviewing ? (Brush)TryFindResource("PrimaryBrush") : (Brush)TryFindResource("OutlineBrush");
                    border.Background = isPreviewing ? (Brush)TryFindResource("PrimaryContainerBrush") : (Brush)TryFindResource("SurfaceContainerLowBrush");
                    
                    if (border.Child is StackPanel stack)
                    {
                        var label = stack.Children.OfType<TextBlock>().FirstOrDefault(tb => tb.FontSize == 11);
                        if (label != null)
                        {
                            label.FontWeight = isPreviewing ? FontWeights.Bold : FontWeights.Normal;
                            label.Foreground = isPreviewing ? (Brush)TryFindResource("OnPrimaryContainerBrush") : (Brush)TryFindResource("OnSurfaceBrush");
                        }

                        // Update or add/remove badge
                        var existingBadge = stack.Children.OfType<Border>().FirstOrDefault(b => b.HorizontalAlignment == HorizontalAlignment.Center && b.Child is TextBlock tb && tb.Text == "SELECTED");
                        if (isSelected && existingBadge == null)
                        {
                            var badge = new Border
                            {
                                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(6, 2, 6, 2),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Margin = new Thickness(0, 4, 0, 0)
                            };
                            badge.Child = new TextBlock { Text = "SELECTED", FontSize = 8, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                            stack.Children.Add(badge);
                        }
                        else if (!isSelected && existingBadge != null)
                        {
                            stack.Children.Remove(existingBadge);
                        }
                    }
                }
            }
        }
        public void RefreshCustomTemplates()
        {
            try
            {
                CustomTemplatesList.Children.Clear();
                string customFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp", "CustomTemplates");
                
                if (!Directory.Exists(customFolder))
                {
                    Directory.CreateDirectory(customFolder);
                }

                var files = Directory.EnumerateFiles(customFolder).ToList();
                var userDesigns = SettingsManager.Default.UserTemplates ?? new System.Collections.Generic.List<UserTemplate>();
                
                if (files.Count == 0 && userDesigns.Count == 0)
                {
                    EmptyCustomTemplatesCard.Visibility = Visibility.Visible;
                    CustomTemplatesList.Visibility = Visibility.Collapsed;
                }
                else
                {
                    EmptyCustomTemplatesCard.Visibility = Visibility.Collapsed;
                    CustomTemplatesList.Visibility = Visibility.Visible;

                    // 1. "Create New Template" Card
                    var newTemplateCard = CreateNewTemplateCard();
                    CustomTemplatesList.Children.Add(newTemplateCard);

                    // 2. Show User-Designed Templates
                    foreach (var design in userDesigns)
                    {
                        var border = CreateTemplateCard("UserDesign:" + design.Name, design.Name, true);
                        CustomTemplatesList.Children.Add(border);
                    }

                    // 3. Show PDF/Image Templates
                    foreach (var file in files)
                    {
                        var border = CreateTemplateCard("CustomPDF:" + file, Path.GetFileName(file), false);
                        CustomTemplatesList.Children.Add(border);
                    }
                }

                PopulateSystemTemplates();
                PopulateSideList();

                // Refresh current preview if visible to ensure constant sync
                if (TemplatePreviewMode != null && TemplatePreviewMode.Visibility == Visibility.Visible)
                {
                    UpdatePreview(_currentPreviewId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh Error: {ex.Message}");
            }
        }

        private void PopulateSystemTemplates()
        {
            if (HalfA4TemplatesList == null || FullA4TemplatesList == null) return;

            HalfA4TemplatesList.Children.Clear();
            FullA4TemplatesList.Children.Clear();

            var halfIds = new[] { "HalfCorporate", "HalfElegant", "HalfModernDark", "HalfTechnical" };
            foreach (var id in halfIds)
            {
                var fullId = "SystemTemplate:" + id;
                var card = CreateTemplateCard(fullId, GetTemplateDisplayName(id), true);
                HalfA4TemplatesList.Children.Add(card);
            }

            var fullIds = new[] { "FullCorporate", "FullElegant", "FullModernDark", "FullTechnical" };
            foreach (var id in fullIds)
            {
                var fullId = "SystemTemplate:" + id;
                var card = CreateTemplateCard(fullId, GetTemplateDisplayName(id), true);
                FullA4TemplatesList.Children.Add(card);
            }
        }

        private Border CreateNewTemplateCard()
        {
            var border = new Border
            {
                Width = 140,
                Height = 180,
                Margin = new Thickness(0, 0, 12, 12),
                CornerRadius = new CornerRadius(12),
                Background = (Brush)TryFindResource("PrimaryContainerBrush"),
                BorderBrush = (Brush)TryFindResource("PrimaryBrush"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var icon = new TextBlock 
            { 
                Text = "+", 
                FontSize = 32, 
                FontWeight = FontWeights.Bold, 
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)TryFindResource("OnPrimaryContainerBrush")
            };
            var label = new TextBlock 
            { 
                Text = "Create New", 
                FontSize = 10, 
                FontWeight = FontWeights.SemiBold, 
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)TryFindResource("OnPrimaryContainerBrush")
            };

            stack.Children.Add(icon);
            stack.Children.Add(label);
            border.Child = stack;

            border.MouseLeftButtonDown += (s, e) => CustomizeTemplate_Click(null!, null!);

            return border;
        }

        private Border CreateTemplateCard(string id, string name, bool isDesign)
        {
            var border = new Border
            {
                Width = 140,
                Height = 180,
                Margin = new Thickness(0, 0, 12, 12),
                CornerRadius = new CornerRadius(12),
                Background = (Brush)TryFindResource("SurfaceContainerLowBrush"),
                BorderBrush = (Brush)TryFindResource(SettingsManager.Default.SelectedTemplateId == id ? "PrimaryBrush" : "OutlineBrush"),
                BorderThickness = new Thickness(SettingsManager.Default.SelectedTemplateId == id ? 2 : 1),
                Cursor = Cursors.Hand,
                Tag = id
            };

            var mainGrid = new Grid();
            
            var stack = new StackPanel();
            var preview = new Border
            {
                Height = 100,
                CornerRadius = new CornerRadius(10),
                Background = Brushes.White,
                ClipToBounds = true,
                Margin = new Thickness(8),
                BorderBrush = (Brush)TryFindResource("OutlineBrush"),
                BorderThickness = new Thickness(0.5)
            };

            var previewControl = GetTemplatePreviewControl(id);
            preview.Child = previewControl;

            var nameTxt = new TextBlock
            {
                Text = name,
                FontSize = 9,
                FontWeight = SettingsManager.Default.SelectedTemplateId == id ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)TryFindResource("OnSurfaceBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(4, 0, 4, 4)
            };

            stack.Children.Add(preview);
            stack.Children.Add(nameTxt);

            // Professional Menu (Ellipsis)
            var menuBtn = new Button
            {
                Content = "⋮",
                FontSize = 18,
                Width = 28,
                Height = 28,
                Style = (Style)TryFindResource("SecondaryButtonStyle"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 6, 0),
                Padding = new Thickness(0, 0, 0, 2),
                Background = (Brush)TryFindResource("SurfaceContainerHighestBrush"),
                BorderBrush = (Brush)TryFindResource("OutlineBrush"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = "Template Options"
            };

            var contextMenu = new ContextMenu { Style = (Style)TryFindResource("PremiumContextMenuStyle") };
            if (isDesign)
            {
                var editItem = new MenuItem { Header = "Edit Template", Icon = "✏️", Style = (Style)TryFindResource("PremiumMenuItemStyle") };
                editItem.Click += (s, e) => EditUserDesign_Click(id.Replace("UserDesign:", ""));
                contextMenu.Items.Add(editItem);
            }

            var deleteItem = new MenuItem { Header = "Delete Template", Icon = "🗑️", Style = (Style)TryFindResource("PremiumDestructiveMenuItemStyle") };
            deleteItem.Click += (s, e) => {
                if (MessageBox.Show($"Are you sure you want to delete '{name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    if (isDesign) SettingsManager.DeleteUserTemplate(name);
                    else try { File.Delete(id.Replace("CustomPDF:", "")); } catch { }
                    RefreshCustomTemplates();
                }
            };
            contextMenu.Items.Add(deleteItem);

            menuBtn.Click += (s, e) => {
                contextMenu.PlacementTarget = menuBtn;
                contextMenu.Placement = PlacementMode.Bottom;
                contextMenu.IsOpen = true;
                e.Handled = true;
            };

            // Overlay the menu button on the main layout
            var mainGridContainer = new Grid();
            mainGridContainer.Children.Add(stack);
            mainGridContainer.Children.Add(menuBtn);
            
            border.Child = mainGridContainer;

            border.MouseLeftButtonDown += (s, e) => {
                if (e.ClickCount == 2)
                {
                    _clickTimer.Stop();
                    _pendingClickId = string.Empty;
                    ShowPreviewMode(id);
                }
                else
                {
                    _clickTimer.Stop();
                    _pendingClickId = id;
                    _visualSelectedId = id;
                    UpdateTemplateSelectionUI();
                    _clickTimer.Start();
                }
            };

            return border;
        }

        private FrameworkElement GetTemplatePreviewControl(string id)
        {
            var viewbox = new Viewbox { Stretch = Stretch.UniformToFill };
            
            // ALL templates now use the editable CustomTemplate renderer
            string resourceKey = "CustomTemplate";
            if (id.StartsWith("CustomPDF:"))
                resourceKey = "CustomTemplate";

            var template = TryFindResource(resourceKey) as DataTemplate;
            var contentControl = new ContentControl { ContentTemplate = template };

            // Load Data
            var demo = new PrintViewModel 
            { 
                CompanyName = string.IsNullOrEmpty(SettingsManager.Default.CompanyName) ? "YOUR COMPANY NAME" : SettingsManager.Default.CompanyName,
                CompanyAddress = string.IsNullOrEmpty(SettingsManager.Default.CompanyAddress) ? "123 Business Avenue, Suite 100\nNew York, NY 10001" : SettingsManager.Default.CompanyAddress,
                CompanyPhone = SettingsManager.Default.CompanyPhone ?? "555-0123",
                CompanyPhone2 = SettingsManager.Default.CompanyPhone2 ?? "",
                MemoNumber = "SM-12345",
                Date = DateTime.Now.ToString("MMM dd, yyyy"),
                CustomerName = "Johnathan Doe",
                CustomerPhone = "+1 (555) 000-1234",
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

            // All templates go through JSON block renderer
            try {
                string json = "";
                if (id == "Custom") json = SettingsManager.Default.CustomTemplateJson;
                else if (id.StartsWith("UserDesign:")) {
                    string name = id.Replace("UserDesign:", "");
                    json = SettingsManager.Default.UserTemplates?.FirstOrDefault(t => t.Name == name)?.JsonData ?? "";
                }
                else if (id.StartsWith("SystemTemplate:")) {
                    string sysId = id.Replace("SystemTemplate:", "");
                    var userVersion = SettingsManager.Default.UserTemplates?.FirstOrDefault(t => t.Name == sysId);
                    if (userVersion != null) json = userVersion.JsonData;
                    else json = DefaultTemplateService.GetTemplateJson(sysId);
                }
                else if (IsValidTemplateId(id)) {
                    json = DefaultTemplateService.GetTemplateJson(id);
                }
                
                if (!string.IsNullOrEmpty(json)) {
                    var blocks = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<ClientApp.CustomTemplateDesignerWindow.DesignerBlock>>(json);
                    if (blocks != null && blocks.Count > 0) {
                        demo.CustomBlocks = blocks;
                        demo.IsHalfA4 = blocks[0].IsHalfA4;
                        var rendererCanvas = TemplateRenderer.Render(blocks, demo);
                        viewbox.Child = rendererCanvas;
                        return viewbox;
                    }
                }
            } catch { }

            contentControl.Content = demo;
            viewbox.Child = contentControl;
            return viewbox;
        }

        private string _currentPreviewId = "Standard";

        private void UpdatePreview(string templateId)
        {
            try
            {
                _currentPreviewId = templateId;
                PreviewTitle.Text = GetTemplateDisplayName(templateId);
                
                // Selection Status UI
                bool isAlreadySelected = SettingsManager.Default.SelectedTemplateId == templateId;
                btnSelectTemplate.Visibility = isAlreadySelected ? Visibility.Collapsed : Visibility.Visible;
                pnlSelectedStatus.Visibility = isAlreadySelected ? Visibility.Visible : Visibility.Collapsed;

                if (btnExportTemplatePreview != null)
                {
                    btnExportTemplatePreview.Visibility = templateId.StartsWith("UserDesign:") ? Visibility.Visible : Visibility.Collapsed;
                }

                var demo = new PrintViewModel
                {
                    CompanyName = string.IsNullOrEmpty(SettingsManager.Default.CompanyName) ? "YOUR COMPANY NAME" : SettingsManager.Default.CompanyName,
                    CompanyAddress = string.IsNullOrEmpty(SettingsManager.Default.CompanyAddress) ? "123 Business Avenue, Suite 100\nNew York, NY 10001" : SettingsManager.Default.CompanyAddress,
                    CompanyPhone = SettingsManager.Default.CompanyPhone ?? "555-0123",
                    CompanyPhone2 = SettingsManager.Default.CompanyPhone2 ?? "",
                    MemoNumber = "SM-12345",
                    Date = DateTime.Now.ToString("MMM dd, yyyy"),
                    CustomerName = "Johnathan Doe",
                    CustomerPhone = "+1 (555) 000-1234",
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

                string contact = $"Phone: {demo.CompanyPhone}";
                if (!string.IsNullOrEmpty(demo.CompanyPhone2))
                    contact += $" / {demo.CompanyPhone2}";
                demo.CompanyContact = contact;

                if (templateId.StartsWith("Half"))
                {
                    demo.IsHalfA4 = true;
                }

                string resourceKey = (templateId == "Custom" || templateId.StartsWith("UserDesign:") || templateId.StartsWith("SystemTemplate:")) ? "CustomTemplate" : (templateId + "Template");
                var template = TryFindResource(resourceKey) as DataTemplate;
                
                if (resourceKey == "CustomTemplate")
                {
                    try {
                        string json = "";
                        if (templateId == "Custom") json = SettingsManager.Default.CustomTemplateJson;
                        else if (templateId.StartsWith("UserDesign:")) {
                            string name = templateId.Replace("UserDesign:", "");
                            json = SettingsManager.Default.UserTemplates?.FirstOrDefault(t => t.Name == name)?.JsonData ?? "";
                        }
                        else if (templateId.StartsWith("SystemTemplate:")) {
                            string sysId = templateId.Replace("SystemTemplate:", "");
                            var userVersion = SettingsManager.Default.UserTemplates?.FirstOrDefault(t => t.Name == sysId);
                            if (userVersion != null) json = userVersion.JsonData;
                            else json = ClientApp.Services.DefaultTemplateService.GetTemplateJson(sysId);
                        }

                        if (!string.IsNullOrEmpty(json)) {
                            var blocks = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<ClientApp.CustomTemplateDesignerWindow.DesignerBlock>>(json);
                            if (blocks != null && blocks.Count > 0) {
                                demo.CustomBlocks = blocks;
                                demo.IsHalfA4 = blocks[0].IsHalfA4;

                                // High Fidelity Rendering using TemplateRenderer
                                var rendererCanvas = TemplateRenderer.Render(blocks, demo);
                                PreviewContentControl.Content = rendererCanvas;
                                PreviewContentControl.ContentTemplate = null; // Clear template to use canvas directly
                                RefreshSidebarHighlights();
                                return;
                            }
                        }
                    } catch { }
                }

                if (template != null)
                {
                    PreviewContentControl.Content = demo;
                    PreviewContentControl.ContentTemplate = template;
                }
                else
                {
                    MessageBox.Show($"Template '{resourceKey}' not found.", "Preview Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                
                RefreshSidebarHighlights();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Preview Error: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetTemplateDisplayName(string id)
        {
            return id switch
            {
                "HalfCorporate" => "Corporate",
                "HalfElegant" => "Elegant",
                "HalfModernDark" => "Modern Dark",
                "HalfTechnical" => "Technical",
                "FullCorporate" => "Corporate",
                "FullElegant" => "Elegant",
                "FullModernDark" => "Modern Dark",
                "FullTechnical" => "Technical",
                _ => id.StartsWith("CustomPDF:") ? Path.GetFileName(id.Replace("CustomPDF:", "")) : 
                     (id.StartsWith("UserDesign:") ? id.Replace("UserDesign:", "") : id + " Layout")
            };
        }

        private void NavMemos_Click(object sender, RoutedEventArgs e)
        {
            DashboardView.Visibility = Visibility.Visible;
            SettingsView.Visibility = Visibility.Collapsed;
            CustomizationView.Visibility = Visibility.Collapsed;
            btnNavMemos.Tag = "Active";
            btnNavSettings.Tag = null;
            btnNavCustom.Tag = null;
        }

        private async void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            DashboardView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
            CustomizationView.Visibility = Visibility.Collapsed;
            btnNavMemos.Tag = null;
            btnNavSettings.Tag = "Active";
            btnNavCustom.Tag = null;
            
            await LoadProfileInfo();
        }

        private async System.Threading.Tasks.Task LoadProfileInfo()
        {
            var licenseManager = new LicenseManager();
            var info = await licenseManager.GetCurrentLicenseInfoAsync();
            txtLicenseKeyDisplay.Text = string.IsNullOrEmpty(info.key) ? "No Active Key" : info.key;
            
            if (string.IsNullOrEmpty(info.key))
            {
                txtLicenseStatusDisplay.Text = "Inactive";
                txtLicenseStatusDisplay.Foreground = (Brush)FindResource("ErrorBrush");
                txtLicenseDevicesDisplay.Text = "-";
            }
            else if (info.expiresAt.HasValue)
            {
                if (info.expiresAt.Value < DateTime.UtcNow)
                {
                    txtLicenseStatusDisplay.Text = "Expired on " + info.expiresAt.Value.ToString("MMM dd, yyyy");
                    txtLicenseStatusDisplay.Foreground = (Brush)FindResource("ErrorBrush");
                }
                else
                {
                    txtLicenseStatusDisplay.Text = "Expires on " + info.expiresAt.Value.ToString("MMM dd, yyyy");
                    txtLicenseStatusDisplay.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f59e0b"));
                }
                
                string limitStr = info.maxDevices == -1 ? "Unlimited" : info.maxDevices.ToString();
                txtLicenseDevicesDisplay.Text = $"{info.activeDevices} / {limitStr}";
            }
            else
            {
                txtLicenseStatusDisplay.Text = "Active (Unlimited)";
                txtLicenseStatusDisplay.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981"));
                
                string limitStr = info.maxDevices == -1 ? "Unlimited" : info.maxDevices.ToString();
                txtLicenseDevicesDisplay.Text = $"{info.activeDevices} / {limitStr}";
            }

            // Set Cloud Sync elements based on remote subscription key capabilities
            if (!string.IsNullOrEmpty(info.key) && info.cloudSyncEnabled)
            {
                txtCloudSyncStatus.Text = "Enabled";
                txtCloudSyncStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981"));
                
                string limitStr = info.cloudStorageLimitGb < 1.0 ? $"{info.cloudStorageLimitGb * 1024.0:F0} MB" : $"{info.cloudStorageLimitGb:F1} GB";
                txtCloudStorageDisplay.Text = $"{info.cloudStorageUsedMb:F2} MB of {limitStr} used";
                borderCloudStorageInfo.Visibility = Visibility.Visible;
                txtCloudSyncWarning.Visibility = Visibility.Collapsed;

                double limitMb = info.cloudStorageLimitGb * 1024.0;
                double pct = limitMb > 0 ? (info.cloudStorageUsedMb / limitMb) * 100.0 : 0.0;
                pbCloudStorage.Value = Math.Min(100.0, Math.Max(0.0, pct));

                // Update Sidebar Cloud Connected Panel
                panelSidebarCloudConnected.Visibility = Visibility.Visible;
                panelSidebarCloudDisconnected.Visibility = Visibility.Collapsed;
                txtSidebarStorageDisplay.Text = $"{info.cloudStorageUsedMb:F2} MB / {limitStr}";
                pbSidebarStorage.Value = Math.Min(100.0, Math.Max(0.0, pct));

                // Enable sync mode choices and hide subscription restriction warning
                cmbSyncModeHybrid.IsEnabled = true;
                cmbSyncModeCloud.IsEnabled = true;
                cmbSyncModeLan.IsEnabled = true;
                txtCloudSyncRequiredWarning.Visibility = Visibility.Collapsed;
                pnlImageUploadCloudSettings.Visibility = Visibility.Visible;
            }
            else
            {
                txtCloudSyncStatus.Text = "Disabled";
                txtCloudSyncStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444"));
                
                borderCloudStorageInfo.Visibility = Visibility.Collapsed;
                txtCloudSyncWarning.Visibility = Visibility.Visible;

                // Update Sidebar Cloud Disconnected Panel
                panelSidebarCloudConnected.Visibility = Visibility.Collapsed;
                panelSidebarCloudDisconnected.Visibility = Visibility.Visible;
                SyncStatusText.Text = "Not Connected";

                // Disable Hybrid & Cloud options, force LAN sync mode
                cmbSyncModeHybrid.IsEnabled = false;
                cmbSyncModeCloud.IsEnabled = false;
                cmbSyncModeLan.IsEnabled = true;
                cmbSyncModeLan.IsSelected = true;

                // Show subscription restriction warning and hide image sync settings
                txtCloudSyncRequiredWarning.Visibility = Visibility.Visible;
                pnlImageUploadCloudSettings.Visibility = Visibility.Collapsed;
            }

            var keyId = licenseManager.GetCurrentKeyId();
            if (!string.IsNullOrEmpty(keyId))
            {
                var profile = await licenseManager.GetProfileAsync(keyId);
                if (profile != null)
                {
                    txtProfileCompany.Text = profile.company_name ?? "-";
                    txtProfileEmail.Text = profile.email_id ?? "-";
                }
            }

            // Check if multiple devices are active globally but not discovered on LAN
            if (info.activeDevices > 1 && LanSyncService.DiscoveredPeers.Count == 0 && !_hasShownLanWarning)
            {
                _hasShownLanWarning = true;
                // Silently upgrade to Hybrid sync mode if it is local only, since remote devices are detected
                if (SettingsManager.Default.SyncMode == "LocalOnly")
                {
                    SettingsManager.Default.SyncMode = "Hybrid";
                    SettingsManager.Save();
                    foreach (ComboBoxItem item in cmbSyncMode.Items)
                    {
                        if (item.Tag?.ToString() == "Hybrid")
                        {
                            cmbSyncMode.SelectedItem = item;
                            break;
                        }
                    }
                    UpdateSyncStatus();
                }
            }

            if (LicenseManager.CurrentStatus != null)
            {
                RefreshLicenseWarningToast(LicenseManager.CurrentStatus);
            }
        }

        private async void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            var licenseManager = new LicenseManager();
            var keyId = licenseManager.GetCurrentKeyId();
            if (!string.IsNullOrEmpty(keyId))
            {
                var profileWindow = new ProfileSetupWindow(keyId) { Owner = this };
                profileWindow.ShowDialog();
                // Refresh after it's closed
                await LoadProfileInfo();
            }
        }

        private async void UpgradePlan_Click(object sender, RoutedEventArgs e)
        {
            var licenseManager = new LicenseManager();
            var info = await licenseManager.GetCurrentLicenseInfoAsync();
            string key = info.key ?? "";
            
            try
            {
                var destination = $"https://servicememomanager.com/upgrade?key={System.Uri.EscapeDataString(key)}";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = destination,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open upgrade portal: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LogoutLicense_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Logging out will remove this license key from this device. To log in again, you will need to re-enter your subscription key.\n\nAre you sure you want to logout?",
                "Confirm License Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var licenseManager = new LicenseManager();
                bool deactivated = await licenseManager.DeactivateLicenseAsync();
                
                if (deactivated)
                {
                    // 1. Wipe local database to prevent leftover records from showing up
                    try
                    {
                        using (var db = new Data.LocalDbContext())
                        {
                            db.ServiceMemos.RemoveRange(db.ServiceMemos);
                            db.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error clearing local db on logout: {ex.Message}");
                    }

                    // 2. Clear credentials from settings
                    SettingsManager.Default.SubscriptionKey = string.Empty;
                    SettingsManager.Default.CloudUserEmail = string.Empty;
                    SettingsManager.Default.IsCloudSyncEnabled = false;
                    SettingsManager.Default.SyncMode = "LocalOnly";
                    SettingsManager.Save();

                    // Open a new ActivationWindow
                    var activationWindow = new ActivationWindow();
                    activationWindow.Show();
                    
                    // Close the current MainWindow
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Failed to cleanly deactivate the license. Please try again or check your internet connection.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void NavigateToOrderLayout()
        {
            NavCustom_Click(this, new RoutedEventArgs());
        }

        private void NavCustom_Click(object sender, RoutedEventArgs e)
        {
            DashboardView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;
            CustomizationView.Visibility = Visibility.Visible;
            btnNavMemos.Tag = null;
            btnNavSettings.Tag = null;
            btnNavCustom.Tag = "Active";
            RefreshCustomTemplates();
        }

        private void SaveBranding_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Default.CompanyName = txtCompanyName.Text;
            SettingsManager.Default.CompanyPhone = txtCompanyPhone.Text;
            SettingsManager.Default.CompanyPhone2 = txtCompanyPhone2.Text;
            SettingsManager.Default.CompanyAddress = txtCompanyAddress.Text;
            SettingsManager.Default.TermsAndConditions = txtTermsAndConditions.Text;
            SettingsManager.Save();

            // Sync updated company details to Supabase Cloud in the background if SyncMode is enabled
            if (SettingsManager.Default.SyncMode != "LocalOnly" && !string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey))
            {
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var licenseManager = new LicenseManager();
                        var keyId = licenseManager.GetCurrentKeyId();
                        if (!string.IsNullOrEmpty(keyId))
                        {
                            string? logoBase64 = null;
                            string logoPath = SettingsManager.Default.CompanyLogoPath;
                            if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
                            {
                                try
                                {
                                    byte[] fileBytes = System.IO.File.ReadAllBytes(logoPath);
                                    string base64String = Convert.ToBase64String(fileBytes);
                                    string ext = System.IO.Path.GetExtension(logoPath).ToLower();
                                    string mimeType = ext == ".png" ? "image/png" : 
                                                      (ext == ".jpg" || ext == ".jpeg") ? "image/jpeg" : 
                                                      "application/octet-stream";
                                    logoBase64 = $"data:{mimeType};base64,{base64String}";
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to encode company logo: {ex.Message}");
                                }
                            }

                            var profile = new CompanyProfile
                            {
                                company_name = SettingsManager.Default.CompanyName,
                                phone_number = SettingsManager.Default.CompanyPhone,
                                email_id = SettingsManager.Default.CompanyPhone2, // Using CompanyPhone2 as email/other contact info
                                logo_base64 = logoBase64
                            };
                            
                            // Check for existing profile to preserve unchanged fields
                            var existing = await licenseManager.GetProfileAsync(keyId);
                            if (existing != null)
                            {
                                profile.id = existing.id;
                                profile.activation_key_id = existing.activation_key_id;
                                // Keep email_id unchanged if CompanyPhone2 was empty
                                if (string.IsNullOrEmpty(profile.email_id))
                                {
                                    profile.email_id = existing.email_id;
                                }
                                // Keep logo unchanged if logoBase64 is null
                                if (string.IsNullOrEmpty(profile.logo_base64))
                                {
                                    profile.logo_base64 = existing.logo_base64;
                                }
                            }
                            else
                            {
                                profile.activation_key_id = keyId;
                            }
                            
                            await licenseManager.SaveProfileAsync(keyId, profile);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error syncing branding details to Supabase: {ex.Message}");
                    }
                });
            }

            UpdateWorkspaceHeader();
            UpdatePreview(_currentPreviewId);
            MessageBox.Show("Branding details updated successfully!", "Branding Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UploadLogo_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
                Title = "Select Company Logo"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServiceMemoApp");
                    if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);
                    
                    string destPath = Path.Combine(appDataPath, "company_logo" + Path.GetExtension(openFileDialog.FileName));
                    File.Copy(openFileDialog.FileName, destPath, true);
                    
                    SettingsManager.Default.CompanyLogoPath = destPath;
                    SettingsManager.Save();
                    
                    imgBrandingLogo.Source = new BitmapImage(new Uri(destPath));
                    UpdatePreview(_currentPreviewId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error uploading logo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CustomizeTemplate_Click(object sender, RoutedEventArgs e)
        {
            string? initialData = null;
            string? initialName = null;
            
            // Only import current if NOT clicking "Create New" (sender is null for Create New)
            if (sender != null) 
            {
                string currentId = (TemplatePreviewMode.Visibility == Visibility.Visible) ? _currentPreviewId : SettingsManager.Default.SelectedTemplateId;
                
                if (currentId.StartsWith("UserDesign:"))
                {
                    string name = currentId.Replace("UserDesign:", "");
                    initialData = SettingsManager.Default.UserTemplates?.FirstOrDefault(t => t.Name == name)?.JsonData;
                    initialName = name;
                }
                else if (currentId == "Custom")
                {
                    initialData = SettingsManager.Default.CustomTemplateJson;
                }
                else if (currentId.StartsWith("SystemTemplate:"))
                {
                    string sysId = currentId.Replace("SystemTemplate:", "");
                    initialData = ClientApp.Services.DefaultTemplateService.GetTemplateJson(sysId);
                }
                else if (IsValidTemplateId(currentId))
                {
                    initialData = currentId; // Pass the ID, designer will handle it
                }
            }

            var designer = new CustomTemplateDesignerWindow(initialData, initialName);
            designer.Owner = this;
            if (designer.ShowDialog() == true)
            {
                RefreshCustomTemplates();
                UpdateTemplateSelectionUI();
                
                string refreshId = (TemplatePreviewMode.Visibility == Visibility.Visible) ? _currentPreviewId : SettingsManager.Default.SelectedTemplateId;
                UpdatePreview(refreshId);
            }
        }

        private void EditUserDesign_Click(string name)
        {
            var design = SettingsManager.Default.UserTemplates?.FirstOrDefault(t => t.Name == name);
            if (design != null)
            {
                var designer = new CustomTemplateDesignerWindow(design.JsonData, name);
                designer.Owner = this;
                if (designer.ShowDialog() == true)
                {
                    RefreshCustomTemplates();
                    UpdateTemplateSelectionUI();
                    UpdatePreview(SettingsManager.Default.SelectedTemplateId);
                }
            }
        }

        private void ExportTemplatePreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPreviewId) || !_currentPreviewId.StartsWith("UserDesign:"))
                return;

            string name = _currentPreviewId.Replace("UserDesign:", "");
            var selectedTemplate = SettingsManager.Default.UserTemplates?.FirstOrDefault(t => t.Name == name);
            
            if (selectedTemplate == null)
            {
                MessageBox.Show("Template not found.", "Export Layout", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "MemoBud Layout Design (*.mbld)|*.mbld",
                    DefaultExt = ".mbld",
                    Title = "Export Layout Design",
                    FileName = selectedTemplate.Name + ".mbld"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(saveFileDialog.FileName, selectedTemplate.JsonData);
                    MessageBox.Show("Layout exported successfully!", "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting layout: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportLayout_Click(object sender, RoutedEventArgs e)
        {
            string templateName = txtImportName.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(templateName))
            {
                MessageBox.Show("Please enter a template name in the input box before importing.", "Template Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtImportName.Focus();
                return;
            }

            if (SettingsManager.Default.UserTemplates != null && 
                SettingsManager.Default.UserTemplates.Any(ut => ut.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase)))
            {
                var overwriteResult = MessageBox.Show($"A template named '{templateName}' already exists. Would you like to overwrite it?\n\n(Choosing 'No' will automatically append a timestamp to the name instead.)", "Duplicate Template Name", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (overwriteResult == MessageBoxResult.Cancel)
                {
                    return;
                }
                else if (overwriteResult == MessageBoxResult.No)
                {
                    templateName += "_" + DateTime.Now.ToString("Hmm");
                }
            }

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MemoBud Layout Design (*.mbld)|*.mbld",
                Title = "Import Layout Design (.mbld)"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string json = System.IO.File.ReadAllText(openFileDialog.FileName);

                    if (SettingsManager.Default.UserTemplates == null)
                        SettingsManager.Default.UserTemplates = new System.Collections.Generic.List<UserTemplate>();

                    SettingsManager.Default.UserTemplates.RemoveAll(ut => ut.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));
                    SettingsManager.Default.UserTemplates.Add(new UserTemplate { Name = templateName, JsonData = json });
                    SettingsManager.Save();
                    
                    txtImportName.Text = "";
                    RefreshCustomTemplates();
                    UpdateTemplateSelectionUI();

                    MessageBox.Show($"✅ Layout '{templateName}' imported successfully!", "Import Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error importing layout:\n\n" + ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void DarkMode_Click(object sender, RoutedEventArgs e)
        {
            bool isDark = chkDarkMode.IsChecked ?? false;
            ThemeManager.SetTheme(isDark);
            LightPresetsGrid.Visibility = isDark ? Visibility.Collapsed : Visibility.Visible;
            DarkPresetsGrid.Visibility = isDark ? Visibility.Visible : Visibility.Collapsed;
        }

        private void chkSettingsSyncImages_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Default.SyncImagesEnabled = chkSettingsSyncImages.IsChecked ?? false;
            SettingsManager.Save();
        }

        private void cmbSyncMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSyncMode == null || !this.IsLoaded) return;
            
            if (cmbSyncMode.SelectedItem is ComboBoxItem item && item.Tag is string mode)
            {
                SettingsManager.Default.SyncMode = mode;
                SettingsManager.Save();
                
                if (mode == "InternetOnly")
                {
                    LanSyncService.Stop();
                }
                else
                {
                    LanSyncService.Stop();
                    LanSyncService.Start();
                }
                
                UpdateNetworkStatus();
                LanSyncService_PeersChanged();
            }
        }

        private void InitializeNetworkMonitoring()
        {
            System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged += (s, e) => {
                this.Dispatcher.Invoke(() => UpdateNetworkStatus());
            };
            UpdateNetworkStatus();
        }

        private void RefreshCloudOfflineToast()
        {
            bool isNetAvailable = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
            bool isCloudOffline = CloudSyncService.IsCloudOffline || !isNetAvailable;
            bool isLocalOnly = SettingsManager.Default.SyncMode == "LocalOnly";
            bool isCloudEnabled = SettingsManager.Default.IsCloudSyncEnabled;

            bool showOfflineToast = isCloudEnabled && !isLocalOnly && isCloudOffline;

            if (borderCloudOfflineToast != null)
            {
                borderCloudOfflineToast.Visibility = showOfflineToast ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SwitchOffline_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Default.IsCloudSyncEnabled = false;
            SettingsManager.Default.SyncMode = "LocalOnly";
            SettingsManager.Save();
            
            // Refresh UI
            UpdateCloudSyncSidebarUI();
            RefreshCloudOfflineToast();
            
            MessageBox.Show("Switched to Local Offline Mode. Cloud sync is disabled. You can re-enable it anytime from Cloud Settings.", "Offline Mode", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RefreshLicenseWarningToast(LicenseStatus status)
        {
            if (borderLicenseWarningToast != null && txtLicenseWarningText != null)
            {
                if (status != null && status.IsValid && !string.IsNullOrEmpty(status.WarningMessage))
                {
                    txtLicenseWarningText.Text = status.WarningMessage;
                    borderLicenseWarningToast.Visibility = Visibility.Visible;
                }
                else
                {
                    borderLicenseWarningToast.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void UpdateCloudSyncSidebarUI()
        {
            if (SettingsManager.Default.IsCloudSyncEnabled)
            {
                if (panelSidebarCloudConnected == null || panelSidebarCloudDisconnected == null) return;
                
                panelSidebarCloudConnected.Visibility = Visibility.Visible;
                panelSidebarCloudDisconnected.Visibility = Visibility.Collapsed;

                double usedMb = CloudSyncService.RealTimeCloudStorageUsedMb;
                int rowsCount = CloudSyncService.RealTimeCloudRowsCount;
                
                // If it is 0 (e.g. at startup before any sync has completed), estimate it from SQLite
                if (rowsCount == 0)
                {
                    using (var db = new LocalDbContext())
                    {
                        rowsCount = db.ServiceMemos.Count(m => m.Status != "Deleted");
                    }
                }

                // If usedMb is 0.0 (e.g. at startup), estimate it (approx 1.5 KB per row)
                if (usedMb == 0.0 && rowsCount > 0)
                {
                    usedMb = (rowsCount * 1.5) / 1024.0;
                }

                var licenseManager = new LicenseManager();
                var info = await licenseManager.GetCurrentLicenseInfoAsync();
                double limitGb = info.cloudStorageLimitGb;
                if (limitGb <= 0) limitGb = 5.0; // Default fallback is 5.0 GB

                double limitMb = limitGb * 1024.0;
                double pct = limitMb > 0 ? (usedMb / limitMb) * 100.0 : 0.0;

                string limitStr = limitGb < 1.0 ? $"{limitGb * 1024.0:F0} MB" : $"{limitGb:F1} GB";
                txtSidebarStorageDisplay.Text = $"{usedMb:F2} MB / {limitStr} ({rowsCount} rows)";
                pbSidebarStorage.Value = Math.Min(100.0, Math.Max(0.0, pct));
                
                SyncStatusText.Text = "Connected";
                SyncStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
            }
            else
            {
                if (panelSidebarCloudConnected == null || panelSidebarCloudDisconnected == null) return;
                
                panelSidebarCloudConnected.Visibility = Visibility.Collapsed;
                panelSidebarCloudDisconnected.Visibility = Visibility.Visible;
                SyncStatusText.Text = "Not Connected";
                if (TryFindResource("OnSurfaceBrush") is Brush brush)
                {
                    SyncStatusText.Foreground = brush;
                }
            }
        }

        private void UpdateNetworkStatus()
        {
            bool isNetAvailable = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
            bool isInternetOnly = SettingsManager.Default.SyncMode == "InternetOnly";
            
            NetworkDisconnectOverlay.Visibility = Visibility.Collapsed;

            if (!isNetAvailable)
            {
                txtLanSyncStatus.Text = "Disconnected";
                txtLanSyncStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // red
            }
            else
            {
                if (isInternetOnly)
                {
                    txtLanSyncStatus.Text = "Disabled";
                    txtLanSyncStatus.Foreground = (Brush)TryFindResource("OnSurfaceVariantBrush");
                }
                else
                {
                    txtLanSyncStatus.Text = "Active";
                    txtLanSyncStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // green
                }
            }

            RefreshCloudOfflineToast();
        }

        private void LanSyncService_PeersChanged()
        {
            this.Dispatcher.Invoke(() =>
            {
                var peers = LanSyncService.DiscoveredPeers.Values.ToList();
                txtLanPeersCount.Text = $"{peers.Count} Device(s)";
                
                if (peers.Count == 0)
                {
                    txtLanPeersList.Text = "No active peers found on LAN.";
                    
                    txtLicenseNoLocalDevicesFound.Visibility = Visibility.Visible;
                    lstLicenseDiscoveredDevices.ItemsSource = null;
                }
                else
                {
                    txtLanPeersList.Text = string.Join("\n", peers.Select(p => $"• {p.MachineName} ({p.IPAddress})"));
                    
                    txtLicenseNoLocalDevicesFound.Visibility = Visibility.Collapsed;
                    lstLicenseDiscoveredDevices.ItemsSource = peers;
                }
            });
        }

        public static void ShowOrderCompletedNotification(string memoNumber, string deviceName, string deviceModel, string technicianName)
        {
            string title = "Order Completed via Staff Portal";
            string message = $"Service Memo {memoNumber} ({deviceName} - {deviceModel}) has been marked as Completed by Staff Member: {technicianName}.";

            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                // 1. Native Windows Toast / Balloon Notification (bottom-right of Windows desktop)
                try
                {
                    var notifyIcon = new System.Windows.Forms.NotifyIcon
                    {
                        Icon = System.Drawing.SystemIcons.Information,
                        Visible = true,
                        Text = "Job Order Generator"
                    };

                    notifyIcon.ShowBalloonTip(6000, title, message, System.Windows.Forms.ToolTipIcon.Info);

                    Task.Delay(7000).ContinueWith(_ =>
                    {
                        try
                        {
                            notifyIcon.Visible = false;
                            notifyIcon.Dispose();
                        }
                        catch { }
                    });
                }
                catch { }

                // 2. WPF Message Box Dialog
                try
                {
                    MessageBox.Show(
                        message,
                        title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch { }
            }));
        }

        private void CloudSyncService_CloudOrderCompleted(ServiceMemoDto memo)
        {
            ShowOrderCompletedNotification(memo.MemoNumber, memo.DeviceName, memo.DeviceModel, memo.TechnicianName);
        }

        private void LanSyncService_SyncStateChanged(bool active)
        {
            this.Dispatcher.Invoke(() =>
            {
                panelSyncLoading.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            LanSyncService.Stop();
            base.OnClosing(e);
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string preset)
            {
                ThemeManager.SetTheme(chkDarkMode.IsChecked ?? false, preset);
            }
        }

        internal void LoadData()
        {
            using (var db = new LocalDbContext())
            {
                db.Migrate();
                var query = db.ServiceMemos.Where(m => m.Status != "Deleted" && m.Status != "Deleted_Synced").AsQueryable();

                // Internet-Only mode: only show cloud-origin memos (IN prefix)
                if (SettingsManager.Default.SyncMode == "InternetOnly")
                {
                    query = query.Where(m => m.MemoNumber.StartsWith("IN"));
                }

                var searchText = txtSearch?.Text?.Trim().ToLower();
                if (!string.IsNullOrEmpty(searchText))
                {
                    query = query.Where(m => 
                        m.CustomerName.ToLower().Contains(searchText) || 
                        m.PhoneNumber.Contains(searchText) || 
                        m.DeviceName.ToLower().Contains(searchText) ||
                        (m.TechnicianName != null && m.TechnicianName.ToLower().Contains(searchText)) ||
                        m.MemoNumber.ToLower().Contains(searchText));
                }

                var memos = query.OrderByDescending(m => m.CreatedAt).ToList();

                bool needsSave = false;
                foreach (var memo in memos)
                {
                    if (!string.IsNullOrEmpty(memo.Status) && memo.Status.StartsWith("System.Windows.Controls.ComboBoxItem: "))
                    {
                        memo.Status = memo.Status.Replace("System.Windows.Controls.ComboBoxItem: ", "");
                        db.ServiceMemos.Update(memo);
                        needsSave = true;
                    }
                }
                if (needsSave) db.SaveChanges();

                var view = new System.Windows.Data.ListCollectionView(memos);
                view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription("CreatedAt", new DateGroupConverter()));
                MemosDataGrid.ItemsSource = view;
            }
            UpdateSyncStatus();
        }

        /// <summary>
        /// Converts a DateTime to a friendly date group label: "Today", "Yesterday", or a formatted date string.
        /// </summary>
        private class DateGroupConverter : System.Windows.Data.IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value is DateTime dt)
                {
                    var today = DateTime.Today;
                    if (dt.Date == today) return "TODAY";
                    if (dt.Date == today.AddDays(-1)) return "YESTERDAY";
                    return $"{dt:dddd, MMMM d, yyyy}".ToUpper();
                }
                return "UNKNOWN DATE";
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
                => throw new NotSupportedException();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e) => LoadData();

        private async void UpdateSyncStatus()
        {
            await LoadProfileInfo();
            if (SettingsManager.Default.IsCloudSyncEnabled && !string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey))
            {
                btnCloudLogin.Content = "Cloud Settings";
            }
            else
            {
                btnCloudLogin.Content = "Configure Cloud";
            }
        }

        private void WhatsApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ClientApp.Models.ServiceMemo memo)
            {
                if (!string.IsNullOrEmpty(memo.PhoneNumber))
                {
                    string url = $"https://wa.me/{memo.PhoneNumber.Replace(" ", "").Replace("-", "").Replace("+", "")}";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                }
            }
        }

        private void ExportBackup_Click(object sender, RoutedEventArgs e) => BackupManager.ExportBackup();
        private void ImportBackup_Click(object sender, RoutedEventArgs e) { BackupManager.ImportBackup(); LoadData(); }
        private void CloudSettings_Click(object sender, RoutedEventArgs e) { var settingsWindow = new CloudSettingsWindow(); settingsWindow.Owner = this; settingsWindow.ShowDialog(); UpdateSyncStatus(); }
        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            dbSyncProgress.Visibility = Visibility.Visible;
            if (txtSearch != null) txtSearch.Text = string.Empty;
            LoadData();

            try
            {
                // Sync with LAN peers
                var peers = LanSyncService.DiscoveredPeers.Values.ToList();
                foreach (var peer in peers)
                {
                    await LanSyncService.SyncWithSinglePeerAsync(peer);
                }

                // Sync with cloud if applicable
                if (SettingsManager.Default.SyncMode != "LocalOnly" && !string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey))
                {
                    await CloudSyncService.SyncWithCloudAsync();
                }
            }
            catch { }
            
            LoadData();
            dbSyncProgress.Visibility = Visibility.Collapsed;
        }
        
        private void NewRequest_Click(object sender, RoutedEventArgs e)
        {
            var newRequestWindow = new NewRequestWindow();
            newRequestWindow.Owner = this;
            newRequestWindow.ShowDialog();
            if (newRequestWindow.RequestNavigationToBranding)
            {
                NavigateToOrderLayout();
            }
            if (newRequestWindow.WasCreated) LoadData();
        }

        private void MemosDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (MemosDataGrid.SelectedItem is ClientApp.Models.ServiceMemo selectedMemo) OpenDetailsWindow(selectedMemo.Id);
        }

        private void MemosDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (MemosDataGrid.SelectedItem is ClientApp.Models.ServiceMemo selectedMemo)
                {
                    OpenDetailsWindow(selectedMemo.Id);
                    e.Handled = true; // Prevent default DataGrid behavior (moving to next row)
                }
            }
        }

        private void OpenDetailsWindow(int memoId)
        {
            var detailsWindow = new MemoDetailsWindow(memoId);
            detailsWindow.Owner = this;
            detailsWindow.ShowDialog();
            if (detailsWindow.NeedsBrandingNavigation)
            {
                NavigateToOrderLayout();
            }
            if (detailsWindow.NeedsRefresh) LoadData();
        }
        private void UploadCustomTemplate_Click(object sender, RoutedEventArgs e)
        {
            // Re-purposed to open designer since we want to move away from manual PDF uploads
            CustomizeTemplate_Click(sender, e);
        }

        private void TemplateZoomIn_Click(object sender, RoutedEventArgs e) => ApplyTemplateZoom(0.1);
        private void TemplateZoomOut_Click(object sender, RoutedEventArgs e) => ApplyTemplateZoom(-0.1);
        private void TemplateResetZoom_Click(object sender, RoutedEventArgs e) { TemplatePreviewScale.ScaleX = 0.5; TemplatePreviewScale.ScaleY = 0.5; UpdateTemplateZoomText(); }

        private void ApplyTemplateZoom(double delta)
        {
            double newScale = TemplatePreviewScale.ScaleX + delta;
            if (newScale >= 0.2 && newScale <= 3.0)
            {
                TemplatePreviewScale.ScaleX = newScale;
                TemplatePreviewScale.ScaleY = newScale;
                UpdateTemplateZoomText();
            }
        }

        private void UpdateTemplateZoomText() => txtTemplateZoomPercent.Text = $"{(TemplatePreviewScale.ScaleX * 100):0}%";

        private void TemplateZoomContainer_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            double scale = TemplatePreviewScale.ScaleX * e.DeltaManipulation.Scale.X;
            if (scale >= 0.2 && scale <= 3.0)
            {
                TemplatePreviewScale.ScaleX = scale;
                TemplatePreviewScale.ScaleY = scale;
                UpdateTemplateZoomText();
            }
            e.Handled = true;
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && TemplatePreviewMode.Visibility == Visibility.Visible)
            {
                ApplyTemplateZoom(e.Delta > 0 ? 0.1 : -0.1);
                e.Handled = true;
            }
            base.OnPreviewMouseWheel(e);
        }

        private void HorizontalScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta != 0)
            {
                // Bubble the event to parent ScrollViewer
                var sv = sender as ScrollViewer;
                if (sv != null)
                {
                    var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                    eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                    eventArg.Source = sender;
                    
                    // Find the parent ScrollViewer
                    var parent = DesignerMainScroller;
                    parent.RaiseEvent(eventArg);
                    e.Handled = true;
                }
            }
        }

        private void SettingsScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta != 0 && sender is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        private Point _lastMousePosition;
        private bool _isPanning = false;

        private void TemplatePreviewScroller_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (TemplatePreviewScale.ScaleX > 0.51)
            {
                _lastMousePosition = e.GetPosition(TemplatePreviewScroller);
                _isPanning = true;
                TemplatePreviewScroller.Cursor = Cursors.Hand;
                TemplatePreviewScroller.CaptureMouse();
            }
        }

        private void TemplatePreviewScroller_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point currentPosition = e.GetPosition(TemplatePreviewScroller);
                double deltaX = _lastMousePosition.X - currentPosition.X;
                double deltaY = _lastMousePosition.Y - currentPosition.Y;

                TemplatePreviewScroller.ScrollToHorizontalOffset(TemplatePreviewScroller.HorizontalOffset + deltaX);
                TemplatePreviewScroller.ScrollToVerticalOffset(TemplatePreviewScroller.VerticalOffset + deltaY);

                _lastMousePosition = currentPosition;
            }
        }

        private void TemplatePreviewScroller_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                TemplatePreviewScroller.ReleaseMouseCapture();
                TemplatePreviewScroller.Cursor = null;
            }
        }
        private void BrowseCloudTemplates_Click(object sender, RoutedEventArgs e)
        {
            var win = new BrowseCloudTemplatesWindow();
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                RefreshCustomTemplates();
            }
        }



        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            StopCloudPolling();
            if (UpdateManager.Instance.IsDownloading)
            {
                e.Cancel = true;
                this.Hide();

                try
                {
                    var notifyIcon = new System.Windows.Forms.NotifyIcon
                    {
                        Icon = System.Drawing.SystemIcons.Information,
                        Visible = true,
                        Text = "Job Order Generator"
                    };

                    notifyIcon.ShowBalloonTip(3000, 
                        "Update in Progress", 
                        "The Job Order Generator is being updated in the background. It will exit automatically when complete.", 
                        System.Windows.Forms.ToolTipIcon.Info);

                    UpdateManager.Instance.DownloadCompleted += (version) =>
                    {
                        notifyIcon.Visible = false;
                        notifyIcon.Dispose();
                        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                    };

                    UpdateManager.Instance.DownloadFailed += (error) =>
                    {
                        notifyIcon.Visible = false;
                        notifyIcon.Dispose();
                        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                    };
                }
                catch
                {
                    Application.Current.Shutdown();
                }
            }
        }

        private void StartCloudPolling()
        {
            if (_cloudPollCts != null) return; // already running

            _cloudPollCts = new System.Threading.CancellationTokenSource();
            var token = _cloudPollCts.Token;

            System.Threading.Tasks.Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(1500, token); // every 1.5 seconds for instant feel
                        if (token.IsCancellationRequested) break;

                        if (SettingsManager.Default.SyncMode != "LocalOnly" && !string.IsNullOrEmpty(SettingsManager.Default.SubscriptionKey))
                        {
                            await CloudSyncService.SyncWithCloudAsync();

                            // Dispatch UI refresh on main thread
                            await System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                LoadData();
                            }));
                        }
                    }
                    catch (System.Threading.Tasks.TaskCanceledException) { break; }
                    catch { }
                }
            });
        }

        private void StopCloudPolling()
        {
            _cloudPollCts?.Cancel();
            _cloudPollCts?.Dispose();
            _cloudPollCts = null;
        }

        private UpdateInfo? _liveDetectedUpdate;

        private void UpdateManager_LiveUpdateDetected(UpdateInfo update)
        {
            Dispatcher.Invoke(() =>
            {
                if (SettingsManager.Default.SkipUpdateVersion == update.Version) return;
                _liveDetectedUpdate = update;

                if (update.IsCompulsory)
                {
                    txtLiveUpdateTitle.Text = "Mandatory System Update Available";
                    txtLiveUpdateDesc.Text = $"Version {update.Version} is a compulsory update and will be applied when you restart the application.";
                    btnLiveUpdateNow.Visibility = Visibility.Collapsed;
                    btnLiveUpdateLater.Content = "Got It";
                }
                else
                {
                    txtLiveUpdateTitle.Text = "Software Update Available";
                    txtLiveUpdateDesc.Text = $"A new update (v{update.Version}) is available. Restart app to update.";
                    btnLiveUpdateNow.Visibility = Visibility.Visible;
                    btnLiveUpdateNow.Content = "Update Now";
                    btnLiveUpdateLater.Content = "Update Later";
                }

                borderLiveUpdateBanner.Visibility = Visibility.Visible;
            });
        }

        private void LiveUpdateLater_Click(object sender, RoutedEventArgs e)
        {
            if (_liveDetectedUpdate != null && !_liveDetectedUpdate.IsCompulsory)
            {
                SettingsManager.Default.SkipUpdateVersion = _liveDetectedUpdate.Version;
                SettingsManager.Save();
            }
            borderLiveUpdateBanner.Visibility = Visibility.Collapsed;
        }

        private void LiveUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            borderLiveUpdateBanner.Visibility = Visibility.Collapsed;
            if (_liveDetectedUpdate != null)
            {
                var win = new UpdateNotificationWindow(_liveDetectedUpdate);
                win.Owner = this;
                win.ShowDialog();
            }
        }
    }
}
