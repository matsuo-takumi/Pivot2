using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Services;
using System.Linq;
using System;
using Pivot.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Pivot.Views
{
    public sealed partial class AssetSettingsPage : Page
    {
        private readonly SettingsService? _settings;

        public AssetSettingsPage()
        {
            this.InitializeComponent();
            _settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
            LoadFilters();
        }

        private void LoadFilters()
        {
            try
            {
                var settings = _settings?.GetUserSettings();
                if (settings != null)
                {
                    // expose a small view model for binding convenience
                    var visible = _settings.GetVisibleFiltersForTab("Asset");
                    var viewItems = settings.AssetFilters.Select(f => new AssetFilterViewItem
                    {
                        Id = f.Id,
                        Name = f.Name,
                        AllowedExtensions = f.AllowedExtensions,
                        Visible = visible != null && visible.Contains(f.Id)
                    }).ToList();

                    FiltersList.ItemsSource = viewItems;
                }
            }
            catch { }
        }

        private async void FilterVisible_Toggled(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleSwitch ts) || !(ts.Tag is Guid id) || _settings == null) return;
            try
            {
                var current = _settings.GetVisibleFiltersForTab("Asset") ?? new List<Guid>();
                if (ts.IsOn)
                {
                    if (!current.Contains(id)) current.Add(id);
                }
                else
                {
                    current.Remove(id);
                }
                await _settings.SetVisibleFiltersForTabAsync("Asset", current);
                LoadFilters();
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
                Title = "Add Filter",
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                Content = panel
            };

            // Ensure dialog has XamlRoot when shown from a Page
            dialog.XamlRoot = this.XamlRoot;
            // Apply app theme to dialog
            try { dialog.RequestedTheme = _settings?.GetTheme() ?? ElementTheme.Default; } catch { }

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    var name = nameBox.Text?.Trim() ?? string.Empty;
                    var exts = (extBox.Text ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 0).Select(s => s.StartsWith('.') ? s : "." + s).Distinct().ToList();

                    if (!string.IsNullOrWhiteSpace(name) && _settings != null)
                    {
                        var user = _settings.GetUserSettings();
                        var list = user.AssetFilters;
                        var nf = new CustomFilter { Name = name, AllowedExtensions = exts };
                        list.Add(nf);
                        await _settings.SetAssetFiltersAsync(list);
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
                var target = user.AssetFilters.FirstOrDefault(f => f.Id == id);
                if (target == null) return;

                var nameBox = new TextBox { Header = "Name", Text = target.Name };
                var extBox = new TextBox { Header = "Extensions (comma separated)", Text = string.Join(",", target.AllowedExtensions) };
                var panel = new StackPanel();
                panel.Children.Add(nameBox);
                panel.Children.Add(extBox);

                var dialog = new ContentDialog()
                {
                    Title = "Edit Filter",
                    PrimaryButtonText = "Save",
                    CloseButtonText = "Cancel",
                    Content = panel
                };

                // Ensure dialog has XamlRoot when shown from a Page
                dialog.XamlRoot = this.XamlRoot;
                // Apply app theme to dialog
                try { dialog.RequestedTheme = _settings?.GetTheme() ?? ElementTheme.Default; } catch { }

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    try
                    {
                        var name = nameBox.Text?.Trim() ?? string.Empty;
                        var exts = (extBox.Text ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 0).Select(s => s.StartsWith('.') ? s : "." + s).Distinct().ToList();
                        target.Name = name;
                        target.AllowedExtensions = exts;
                        await _settings.SetAssetFiltersAsync(user.AssetFilters);
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
                var target = user.AssetFilters.FirstOrDefault(f => f.Id == id);
                if (target == null) return;
                if (target.IsBuiltIn) return; // do not delete built-in
                user.AssetFilters.Remove(target);
                await _settings.SetAssetFiltersAsync(user.AssetFilters);
                LoadFilters();
            }
        }

        private async void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            var defaults = (typeof(Pivot.Services.SettingsService).GetMethod("GetDefaultAssetFilters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null);
            try
            {
                var defList = _settings.GetUserSettings();
                // use SettingsService helper by re-setting defaults via API if available
                await _settings.SetAssetFiltersAsync(new List<CustomFilter>
                {
                    new CustomFilter { Name = "3D", AllowedExtensions = new List<string>{ ".obj",".fbx",".gltf",".glb",".dae" }, IsBuiltIn=true },
                    new CustomFilter { Name = "Images", AllowedExtensions = new List<string>{ ".png",".jpg",".jpeg",".bmp",".gif",".webp",".tga",".tif",".tiff" }, IsBuiltIn=true },
                    new CustomFilter { Name = "Video", AllowedExtensions = new List<string>{ ".mp4",".mov",".avi",".mkv",".webm" }, IsBuiltIn=true },
                    new CustomFilter { Name = "Audio", AllowedExtensions = new List<string>{ ".mp3",".wav",".ogg",".flac",".aac" }, IsBuiltIn=true },
                    new CustomFilter { Name = "Other", AllowedExtensions = new List<string>(), IsBuiltIn=true }
                });
                LoadFilters();
            }
            catch { }
        }

        private class AssetFilterViewItem
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public List<string> AllowedExtensions { get; set; } = new List<string>();
            public string AllowedExtensionsString => string.Join(", ", AllowedExtensions);
            public bool Visible { get; set; } = true;
        }
    }
}
