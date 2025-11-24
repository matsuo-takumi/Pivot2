using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Pivot.Services;
using Pivot.ViewModels;
using System;

namespace Pivot.Views
{
    public sealed partial class ColorSettingsPage : Page
    {
        public ColorSettingsViewModel ViewModel { get; }
        private readonly ILogger<ColorSettingsPage>? _logger;

        public ColorSettingsPage()
        {
            _logger = App.Current.Services.GetService<ILogger<ColorSettingsPage>>();
            var settings = App.Current.Services.GetRequiredService<SettingsService>();
            var textColorManager = App.Current.Services.GetRequiredService<ITextColorResourceManager>();
            var presetService = App.Current.Services.GetRequiredService<IPresetService<Models.TextColorPresetData>>();
            var presetLogger = App.Current.Services.GetService<ILogger<TextColorPresetViewModel>>();
            try
            {
                ViewModel = new ColorSettingsViewModel(settings, textColorManager, presetService, presetLogger);
                _logger?.LogInformation("ColorSettingsPage initialized successfully with {EntryCount} text color entries.", ViewModel.TextColorSettings.Entries.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize ColorSettingsPage.");
                throw;
            }
            this.InitializeComponent();
            this.DataContext = ViewModel;
            _logger?.LogInformation("ColorSettingsPage DataContext assigned.");
            
            // プリセットを読み込む
            Loaded += async (s, e) =>
            {
                try
                {
                    await ViewModel.PresetViewModel.LoadPresetsAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load presets on page load.");
                }
            };
        }

        private void ColorSwatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
        }

        private async void ResetToDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ViewModel.ResetToDefaultsAsync();
                _logger?.LogInformation("Text color settings reset to defaults.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to reset text color settings to defaults.");
            }
        }

        private async void ApplyPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Models.Preset<Models.TextColorPresetData> preset)
            {
                await ViewModel.PresetViewModel.ApplyPresetAsync(preset);
            }
        }

        private async void DeletePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Models.Preset<Models.TextColorPresetData> preset)
            {
                await ViewModel.PresetViewModel.DeletePresetAsync(preset);
            }
        }

        private async void ApplySelectedPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.PresetViewModel.SelectedPreset != null)
            {
                await ViewModel.PresetViewModel.ApplyPresetAsync(ViewModel.PresetViewModel.SelectedPreset);
            }
        }

        private async void DeleteSelectedPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.PresetViewModel.SelectedPreset != null)
            {
                await ViewModel.PresetViewModel.DeletePresetAsync(ViewModel.PresetViewModel.SelectedPreset);
            }
        }

        private async void SavePresetButton_Click(object sender, RoutedEventArgs e)
        {
            // プリセット名を入力するダイアログを表示
            var nameTextBox = new TextBox
            {
                Header = "Preset Name",
                PlaceholderText = "Preset name...",
                Text = $"Preset {DateTime.Now:yyyy-MM-dd HH:mm}"
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Enter a name for this preset:",
                Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 8)
            });
            panel.Children.Add(nameTextBox);

            var dialog = new ContentDialog
            {
                Title = "Save Preset",
                Content = panel,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            // Apply app theme to dialog
            try
            {
                var settings = App.Current.Services.GetRequiredService<SettingsService>();
                dialog.RequestedTheme = settings.GetTheme();
            }
            catch { }

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var presetName = nameTextBox.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(presetName))
                {
                    await ViewModel.PresetViewModel.SaveCurrentAsPresetAsync(presetName);
                }
            }
        }
    }
}


