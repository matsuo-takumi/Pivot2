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
using Pivot.CodeModule.Services;

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
                    Visible = visible != null && visible.Contains(f.Id)
                }).ToList();

                    FiltersList.ItemsSource = viewItems;
                }
            }
            catch { }
        }

        private async void AddFilter_Click(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox { Header = "Tag Name", PlaceholderText = "Enter tag name (e.g., C#, Python, JavaScript)" };

            var panel = new StackPanel();
            panel.Children.Add(nameBox);

            var dialog = new ContentDialog()
            {
                Title = "Add Code Filter Tag",
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

                    if (!string.IsNullOrWhiteSpace(name) && _settings != null)
                    {
                        var user = _settings.GetUserSettings();
                        var list = user.CodeFilters;
                        // Code filters don't use extensions - only tag names
                        var nf = new CustomFilter { Name = name, AllowedExtensions = new List<string>() };
                        list.Add(nf);
                        await _settings.SetCodeFiltersAsync(list);
                        LoadFilters();
                    }
                }
                catch (Exception ex)
                {
                    // Show error to user
                    var errorDialog = new ContentDialog()
                    {
                        Title = "Error",
                        Content = $"Failed to add filter: {ex.Message}",
                        CloseButtonText = "OK"
                    };
                    errorDialog.XamlRoot = this.XamlRoot;
                    try { await errorDialog.ShowAsync(); } catch { }
                }
            }
        }

        private async void EditFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is Guid id && _settings != null)
            {
                var user = _settings.GetUserSettings();
                var target = user.CodeFilters.FirstOrDefault(f => f.Id == id);
                if (target == null) return;

                var nameBox = new TextBox { Header = "Tag Name", Text = target.Name, PlaceholderText = "Enter tag name" };
                var panel = new StackPanel();
                panel.Children.Add(nameBox);

                var dialog = new ContentDialog()
                {
                    Title = "Edit Code Filter Tag",
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
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            target.Name = name;
                            // Code filters don't use extensions - keep empty
                            target.AllowedExtensions = new List<string>();
                            await _settings!.SetCodeFiltersAsync(user.CodeFilters);
                            LoadFilters();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Show error to user
                        var errorDialog = new ContentDialog()
                        {
                            Title = "Error",
                            Content = $"Failed to edit filter: {ex.Message}",
                            CloseButtonText = "OK"
                        };
                        errorDialog.XamlRoot = this.XamlRoot;
                        try { await errorDialog.ShowAsync(); } catch { }
                    }
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
                
                var tagName = target.Name;
                
                // allow deleting built-in as requested
                user.CodeFilters.Remove(target);
                await _settings.SetCodeFiltersAsync(user.CodeFilters);
                
                // Remove tag from all snippets
                try
                {
                    var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                    if (repo != null && !string.IsNullOrWhiteSpace(tagName))
                    {
                        repo.RemoveTagFromAllSnippets(tagName);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DeleteFilter_Click: Failed to remove tag from snippets: {ex.Message}");
                }
                
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
                    new CustomFilter { Name = "C#", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                    new CustomFilter { Name = "Python", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                    new CustomFilter { Name = "JavaScript", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                    new CustomFilter { Name = "HTML", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                    new CustomFilter { Name = "CSS", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                    new CustomFilter { Name = "SQL", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                    new CustomFilter { Name = "Markdown", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                    new CustomFilter { Name = "Other", IsBuiltIn=true, AllowedExtensions = new List<string>() }
                });
                LoadFilters();
            }
            catch { }
        }

        private class CodeFilterViewItem
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
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


