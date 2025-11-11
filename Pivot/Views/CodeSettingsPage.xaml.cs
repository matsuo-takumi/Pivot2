using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Services;
using System.Linq;
using Pivot.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Pivot.Views
{
    public sealed partial class CodeSettingsPage : Page
    {
        private readonly SettingsService? _settings;

        public CodeSettingsPage()
        {
            this.InitializeComponent();
            _settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
            LoadFilters();
            LoadSaveFormat();
            LoadSaveOutputDirectory();
        }
        
        private void LoadSaveOutputDirectory()
        {
            try
            {
                var dir = _settings?.GetExportOutputDirectory() ?? string.Empty;
                var box = this.FindName("OutputDirBox") as TextBox;
                if (box != null) box.Text = dir;
                var status = this.FindName("StatusText") as TextBlock;
                if (status != null) status.Text = "";
            }
            catch { }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var box = this.FindName("OutputDirBox") as TextBox;
                if (box == null) return;
                var path = box.Text?.Trim() ?? string.Empty;
                if (_settings != null)
                {
                    await _settings.SetExportOutputDirectoryAsync(path);
                    var status = this.FindName("StatusText") as TextBlock;
                    if (status != null) status.Text = "Saved.";
                }
            }
            catch (Exception ex)
            {
                var status = this.FindName("StatusText") as TextBlock;
                if (status != null) status.Text = "Failed to save: " + ex.Message;
            }
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folderPicker = new FolderPicker();
                folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
                folderPicker.FileTypeFilter.Add("*");

                var uiWindow = App.Current.MainWindow as Microsoft.UI.Xaml.Window;
                if (uiWindow == null)
                {
                    var status = this.FindName("StatusText") as TextBlock;
                    if (status != null) status.Text = "Unable to access application window.";
                    return;
                }

                var hwnd = WindowNative.GetWindowHandle(uiWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    var box = this.FindName("OutputDirBox") as TextBox;
                    if (box != null) box.Text = folder.Path;
                    // Optionally save immediately
                    if (_settings != null) await _settings.SetExportOutputDirectoryAsync(folder.Path);
                    var status = this.FindName("StatusText") as TextBlock;
                    if (status != null) status.Text = "Saved.";
                }
            }
            catch (Exception ex)
            {
                var status = this.FindName("StatusText") as TextBlock;
                if (status != null) status.Text = "Failed to pick folder: " + ex.Message;
            }
        }

        private void LoadFilters()
        {
            try
            {
                var settings = _settings?.GetUserSettings();
                if (settings != null)
                {
                    var visible = _settings!.GetVisibleFiltersForTab("Code");
                var viewItems = settings.CodeFilters.Select(f => new CodeFilterViewItem
                {
                    Id = f.Id,
                    Name = f.Name,
                    AllowedExtensions = f.AllowedExtensions ?? new List<string>(),
                    Visible = visible != null && visible.Contains(f.Id)
                }).ToList();

                    FiltersList.ItemsSource = viewItems;
                }
            }
            catch { }
        }

        private async void AddFilter_Click(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox { Header = "Name" };
            var extBox = new TextBox { Header = "Extensions (comma separated)" };

            var panel = new StackPanel();
            panel.Children.Add(nameBox);
            panel.Children.Add(extBox);

            var dialog = new ContentDialog()
            {
                Title = "Add Code Filter",
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                Content = panel
            };

            dialog.XamlRoot = this.XamlRoot;
            try { dialog.RequestedTheme = _settings?.GetTheme() ?? ElementTheme.Default; } catch { }

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    var name = nameBox.Text?.Trim() ?? string.Empty;

                    var exts = (extBox.Text ?? string.Empty)
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim().ToLowerInvariant())
                        .Where(s => s.Length > 0)
                        .Select(s => s.StartsWith('.') ? s : "." + s)
                        .Distinct()
                        .ToList();

                    if (!string.IsNullOrWhiteSpace(name) && _settings != null)
                    {
                        var user = _settings.GetUserSettings();
                        var list = user.CodeFilters;
                        var nf = new CustomFilter { Name = name, AllowedExtensions = exts };
                        list.Add(nf);
                        await _settings.SetCodeFiltersAsync(list);
                        LoadFilters();
                    }
                }
                catch { }
            }
        }

        private async void EditFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is Guid id && _settings != null)
            {
                var user = _settings.GetUserSettings();
                var target = user.CodeFilters.FirstOrDefault(f => f.Id == id);
                if (target == null) return;

                var nameBox = new TextBox { Header = "Name", Text = target.Name };
                var extBox = new TextBox { Header = "Extensions (comma separated)", Text = string.Join(",", target.AllowedExtensions ?? new List<string>()) };
                var panel = new StackPanel();
                panel.Children.Add(nameBox);
                panel.Children.Add(extBox);

                var dialog = new ContentDialog()
                {
                    Title = "Edit Code Filter",
                    PrimaryButtonText = "Save",
                    CloseButtonText = "Cancel",
                    Content = panel
                };

                dialog.XamlRoot = this.XamlRoot;
                try { dialog.RequestedTheme = _settings?.GetTheme() ?? ElementTheme.Default; } catch { }

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    try
                    {
                        var name = nameBox.Text?.Trim() ?? string.Empty;
                        var exts = (extBox.Text ?? string.Empty)
                            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim().ToLowerInvariant())
                            .Where(s => s.Length > 0)
                            .Select(s => s.StartsWith('.') ? s : "." + s)
                            .Distinct()
                            .ToList();
                        target.Name = name;
                        target.AllowedExtensions = exts;
                        await _settings!.SetCodeFiltersAsync(user.CodeFilters);
                        LoadFilters();
                    }
                    catch { }
                }
            }
        }

        private async void DeleteFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is Guid id && _settings != null)
            {
                var user = _settings.GetUserSettings();
                var target = user.CodeFilters.FirstOrDefault(f => f.Id == id);
                if (target == null) return;
                // allow deleting built-in as requested
                user.CodeFilters.Remove(target);
                await _settings.SetCodeFiltersAsync(user.CodeFilters);
                LoadFilters();
            }
        }

        private async void FilterVisible_Toggled(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleSwitch ts) || !(ts.Tag is Guid id) || _settings == null) return;
            try
            {
                var current = _settings.GetVisibleFiltersForTab("Code") ?? new List<Guid>();
                if (ts.IsOn)
                {
                    if (!current.Contains(id)) current.Add(id);
                }
                else
                {
                    current.Remove(id);
                }
                await _settings.SetVisibleFiltersForTabAsync("Code", current);
                LoadFilters();
            }
            catch { }
        }

        private async void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            try
            {
                await _settings.SetCodeFiltersAsync(new List<CustomFilter>
                {
                    new CustomFilter { Name = "C#", IsBuiltIn=true },
                    new CustomFilter { Name = "Python", IsBuiltIn=true },
                    new CustomFilter { Name = "JavaScript", IsBuiltIn=true },
                    new CustomFilter { Name = "HTML", IsBuiltIn=true },
                    new CustomFilter { Name = "CSS", IsBuiltIn=true },
                    new CustomFilter { Name = "SQL", IsBuiltIn=true },
                    new CustomFilter { Name = "Markdown", IsBuiltIn=true },
                    new CustomFilter { Name = "Other", IsBuiltIn=true }
                });
                LoadFilters();
            }
            catch { }
        }

        private class CodeFilterViewItem
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public List<string> AllowedExtensions { get; set; } = new List<string>();
            public string AllowedExtensionsString => string.Join(", ", AllowedExtensions);
            public bool Visible { get; set; } = true;
        }

        // Code categories are deprecated and removed from Preferences.

        private void LoadSaveFormat()
        {
            try
            {
                var fmt = _settings?.GetCodeExportFormat() ?? Pivot.Models.CodeExportFormat.Json;
                var combo = this.FindName("SaveFormatCombo") as ComboBox;
                if (combo != null)
                {
                    for (int i = 0; i < combo.Items.Count; i++)
                    {
                        if (combo.Items[i] is ComboBoxItem cbi && string.Equals(cbi.Tag?.ToString(), fmt.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            combo.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private async void SaveFormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_settings == null) return;
                if (!(sender is ComboBox cb)) return;
                var sel = cb.SelectedItem as ComboBoxItem;
                var tag = sel?.Tag?.ToString() ?? "Json";
                if (!Enum.TryParse<Pivot.Models.CodeExportFormat>(tag, out var fmt)) fmt = Pivot.Models.CodeExportFormat.Json;
                await _settings.SetCodeExportFormatAsync(fmt);
            }
            catch { }
        }
    }
}


