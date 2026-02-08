using System;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;

namespace Pivot.CodeModule.Services
{
    public class CodeSettingsService
    {
        private readonly ISettingsStore _settingsStore;
        private const string KeyBackgroundMode = "Code_BackgroundMode";
        private const string KeyCustomColor = "Code_CustomColor";

        public enum BackgroundMode
        {
            Theme, // Default theme brush
            Custom // User defined color
        }

        public BackgroundMode CurrentMode { get; private set; } = BackgroundMode.Theme;
        public string CustomColorHex { get; private set; } = "#FF2D2D2D"; // Default dark gray

        public event EventHandler? SettingsChanged;

        public CodeSettingsService(ISettingsStore settingsStore)
        {
            _settingsStore = settingsStore;
        }

        public async Task LoadAsync()
        {
            var modeStr = await _settingsStore.GetAsync(KeyBackgroundMode);
            if (!string.IsNullOrEmpty(modeStr) && Enum.TryParse<BackgroundMode>(modeStr, out var mode))
            {
                CurrentMode = mode;
            }

            var enabledStr = await _settingsStore.GetAsync(KeyIsCustomizationEnabled);
            if (!string.IsNullOrEmpty(enabledStr) && bool.TryParse(enabledStr, out var enabled))
            {
                IsCustomizationEnabled = enabled;
            }

            CustomColorHex = await _settingsStore.GetAsync(KeyCustomColor) ?? "#FF2D2D2D";

            EditorTextColorHex = await _settingsStore.GetAsync(KeyEditorTextColor);
            EditorBackgroundColorHex = await _settingsStore.GetAsync(KeyEditorBackgroundColor);
            
            TitleColorHex = await _settingsStore.GetAsync(KeyTitleColor);
            TagTextColorHex = await _settingsStore.GetAsync(KeyTagTextColor);
            TagBackgroundColorHex = await _settingsStore.GetAsync(KeyTagBackgroundColor); 
            TagBorderColorHex = await _settingsStore.GetAsync(KeyTagBorderColor); 
            LineNumberColorHex = await _settingsStore.GetAsync(KeyLineNumberColor);
        }

