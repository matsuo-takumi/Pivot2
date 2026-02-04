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

            CustomColorHex = await _settingsStore.GetAsync(KeyCustomColor) ?? "#FF2D2D2D";

            EditorTextColorHex = await _settingsStore.GetAsync(KeyEditorTextColor) ?? "#FFFFFFFF";
            EditorBackgroundColorHex = await _settingsStore.GetAsync(KeyEditorBackgroundColor) ?? "#FF1E1E1E";
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

        public string EditorTextColorHex { get; private set; } = "#FFFFFFFF"; // Default White
        public string EditorBackgroundColorHex { get; private set; } = "#FF1E1E1E"; // Default Dark

        public async Task SetEditorTextColorAsync(string hex)
        {
            EditorTextColorHex = hex;
            await _settingsStore.UpsertAsync(KeyEditorTextColor, hex);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task SetEditorBackgroundColorAsync(string hex)
        {
            EditorBackgroundColorHex = hex;
            await _settingsStore.UpsertAsync(KeyEditorBackgroundColor, hex);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public Brush? GetEditorTextBrush()
        {
            try
            {
                if (string.IsNullOrEmpty(EditorTextColorHex)) return new SolidColorBrush(Colors.White);
                var color = ParseHexColor(EditorTextColorHex);
                return new SolidColorBrush(color);
            }
            catch { return new SolidColorBrush(Colors.White); }
        }

        public Brush? GetEditorBackgroundBrush()
        {
            try
            {
                if (string.IsNullOrEmpty(EditorBackgroundColorHex)) return new SolidColorBrush(ParseHexColor("#FF1E1E1E"));
                var color = ParseHexColor(EditorBackgroundColorHex);
                return new SolidColorBrush(color);
            }
            catch { return new SolidColorBrush(ParseHexColor("#FF1E1E1E")); }
        }

        public Brush? GetBackgroundBrush()
        {
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
