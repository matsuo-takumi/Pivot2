using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Services;
using System;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Pivot.Views
{
    public sealed partial class ExportPage : Page
    {
        private readonly SettingsService? _settings;

        public ExportPage()
        {
            this.InitializeComponent();
            try
            {
                _settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                if (_settings != null)
                {
                    OutputDirBox.Text = _settings.GetExportOutputDirectory() ?? string.Empty;
                    StatusText.Text = "";
                }
            }
            catch { }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = OutputDirBox.Text?.Trim() ?? string.Empty;
                if (_settings != null)
                {
                    await _settings.SetExportOutputDirectoryAsync(path);
                    StatusText.Text = "Saved.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to save: " + ex.Message;
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
                    StatusText.Text = "Unable to access application window.";
                    return;
                }

                var hwnd = WindowNative.GetWindowHandle(uiWindow);
                InitializeWithWindow.Initialize(folderPicker, hwnd);

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    var path = folder.Path ?? string.Empty;
                    OutputDirBox.Text = path;
                    StatusText.Text = "Selected: " + path;
                    // Optionally save immediately
                    try { if (_settings != null) await _settings.SetExportOutputDirectoryAsync(path); } catch { }
                }
                else
                {
                    StatusText.Text = "No folder selected.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Folder picker error: " + ex.Message;
            }
        }
    }
}


