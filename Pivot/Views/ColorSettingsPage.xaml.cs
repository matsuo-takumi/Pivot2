using Pivot.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Pivot.Services;
using Pivot.ViewModels;
using Pivot.Converters;
using System;
using System.Threading.Tasks;
using Windows.UI;

namespace Pivot.Views
{
    public sealed partial class ColorSettingsPage : Page
    {
        public ColorSettingsViewModel ViewModel { get; }
        public CodeModule.ViewModels.CodeSettingsViewModel CodeViewModel { get; }
        
        private readonly ILogger<ColorSettingsPage>? _logger;
        private bool _isPresetsLoading;

        public ColorSettingsPage()
        {
            _logger = App.Current.Services.GetService<ILogger<ColorSettingsPage>>();
            
            // Initialize XAML first to allow UI to render immediately
            this.InitializeComponent();
            
            // Get services
            var themeSettings = App.Current.Services.GetRequiredService<ThemeSettingsService>();
            var textColorManager = App.Current.Services.GetRequiredService<ITextColorResourceManager>();
            var presetService = App.Current.Services.GetRequiredService<IPresetService<Models.TextColorPresetData>>();
            var presetLogger = App.Current.Services.GetService<ILogger<TextColorPresetViewModel>>();
            
            try
            {
                // Create ViewModel immediately - entries will be loaded asynchronously after page loads
                ViewModel = new ColorSettingsViewModel(themeSettings, textColorManager, presetService, presetLogger);
                CodeViewModel = App.Current.Services.GetRequiredService<CodeModule.ViewModels.CodeSettingsViewModel>();
                this.DataContext = ViewModel;
                _logger?.LogInformation("ColorSettingsPage initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize ColorSettingsPage.");
                throw;
            }
            
            // Load entries and presets asynchronously after page loads
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe to avoid multiple calls
            Loaded -= OnPageLoaded;
            
            // Load entries and presets asynchronously to avoid blocking UI
            await Task.WhenAll(
                ViewModel.LoadEntriesAsync(),
                LoadPresetsAsync(),
                CodeViewModel.PresetViewModel.LoadPresetsAsync()
            );
            
        }



        private async Task LoadPresetsAsync()
        {
            if (_isPresetsLoading) return;
            _isPresetsLoading = true;
            
            try
            {
                // Load presets asynchronously without blocking UI thread
                await ViewModel.PresetViewModel.LoadPresetsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load presets on page load.");
            }
            finally
            {
                _isPresetsLoading = false;
            }
        }

        private void ColorSwatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TextColorSettingViewModel entry)
            {
                // Create ColorPicker lazily when flyout is opened (not in XAML)
                var flyout = new Flyout
                {
                    Placement = FlyoutPlacementMode.Bottom
                };

                var colorPicker = new Microsoft.UI.Xaml.Controls.ColorPicker
                {
                    Color = entry.SelectedColor,
                    ColorSpectrumShape = Microsoft.UI.Xaml.Controls.ColorSpectrumShape.Ring,
                    IsMoreButtonVisible = false,
                    IsColorSliderVisible = true,
                    IsColorChannelTextInputVisible = false,
                    IsHexInputVisible = false,
                    IsAlphaEnabled = false,
                    IsAlphaSliderVisible = false,
                    IsAlphaTextInputVisible = false
                };

                // Handle color changes
                colorPicker.ColorChanged += (s, args) =>
                {
                    entry.SelectedColor = args.NewColor;
                };

                flyout.Content = colorPicker;
                flyout.ShowAt(button);
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

        private async void PresetItem_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Models.Preset<Models.TextColorPresetData> preset)
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
                var themeSettings = App.Current.Services.GetRequiredService<ThemeSettingsService>();
                dialog.RequestedTheme = themeSettings.AppTheme;
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

        // Code Settings Color Picker Handler
        private void CodeColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (CodeViewModel.IsCustomColor)
            {
                var color = args.NewColor;
                // Simple manual hex string creation
                var hex = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                CodeViewModel.UpdateColorCommand.Execute(hex);
            }
        }

        private void CodeColorSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CodeModule.ViewModels.CodeColorSettingViewModel entry)
            {
                var flyout = new Flyout
                {
                    Placement = FlyoutPlacementMode.Bottom
                };

                var colorPicker = new Microsoft.UI.Xaml.Controls.ColorPicker
                {
                    Color = entry.PreviewBrush is SolidColorBrush solid ? solid.Color : Colors.White,
                    ColorSpectrumShape = Microsoft.UI.Xaml.Controls.ColorSpectrumShape.Ring,
                    IsMoreButtonVisible = false,
                    IsColorSliderVisible = true,
                    IsColorChannelTextInputVisible = false,
                    IsHexInputVisible = false,
                    IsAlphaEnabled = true,
                    IsAlphaSliderVisible = true,
                    IsAlphaTextInputVisible = true
                };

                colorPicker.ColorChanged += (s, args) =>
                {
                    entry.UpdateColorFromPicker(args.NewColor);
                };

                flyout.Content = colorPicker;
                flyout.ShowAt(button);
            }
        }
        // Code Preset Event Handlers
        private async void SaveCodePresetButton_Click(object sender, RoutedEventArgs e)
        {
            // プリセット名を入力するダイアログを表示
            var nameTextBox = new TextBox
            {
                Header = "Preset Name",
                PlaceholderText = "Preset name...",
                Text = $"Code Preset {DateTime.Now:yyyy-MM-dd HH:mm}"
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Enter a name for this code color preset:",
                Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 8)
            });
            panel.Children.Add(nameTextBox);

            var dialog = new ContentDialog
            {
                Title = "Save Code Preset",
                Content = panel,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            // Apply app theme to dialog
            try
            {
                var themeSettings = App.Current.Services.GetRequiredService<ThemeSettingsService>();
                dialog.RequestedTheme = themeSettings.AppTheme;
            }
            catch { }

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var presetName = nameTextBox.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(presetName))
                {
                    await CodeViewModel.PresetViewModel.SaveCurrentAsPresetAsync(presetName);
                }
            }
        }

        private async void CodePresetItem_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Models.Preset<Models.CodeColorPresetData> preset)
            {
                await CodeViewModel.PresetViewModel.ApplyPresetAsync(preset);
            }
        }

        private async void DeleteCodePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Models.Preset<Models.CodeColorPresetData> preset)
            {
                await CodeViewModel.PresetViewModel.DeletePresetAsync(preset);
            }
        }

        private async void ApplySelectedCodePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (CodeViewModel.PresetViewModel.SelectedPreset != null)
            {
                await CodeViewModel.PresetViewModel.ApplyPresetAsync(CodeViewModel.PresetViewModel.SelectedPreset);
            }
        }

        private async void DeleteSelectedCodePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (CodeViewModel.PresetViewModel.SelectedPreset != null)
            {
                await CodeViewModel.PresetViewModel.DeletePresetAsync(CodeViewModel.PresetViewModel.SelectedPreset);
            }
        }
    }
}

