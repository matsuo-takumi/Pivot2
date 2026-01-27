using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;
using Pivot.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.UI;
using CommunityToolkit.Mvvm.Messaging;

namespace Pivot.ViewModels
{
    public class CodeColorSettingsViewModel : ObservableObject
    {
        private readonly ThemeSettingsService _themeSettings;
        private readonly ITextColorResourceManager _resourceManager; // reusing for generic color application if needed, mostly for saving settings
        private bool _isLoading;

        // Reusing TextColorSettingViewModel as it is a generic key-value-color pair model
        public ObservableCollection<TextColorSettingViewModel> Entries { get; } = new BatchObservableCollection<TextColorSettingViewModel>();

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public CodeColorSettingsViewModel(ThemeSettingsService themeSettings, ITextColorResourceManager resourceManager)
        {
            _themeSettings = themeSettings ?? throw new ArgumentNullException(nameof(themeSettings));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            // Don't create entries synchronously
        }

        public async Task LoadEntriesAsync()
        {
            if (IsLoading || Entries.Count > 0) return;
            IsLoading = true;
            
            try
            {
                await Task.Yield();
                
                var entriesToAdd = new List<TextColorSettingViewModel>(CodeColorRoleDefinitions.Roles.Count);
                foreach (var definition in CodeColorRoleDefinitions.Roles)
                {
                    var hex = _themeSettings.GetTextColorOverride(definition.SettingKey, definition.DefaultHex);
                    var color = TextColorHelper.ParseHexOrDefault(hex, definition.DefaultColor);
                    var entry = new TextColorSettingViewModel(
                        definition.SettingKey,
                        definition.DisplayName,
                        definition.Description,
                        color,
                        OnColorChanged);
                    entriesToAdd.Add(entry);
                }
                
                if (Entries is BatchObservableCollection<TextColorSettingViewModel> batchCollection)
                {
                    batchCollection.AddRange(entriesToAdd);
                }
                else
                {
                    foreach (var entry in entriesToAdd)
                    {
                        Entries.Add(entry);
                    }
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnColorChanged(TextColorSettingViewModel entry)
        {
            if (entry == null) return;
            
            // Persist setting
            Utilities.SafeAsync.FireAndForget(PersistSelectionAsync(entry), nameof(OnColorChanged));
            
            // Notify application to update Code Theme
            // This might be done via a Messenger or an event.
            // For now, we'll let the persistence happen, and the specialized service (or Monaco control) 
            // will need to listen effectively, OR we trigger something here.
            // Since we don't have a direct "CodeThemeService" yet, we rely on the implementation 
            // in MonacoEditorControl to read these values.
            // However, we need to trigger an update.
            // Let's assume we can use the same mechanism or we might need to send a message.
            WeakReferenceMessenger.Default.Send(new CodeThemeChangedMessage());
        }

        private async Task PersistSelectionAsync(TextColorSettingViewModel entry)
        {
            try
            {
                await _themeSettings.SetTextColorOverrideAsync(entry.SettingKey, entry.HexValue);
            }
            catch { }
        }
    }

    public class CodeThemeChangedMessage
    {
    }
}
