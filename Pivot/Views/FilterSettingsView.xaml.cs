using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Pivot.Views
{
    public sealed partial class FilterSettingsView : UserControl
    {
        public FilterSettingsViewModel ViewModel
        {
            get => (FilterSettingsViewModel)DataContext;
            set => DataContext = value;
        }

        public FilterSettingsView()
        {
            InitializeComponent();
        }

        private async void AddFilter_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var nameBox = new TextBox 
            { 
                Header = ViewModel.ShowExtensions ? "Name" : "Tag Name",
                PlaceholderText = ViewModel.ShowExtensions ? "Enter filter name" : "Enter tag name"
            };
            var extBox = new TextBox 
            { 
                Header = "Extensions (comma separated, e.g., .png, .jpg)",
                Visibility = ViewModel.ShowExtensions ? Visibility.Visible : Visibility.Collapsed
            };

            var panel = new StackPanel();
            panel.Children.Add(nameBox);
            if (ViewModel.ShowExtensions)
            {
                panel.Children.Add(extBox);
            }

            var dialog = new ContentDialog()
            {
                Title = "Add Filter",
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                Content = panel,
                XamlRoot = this.XamlRoot
            };

            // Apply app theme to dialog
            try
            { 
                var themeSettings = App.Current.Services.GetService<Services.ThemeSettingsService>();
                if (themeSettings != null)
                {
                    dialog.RequestedTheme = themeSettings.AppTheme;
                }
            } 
            catch { }

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    var name = nameBox.Text?.Trim() ?? string.Empty;
                    var exts = new List<string>();
                    
                    if (ViewModel.ShowExtensions && !string.IsNullOrWhiteSpace(extBox.Text))
                    {
                        exts = extBox.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim().ToLowerInvariant())
                            .Where(s => s.Length > 0)
                            .Select(s => s.StartsWith('.') ? s : "." + s)
                            .Distinct()
                            .ToList();
                    }

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        await ViewModel.AddFilterAsync(name, exts);
                    }
                }
                catch { }
            }
        }

        private async void EditFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is FilterSettingsViewModel.FilterItemViewModel item && ViewModel != null)
            {
                var nameBox = new TextBox { Header = "Name", Text = item.Name };
                var extBox = new TextBox 
                { 
                    Header = "Extensions (comma separated)", 
                    Text = string.Join(", ", item.AllowedExtensions),
                    Visibility = ViewModel.ShowExtensions ? Visibility.Visible : Visibility.Collapsed
                };

                var panel = new StackPanel();
                panel.Children.Add(nameBox);
                panel.Children.Add(extBox);

                var dialog = new ContentDialog()
                {
                    Title = "Edit Filter",
                    PrimaryButtonText = "Save",
                    CloseButtonText = "Cancel",
                    Content = panel,
                    XamlRoot = this.XamlRoot
                };

                // Apply app theme to dialog
                try 
                { 
                    var themeSettings = App.Current.Services.GetService<Services.ThemeSettingsService>();
                    dialog.RequestedTheme = themeSettings?.AppTheme ?? Microsoft.UI.Xaml.ElementTheme.Default;
                } 
                catch { }

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    try
                    {
                        var name = nameBox.Text?.Trim() ?? string.Empty;
                        var exts = new List<string>();
                        
                        if (ViewModel.ShowExtensions && !string.IsNullOrWhiteSpace(extBox.Text))
                        {
                            exts = extBox.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim().ToLowerInvariant())
                                .Where(s => s.Length > 0)
                                .Select(s => s.StartsWith('.') ? s : "." + s)
                                .Distinct()
                                .ToList();
                        }

                        await ViewModel.EditFilterCommand.ExecuteAsync((item.Id, name, exts));
                        ViewModel.LoadFilters(); // Reload to refresh UI
                    }
                    catch { }
                }
            }
        }

        private async void DeleteFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is FilterSettingsViewModel.FilterItemViewModel item && ViewModel != null)
            {
                await ViewModel.DeleteFilterCommand.ExecuteAsync(item.Id);
            }
        }

        private async void ToggleVisibility_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ts && ts.Tag is FilterSettingsViewModel.FilterItemViewModel item && ViewModel != null)
            {
                await ViewModel.ToggleVisibilityCommand.ExecuteAsync(item);
            }
        }
    }
}

