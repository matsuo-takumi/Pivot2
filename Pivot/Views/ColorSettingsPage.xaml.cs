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
            try
            {
                ViewModel = new ColorSettingsViewModel(settings, textColorManager);
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
        }

        private void ColorSwatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
        }

        private void TemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string templateTag &&
                Enum.TryParse<TextColorTemplate>(templateTag, out var template))
            {
                ViewModel.SelectedTemplate = template;
            }
        }
    }
}


