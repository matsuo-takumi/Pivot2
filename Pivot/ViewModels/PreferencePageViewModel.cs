using CommunityToolkit.Mvvm.ComponentModel;
using Pivot.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace Pivot.ViewModels
{
    public partial class PreferencePageViewModel : ObservableObject
    {
        private readonly ILogger<PreferencePageViewModel>? _logger;

        [ObservableProperty]
        private object? currentContent;

        [ObservableProperty]
        private string selectedMenuTag = "Directories";

        public PreferencePageViewModel()
        {
            _logger = ResolveLogger();
            SelectMenuItem("Directories");
        }

        public void SelectMenuItem(string tag)
        {
            _logger?.LogInformation("Preference tab switch requested: {Tag}", tag);
            SelectedMenuTag = tag;

            // Create appropriate content based on tag
            var content = tag switch
            {
                "Asset" => new Views.AssetSettingsPage(),
                "Image" => CreateImageSettingsPage(),
                "Directories" => new DirectoryPage(),
                "Theme" => new ThemePage(),
                "Code" => CreateCodeSettingsPage(),
                "Color" => CreateColorSettingsPage(),
                _ => null
            };

            CurrentContent = content;

            if (content == null)
            {
                _logger?.LogWarning("Preference tab {Tag} did not produce content.", tag);
            }
            else
            {
                _logger?.LogInformation("Preference tab {Tag} loaded {ContentType}.", tag, content.GetType().FullName);
            }
        }

        private object? CreateCodeSettingsPage()
        {
            return TryCreatePreferenceContent(nameof(Views.CodeSettingsPage), () => new Views.CodeSettingsPage());
        }

        private object? CreateColorSettingsPage()
        {
            return TryCreatePreferenceContent(nameof(Views.ColorSettingsPage), () => new Views.ColorSettingsPage());
        }

        private object? CreateImageSettingsPage()
        {
            return TryCreatePreferenceContent(nameof(Views.ImageSettingsPage), () => new Views.ImageSettingsPage());
        }

        private object? TryCreatePreferenceContent(string name, Func<object> factory)
        {
            try
            {
                var view = factory();
                _logger?.LogInformation("Preference view {ViewName} created successfully.", name);
                return view;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create preference view {ViewName}.", name);
                return null;
            }
        }

        private ILogger<PreferencePageViewModel>? ResolveLogger()
        {
            try
            {
                return App.Current?.Services?.GetService<ILogger<PreferencePageViewModel>>();
            }
            catch
            {
                return null;
            }
        }

        // Export page removed
    }
}
