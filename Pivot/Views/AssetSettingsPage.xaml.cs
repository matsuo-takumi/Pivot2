using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace Pivot.Views
{
    public sealed partial class AssetSettingsPage : Page
    {
        public AssetSettingsViewModel ViewModel { get; }

        public AssetSettingsPage()
        {
            ViewModel = App.Current.Services.GetRequiredService<AssetSettingsViewModel>();
            this.InitializeComponent();
        }

        private async void AddMaterialPreset_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "New Material Preset",
                Content = new TextBox { PlaceholderText = "Preset Name" },
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var textBox = dialog.Content as TextBox;
                var name = textBox?.Text?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    await ViewModel.AddMaterialPresetCommand.ExecuteAsync(name);
                }
            }
        }

        private async void DeleteMaterialPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Pivot.Models.MaterialPreset preset)
            {
                await ViewModel.DeleteMaterialPresetCommand.ExecuteAsync(preset);
            }
        }

        private async void AddLightingPreset_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "New Lighting Preset",
                Content = new TextBox { PlaceholderText = "Preset Name" },
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var textBox = dialog.Content as TextBox;
                var name = textBox?.Text?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    await ViewModel.AddLightingPresetCommand.ExecuteAsync(name);
                }
            }
        }

        private async void DeleteLightingPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Pivot.Models.LightingPreset preset)
            {
                await ViewModel.DeleteLightingPresetCommand.ExecuteAsync(preset);
            }
        }

        private void MaterialPresetsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Preview selected material preset (optional - future implementation)
        }

        private void LightingPresetsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Preview selected lighting preset (optional - future implementation)
        }
    }
}