        public async Task SetModeAsync(BackgroundMode mode)
        {
            CurrentMode = mode;
            await _settingsStore.UpsertAsync(KeyBackgroundMode, mode.ToString());
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task SetCustomColorAsync(string hex)
        {
            CustomColorHex = hex;
            await _settingsStore.UpsertAsync(KeyCustomColor, hex);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        // New Editor Colors
        private const string KeyEditorTextColor = "Code_EditorTextColor";
        private const string KeyEditorBackgroundColor = "Code_EditorBackgroundColor";

        public string? EditorTextColorHex { get; private set; } // Null = Theme Default
        public string? EditorBackgroundColorHex { get; private set; } // Null = Theme Default
        
        public async Task SetEditorTextColorAsync(string? hex)
        {
            EditorTextColorHex = hex;
            if (hex == null) await _settingsStore.DeleteAsync(KeyEditorTextColor);
            else await _settingsStore.UpsertAsync(KeyEditorTextColor, hex);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task SetEditorBackgroundColorAsync(string? hex)
        {
            EditorBackgroundColorHex = hex;
            if (hex == null) await _settingsStore.DeleteAsync(KeyEditorBackgroundColor);
            else await _settingsStore.UpsertAsync(KeyEditorBackgroundColor, hex);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        
        // New Customizable Colors
        private const string KeyTitleColor = "Code_TitleColor";
        private const string KeyTagTextColor = "Code_TagTextColor";
        private const string KeyTagBackgroundColor = "Code_TagBackgroundColor";
        private const string KeyTagBorderColor = "Code_TagBorderColor";
        private const string KeyLineNumberColor = "Code_LineNumberColor";

        public string? TitleColorHex { get; private set; }
        public string? TagTextColorHex { get; private set; }
        public string? TagBackgroundColorHex { get; private set; }
        public string? TagBorderColorHex { get; private set; }
        public string? LineNumberColorHex { get; private set; }

        public async Task SetTitleColorAsync(string? hex) { TitleColorHex = hex; if(hex==null) await _settingsStore.DeleteAsync(KeyTitleColor); else await _settingsStore.UpsertAsync(KeyTitleColor, hex); SettingsChanged?.Invoke(this, EventArgs.Empty); }
        public async Task SetTagTextColorAsync(string? hex) { TagTextColorHex = hex; if(hex==null) await _settingsStore.DeleteAsync(KeyTagTextColor); else await _settingsStore.UpsertAsync(KeyTagTextColor, hex); SettingsChanged?.Invoke(this, EventArgs.Empty); }
        public async Task SetTagBackgroundColorAsync(string? hex) { TagBackgroundColorHex = hex; if(hex==null) await _settingsStore.DeleteAsync(KeyTagBackgroundColor); else await _settingsStore.UpsertAsync(KeyTagBackgroundColor, hex); SettingsChanged?.Invoke(this, EventArgs.Empty); }
        public async Task SetTagBorderColorAsync(string? hex) { TagBorderColorHex = hex; if(hex==null) await _settingsStore.DeleteAsync(KeyTagBorderColor); else await _settingsStore.UpsertAsync(KeyTagBorderColor, hex); SettingsChanged?.Invoke(this, EventArgs.Empty); }
        public async Task SetLineNumberColorAsync(string? hex) { LineNumberColorHex = hex; if(hex==null) await _settingsStore.DeleteAsync(KeyLineNumberColor); else await _settingsStore.UpsertAsync(KeyLineNumberColor, hex); SettingsChanged?.Invoke(this, EventArgs.Empty); }

        public Brush? GetTitleBrush() => GetBrushFromHex(TitleColorHex);
        public Brush? GetTagTextBrush() => GetBrushFromHex(TagTextColorHex);
        public Brush? GetTagBackgroundBrush() => GetBrushFromHex(TagBackgroundColorHex);
        public Brush? GetTagBorderBrush() => GetBrushFromHex(TagBorderColorHex);
        public Brush? GetLineNumberBrush() => GetBrushFromHex(LineNumberColorHex);

        public Brush? GetEditorTextBrush() => GetBrushFromHex(EditorTextColorHex);
        public Brush? GetEditorBackgroundBrush() => GetBrushFromHex(EditorBackgroundColorHex);

        // Toggle Customization
        private const string KeyIsCustomizationEnabled = "Code_IsCustomizationEnabled";
        public bool IsCustomizationEnabled { get; private set; } = true;

        public async Task SetCustomizationEnabledAsync(bool enabled)
        {
            IsCustomizationEnabled = enabled;
            await _settingsStore.UpsertAsync(KeyIsCustomizationEnabled, enabled.ToString());
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private Brush? GetBrushFromHex(string? hex)
        {
            if (!IsCustomizationEnabled) return null; // Force default if disabled
            if (string.IsNullOrEmpty(hex)) return null;
            try
            {
                var color = ParseHexColor(hex);
                return new SolidColorBrush(color);
            }
            catch { return null; }
        }

        public Brush? GetBackgroundBrush()
        {
            if (!IsCustomizationEnabled) return null; // Force default
            if (CurrentMode == BackgroundMode.Theme)
            {
                return null; 
            }
            
            try 
            {
                if (string.IsNullOrEmpty(CustomColorHex)) return null;
                
                var color = ParseHexColor(CustomColorHex);
                return new SolidColorBrush(color);
            }
            catch
            {
                return null;
            }
        }
        
        // Preset Support
        public Pivot.Models.CodeColorPresetData CreatePresetData()
        {
            return new Pivot.Models.CodeColorPresetData
            {
                IsCustomizationEnabled = IsCustomizationEnabled,
                BackgroundMode = CurrentMode.ToString(),
                CustomBackgroundColor = CustomColorHex,
                EditorTextColor = EditorTextColorHex,
                EditorBackgroundColor = EditorBackgroundColorHex,
                TitleColor = TitleColorHex,
                TagTextColor = TagTextColorHex,
                TagBackgroundColor = TagBackgroundColorHex,
                TagBorderColor = TagBorderColorHex,
                LineNumberColor = LineNumberColorHex
            };
        }

        public async Task ApplyPresetAsync(Pivot.Models.CodeColorPresetData data)
        {
            if (data == null) return;

            // Apply Settings one by one to trigger updates and persistence
            await SetCustomizationEnabledAsync(data.IsCustomizationEnabled);
            
            if (Enum.TryParse<BackgroundMode>(data.BackgroundMode, out var mode))
            {
                await SetModeAsync(mode);
            }
            
            await SetCustomColorAsync(data.CustomBackgroundColor);
            await SetEditorTextColorAsync(data.EditorTextColor);
            await SetEditorBackgroundColorAsync(data.EditorBackgroundColor);
            await SetTitleColorAsync(data.TitleColor);
            await SetTagTextColorAsync(data.TagTextColor);
            await SetTagBackgroundColorAsync(data.TagBackgroundColor);
            await SetTagBorderColorAsync(data.TagBorderColor);
            await SetLineNumberColorAsync(data.LineNumberColor);
            
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public static Windows.UI.Color ParseHexColor(string hex)
        {
            hex = hex.Replace("#", "");
            byte a = 255;
            byte r = 0;
            byte g = 0;
            byte b = 0;
            
            try
            {
                if (hex.Length == 6)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }
                else if (hex.Length == 8)
                {
                    a = Convert.ToByte(hex.Substring(0, 2), 16);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                }
            }
            catch { }
            
            return Windows.UI.Color.FromArgb(a, r, g, b);
        }
    }
}
