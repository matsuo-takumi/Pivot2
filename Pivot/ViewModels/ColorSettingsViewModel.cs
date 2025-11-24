using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;
using Pivot.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace Pivot.ViewModels
{
    public partial class ColorSettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settings;
        private readonly ITextColorResourceManager _resourceManager;

        public TextColorSettingsViewModel TextColorSettings { get; }
        public TextColorPresetViewModel PresetViewModel { get; }

        public ColorSettingsViewModel(
            SettingsService settings,
            ITextColorResourceManager resourceManager,
            IPresetService<Models.TextColorPresetData> presetService,
            Microsoft.Extensions.Logging.ILogger<TextColorPresetViewModel>? presetLogger = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            TextColorSettings = new TextColorSettingsViewModel(settings, resourceManager);
            PresetViewModel = new TextColorPresetViewModel(presetService, TextColorSettings, presetLogger);
        }

        public async Task ResetToDefaultsAsync()
        {
            // Reset individual text color roles to defaults
            foreach (var entry in TextColorSettings.Entries)
            {
                var definition = TextColorRoleDefinitions.Roles.FirstOrDefault(r => r.SettingKey == entry.SettingKey);
                if (definition != null)
                {
                    entry.SelectedColor = definition.DefaultColor;
                }
            }

            // Clear all text color overrides
            await _settings.ClearAllTextColorOverridesAsync();
        }

    }
}


