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
            this.Loaded += CodeSettingsPage_Loaded;
            
            LoadSaveFormat();
            LoadSaveOutputDirectory();
        }

        private void CodeSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize common filter settings view
            var logger = App.Current.Services.GetService(typeof(Microsoft.Extensions.Logging.ILogger<ViewModels.FilterSettingsViewModel>)) as Microsoft.Extensions.Logging.ILogger<ViewModels.FilterSettingsViewModel>;
            
            if (_settings != null)
            {
                var filterView = this.FindName("FilterSettingsView") as FilterSettingsView;
                if (filterView != null)
                {
                    filterView.ViewModel = new ViewModels.FilterSettingsViewModel(
                        _settings,
                        Models.FilterType.Code,
                        "Code",
                        GetDefaultCodeFilters,
                        logger);
                }
            }
        }

        private static List<CustomFilter> GetDefaultCodeFilters()
        {
            return new List<CustomFilter>
            {
                new CustomFilter { Name = "C#", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                new CustomFilter { Name = "Python", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                new CustomFilter { Name = "JavaScript", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                new CustomFilter { Name = "HTML", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                new CustomFilter { Name = "CSS", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                new CustomFilter { Name = "SQL", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                new CustomFilter { Name = "Markdown", IsBuiltIn=true, AllowedExtensions = new List<string>() },
                new CustomFilter { Name = "Other", IsBuiltIn=true, AllowedExtensions = new List<string>() }
            };
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


