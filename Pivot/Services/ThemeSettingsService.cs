using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Pivot.Messages;
using Pivot.Models;
using Pivot.ViewModels;

namespace Pivot.Services
{
    public class ThemeSettingsService
    {
        private readonly ILogger<ThemeSettingsService> _logger;
        private readonly ISettingsStore _settingsStore;
        private readonly IMessenger _messenger;

        // Cache
        private ElementTheme _appTheme = ElementTheme.Default;
        private BackdropType _appBackdropType = BackdropType.Mica;
        
        // Time-based theme settings
        private int _lightStartHour = 6;   // Light mode starts at 6:00 AM
        private int _darkStartHour = 18;   // Dark mode starts at 6:00 PM
        
        // Overlay
        private string _overlayTintColor = "#0000FF";
        private double _overlayTintOpacity = 0.7;
        private double _overlayTintLuminosityOpacity = 0.0;
        private int _overlayTintTransitionDurationMs = 250;

        // Text Colors
        private TextColorTemplate _preferredTextColorTemplate = TextColorTemplate.FullControl;
        private string _dominantColor = "#FF0078D4";
        private double _dominantVariation = 0.25;
        private bool _dominantGenerateAccent = true;
        private double _dominantAccentStrength = 0.5;
        
        private int _randomSeed = 42;
        private double _randomnessLevel = 0.5;
        private double _randomSaturationMin = 0.2;
        private double _randomSaturationMax = 0.8;
        private double _randomBrightnessMin = 0.2;
        private double _randomBrightnessMax = 0.8;
        private bool _randomAllowExtreme = false;

        private Dictionary<string, string> _textColorOverrides = new();
        private bool _isTextColorCustomizationEnabled = false;

        // Image Selection
        private string _imageSelectionColor = "#0078D4";
        private double _imageSelectionOpacity = 0.5;
        private double _imageSelectionBorderThickness = 1.0;
        private string _imageDragSelectionColor = "#0078D4";
        private double _imageDragSelectionOpacity = 0.3;

        // Menu Display Mode
        private MenuDisplayMode _menuDisplayMode = MenuDisplayMode.Auto;

        // Browser Settings
        private int _thumbnailSize = 180;
        private int _decodePixelWidth = 300;
        private int _pageSize = 10000;
        
        // Preview Settings
        private double _previewMaxZoom = 50.0;
        private double _previewFixedDisplaySize = 800.0;

        // Public Properties
        public ElementTheme AppTheme => _appTheme;
        public int LightStartHour => _lightStartHour;
        public int DarkStartHour => _darkStartHour;
        
        /// <summary>
        /// Gets the effective theme to apply. When AppTheme is Default, returns time-based theme (Light or Dark).
        /// </summary>
        public ElementTheme EffectiveTheme
        {
            get
            {
                if (_appTheme == ElementTheme.Default)
                {
                    return GetTimeBasedTheme();
                }
                return _appTheme;
            }
        }
        
        /// <summary>
        /// Detects the current Windows system theme (Light or Dark) using UISettings.
        /// </summary>
        private static ElementTheme GetSystemTheme()
        {
            try
            {
                var uiSettings = new Windows.UI.ViewManagement.UISettings();
                var foregroundColor = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);
                // If foreground is light (close to white), system is in Dark mode
                // If foreground is dark (close to black), system is in Light mode
                bool isDark = foregroundColor.R > 127 && foregroundColor.G > 127 && foregroundColor.B > 127;
                return isDark ? ElementTheme.Dark : ElementTheme.Light;
            }
            catch
            {
                return ElementTheme.Dark; // Fallback to Dark if detection fails
            }
        }
        
        /// <summary>
        /// Gets the theme based on current time of day.
        /// </summary>
        public ElementTheme GetTimeBasedTheme()
        {
            var currentHour = DateTime.Now.Hour;
            
            // Handle case where light period crosses midnight (e.g., light 22:00 to 6:00)
            if (_lightStartHour < _darkStartHour)
            {
                // Normal case: light during day, dark at night
                // Light: lightStart <= current < darkStart
                if (currentHour >= _lightStartHour && currentHour < _darkStartHour)
                {
                    return ElementTheme.Light;
                }
                return ElementTheme.Dark;
            }
            else
            {
                // Inverted case: light at night, dark during day (unusual but supported)
                // Dark: darkStart <= current < lightStart
                if (currentHour >= _darkStartHour && currentHour < _lightStartHour)
                {
                    return ElementTheme.Dark;
                }
                return ElementTheme.Light;
            }
        }
        public BackdropType AppBackdropType => _appBackdropType;
        public string OverlayTintColor => _overlayTintColor;
        public double OverlayTintOpacity => _overlayTintOpacity;
        public double OverlayTintLuminosityOpacity => _overlayTintLuminosityOpacity;
        public int OverlayTintTransitionDurationMs => _overlayTintTransitionDurationMs;

        public TextColorTemplate PreferredTextColorTemplate => _preferredTextColorTemplate;
        public string DominantColor => _dominantColor;
        public double DominantVariation => _dominantVariation;
        public bool DominantGenerateAccent => _dominantGenerateAccent;
        public double DominantAccentStrength => _dominantAccentStrength;

        public int RandomSeed => _randomSeed;
        public double RandomnessLevel => _randomnessLevel;
        public double RandomSaturationMin => _randomSaturationMin;
        public double RandomSaturationMax => _randomSaturationMax;
        public double RandomBrightnessMin => _randomBrightnessMin;
        public double RandomBrightnessMax => _randomBrightnessMax;
        public bool RandomAllowExtreme => _randomAllowExtreme;

        public IReadOnlyDictionary<string, string> TextColorOverrides => _textColorOverrides;
        public bool IsTextColorCustomizationEnabled => _isTextColorCustomizationEnabled;

        public string ImageSelectionColor => _imageSelectionColor;
        public double ImageSelectionOpacity => _imageSelectionOpacity;
        public double ImageSelectionBorderThickness => _imageSelectionBorderThickness;
        public string ImageDragSelectionColor => _imageDragSelectionColor;
        public double ImageDragSelectionOpacity => _imageDragSelectionOpacity;
        public MenuDisplayMode MenuDisplayMode => _menuDisplayMode;
        
        // Browser Settings
        public int ThumbnailSize => _thumbnailSize;
        public int DecodePixelWidth => _decodePixelWidth;
        public int PageSize => _pageSize;
        
        // Preview Settings
        public double PreviewMaxZoom => _previewMaxZoom;
        public double PreviewFixedDisplaySize => _previewFixedDisplaySize;

        public ThemeSettingsService(
            ILogger<ThemeSettingsService> logger,
            ISettingsStore settingsStore,
            IMessenger messenger)
        {
            _logger = logger;
            _settingsStore = settingsStore;
            _messenger = messenger;
        }

        public async Task LoadAsync()
        {
            _logger.LogInformation("ThemeSettingsService: Loading settings...");

            // Theme
            var themeStr = await _settingsStore.GetAsync("AppTheme");
            if (Enum.TryParse<ElementTheme>(themeStr, out var t)) _appTheme = t;

            var backdropStr = await _settingsStore.GetAsync("AppBackdropType");
            if (Enum.TryParse<BackdropType>(backdropStr, out var b)) _appBackdropType = b;

            // Time-based theme settings
            _lightStartHour = await LoadInt("Theme.LightStartHour", 6);
            _darkStartHour = await LoadInt("Theme.DarkStartHour", 18);

            // Overlay
            var overlayColor = await _settingsStore.GetAsync("Color.OverlayTintColor");
            if (!string.IsNullOrWhiteSpace(overlayColor)) _overlayTintColor = overlayColor;

            var overlayOpacity = await _settingsStore.GetAsync("Color.OverlayTintOpacity");
            if (double.TryParse(overlayOpacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var oo)) _overlayTintOpacity = oo;

            var overlayLum = await _settingsStore.GetAsync("Color.OverlayTintLuminosityOpacity");
            if (double.TryParse(overlayLum, NumberStyles.Float, CultureInfo.InvariantCulture, out var ol)) _overlayTintLuminosityOpacity = ol;

            var overlayDur = await _settingsStore.GetAsync("Color.OverlayTintTransitionDurationMs");
            if (int.TryParse(overlayDur, out var od)) _overlayTintTransitionDurationMs = od;

            // Text Template
            var tmplStr = await _settingsStore.GetAsync("Color.TextTemplate");
            if (Enum.TryParse<TextColorTemplate>(tmplStr, out var tmpl)) _preferredTextColorTemplate = tmpl;

            _dominantColor = await LoadString("Color.Dominant.Color", "#FF0078D4");
            _dominantVariation = await LoadDouble("Color.Dominant.Variation", 0.25);
            _dominantGenerateAccent = await LoadBool("Color.Dominant.GenerateAccent", true);
            _dominantAccentStrength = await LoadDouble("Color.Dominant.AccentStrength", 0.5);

            _randomSeed = await LoadInt("Color.Random.Seed", 42);
            _randomnessLevel = await LoadDouble("Color.Random.Randomness", 0.5);
            _randomSaturationMin = await LoadDouble("Color.Random.SaturationMin", 0.2);
            _randomSaturationMax = await LoadDouble("Color.Random.SaturationMax", 0.8);
            _randomBrightnessMin = await LoadDouble("Color.Random.BrightnessMin", 0.2);
            _randomBrightnessMax = await LoadDouble("Color.Random.BrightnessMax", 0.8);
            _randomAllowExtreme = await LoadBool("Color.Random.AllowExtreme", false);

            _isTextColorCustomizationEnabled = await LoadBool("Color.TextCustomizationEnabled", false);

            var overridesJson = await _settingsStore.GetAsync("Color.TextOverrides");
            if (!string.IsNullOrWhiteSpace(overridesJson))
            {
                try { _textColorOverrides = JsonSerializer.Deserialize<Dictionary<string, string>>(overridesJson) ?? new(); } catch { }
            }

            // Image Selection
            _imageSelectionColor = await LoadString("Image.SelectionColor", "#0078D4");
            _imageSelectionOpacity = await LoadDouble("Image.SelectionOpacity", 0.5);
            _imageSelectionBorderThickness = await LoadDouble("Image.SelectionBorderThickness", 1.0);
            _imageDragSelectionColor = await LoadString("Image.DragSelectionColor", "#0078D4");
            _imageDragSelectionOpacity = await LoadDouble("Image.DragSelectionOpacity", 0.3);

            // Menu Display Mode
            var menuModeStr = await _settingsStore.GetAsync("MenuDisplayMode");
            if (Enum.TryParse<MenuDisplayMode>(menuModeStr, out var mm)) _menuDisplayMode = mm;

            // Browser Settings
            _thumbnailSize = await LoadInt("Browser.ThumbnailSize", 180);
            _decodePixelWidth = await LoadInt("Browser.DecodePixelWidth", 300);
            _pageSize = await LoadInt("Browser.PageSize", 10000);
            
            // Preview Settings
            _previewMaxZoom = await LoadDouble("Preview.MaxZoom", 50.0);
            _previewFixedDisplaySize = await LoadDouble("Preview.FixedDisplaySize", 800.0);
        }

        // Helpers
        private async Task<string> LoadString(string key, string def) 
            => (await _settingsStore.GetAsync(key)) is string s && !string.IsNullOrWhiteSpace(s) ? s : def;

        private async Task<double> LoadDouble(string key, double def)
        {
            var s = await _settingsStore.GetAsync(key);
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        private async Task<int> LoadInt(string key, int def)
        {
            var s = await _settingsStore.GetAsync(key);
            return int.TryParse(s, out var v) ? v : def;
        }

        private async Task<bool> LoadBool(string key, bool def)
        {
            var s = await _settingsStore.GetAsync(key);
            return bool.TryParse(s, out var v) ? v : def;
        }

        // Setters
        public async Task SetThemeAsync(ElementTheme theme)
        {
            _appTheme = theme;
            await _settingsStore.UpsertAsync("AppTheme", theme.ToString());
            // Note: Do NOT send ThemeChangedMessage here - the ViewModel sends it with EffectiveTheme
            // Sending it twice causes race conditions and potential crashes
        }

        public async Task SetBackdropTypeAsync(BackdropType type)
        {
            _appBackdropType = type;
            await _settingsStore.UpsertAsync("AppBackdropType", type.ToString());
        }

        public async Task SetLightStartHourAsync(int hour)
        {
            hour = Math.Clamp(hour, 0, 23);
            _lightStartHour = hour;
            await _settingsStore.UpsertAsync("Theme.LightStartHour", hour.ToString());
            _messenger.Send(new SettingsChangedMessage("LightStartHour"));
        }

        public async Task SetDarkStartHourAsync(int hour)
        {
            hour = Math.Clamp(hour, 0, 23);
            _darkStartHour = hour;
            await _settingsStore.UpsertAsync("Theme.DarkStartHour", hour.ToString());
            _messenger.Send(new SettingsChangedMessage("DarkStartHour"));
        }

        public async Task SetOverlayTintColorAsync(string color)
        {
            _overlayTintColor = color;
            await _settingsStore.UpsertAsync("Color.OverlayTintColor", color);
        }

        public async Task SetOverlayTintOpacityAsync(double opacity)
        {
            _overlayTintOpacity = opacity;
            await _settingsStore.UpsertAsync("Color.OverlayTintOpacity", opacity.ToString(CultureInfo.InvariantCulture));
        }

        public async Task SetOverlayTintLuminosityOpacityAsync(double opacity)
        {
            _overlayTintLuminosityOpacity = opacity;
            await _settingsStore.UpsertAsync("Color.OverlayTintLuminosityOpacity", opacity.ToString(CultureInfo.InvariantCulture));
        }

        public async Task SetOverlayTintTransitionDurationMsAsync(int duration)
        {
            _overlayTintTransitionDurationMs = duration;
            await _settingsStore.UpsertAsync("Color.OverlayTintTransitionDurationMs", duration.ToString());
        }

        public async Task SetTextColorTemplateAsync(TextColorTemplate template)
        {
            _preferredTextColorTemplate = template;
            await _settingsStore.UpsertAsync("Color.TextTemplate", template.ToString());
        }

        public async Task SetDominantColorAsync(string hex)
        {
            _dominantColor = hex;
            await _settingsStore.UpsertAsync("Color.Dominant.Color", hex);
        }

        public async Task SetDominantVariationAsync(double val)
        {
            _dominantVariation = val;
            await _settingsStore.UpsertAsync("Color.Dominant.Variation", val.ToString(CultureInfo.InvariantCulture));
        }

        public async Task SetDominantGenerateAccentAsync(bool val)
        {
            _dominantGenerateAccent = val;
            await _settingsStore.UpsertAsync("Color.Dominant.GenerateAccent", val.ToString());
        }

        public async Task SetDominantAccentStrengthAsync(double val)
        {
            _dominantAccentStrength = val;
            await _settingsStore.UpsertAsync("Color.Dominant.AccentStrength", val.ToString(CultureInfo.InvariantCulture));
        }

        public async Task SetRandomSeedAsync(int val)
        {
            _randomSeed = val;
            await _settingsStore.UpsertAsync("Color.Random.Seed", val.ToString());
        }

        public async Task SetRandomnessLevelAsync(double val)
        {
            _randomnessLevel = val;
            await _settingsStore.UpsertAsync("Color.Random.Randomness", val.ToString(CultureInfo.InvariantCulture));
        }

        public async Task SetRandomSaturationMinAsync(double val)
        {
            _randomSaturationMin = val;
            await _settingsStore.UpsertAsync("Color.Random.SaturationMin", val.ToString(CultureInfo.InvariantCulture));
        }

        public async Task SetRandomSaturationMaxAsync(double val)
        {
            _randomSaturationMax = val;
            await _settingsStore.UpsertAsync("Color.Random.SaturationMax", val.ToString(CultureInfo.InvariantCulture));
        }

        public async Task SetRandomBrightnessMinAsync(double val)
        {
            _randomBrightnessMin = val;
            await _settingsStore.UpsertAsync("Color.Random.BrightnessMin", val.ToString(CultureInfo.InvariantCulture));
        }

        public async Task SetRandomBrightnessMaxAsync(double val)
        {
            _randomBrightnessMax = val;
            await _settingsStore.UpsertAsync("Color.Random.BrightnessMax", val.ToString(CultureInfo.InvariantCulture));
        }

        public async Task SetRandomAllowExtremeAsync(bool val)
        {
            _randomAllowExtreme = val;
            await _settingsStore.UpsertAsync("Color.Random.AllowExtreme", val.ToString());
        }

        public async Task SetTextColorCustomizationEnabledAsync(bool enabled)
        {
            _isTextColorCustomizationEnabled = enabled;
            await _settingsStore.UpsertAsync("Color.TextCustomizationEnabled", enabled.ToString());
        }

        public async Task SetTextColorOverrideAsync(string key, string hex)
        {
            _textColorOverrides[key] = hex;
            await _settingsStore.UpsertAsync("Color.TextOverrides", JsonSerializer.Serialize(_textColorOverrides));
        }

        public async Task ClearAllTextColorOverridesAsync()
        {
            _textColorOverrides.Clear();
            await _settingsStore.UpsertAsync("Color.TextOverrides", JsonSerializer.Serialize(_textColorOverrides));
        }

        public string GetTextColorOverride(string key, string? defaultHex = null)
        {
            if (_textColorOverrides.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val;
            }
            return defaultHex ?? string.Empty;
        }

        public async Task SetImageSelectionColorAsync(string color)
        {
            _imageSelectionColor = color;
            await _settingsStore.UpsertAsync("Image.SelectionColor", color);
            _messenger.Send(new SettingsChangedMessage("ImageSelectionColor"));
        }

        public async Task SetImageSelectionOpacityAsync(double opacity)
        {
            _imageSelectionOpacity = opacity;
            await _settingsStore.UpsertAsync("Image.SelectionOpacity", opacity.ToString(CultureInfo.InvariantCulture));
            _messenger.Send(new SettingsChangedMessage("ImageSelectionOpacity"));
        }

        public async Task SetImageSelectionBorderThicknessAsync(double thickness)
        {
            _imageSelectionBorderThickness = thickness;
            await _settingsStore.UpsertAsync("Image.SelectionBorderThickness", thickness.ToString(CultureInfo.InvariantCulture));
            _messenger.Send(new SettingsChangedMessage("ImageSelectionBorderThickness"));
        }

        public async Task SetImageDragSelectionColorAsync(string color)
        {
            _imageDragSelectionColor = color;
            await _settingsStore.UpsertAsync("Image.DragSelectionColor", color);
            _messenger.Send(new SettingsChangedMessage("ImageDragSelectionColor"));
        }

        public async Task SetImageDragSelectionOpacityAsync(double opacity)
        {
            _imageDragSelectionOpacity = opacity;
            await _settingsStore.UpsertAsync("Image.DragSelectionOpacity", opacity.ToString(CultureInfo.InvariantCulture));
            _messenger.Send(new SettingsChangedMessage("ImageDragSelectionOpacity"));
        }

        public async Task SetMenuDisplayModeAsync(MenuDisplayMode mode)
        {
            _menuDisplayMode = mode;
            await _settingsStore.UpsertAsync("MenuDisplayMode", mode.ToString());
        }

        // Browser Settings
        public async Task SetThumbnailSizeAsync(int size)
        {
            _thumbnailSize = size;
            await _settingsStore.UpsertAsync("Browser.ThumbnailSize", size.ToString());
            _messenger.Send(new SettingsChangedMessage("ThumbnailSize"));
        }

        public async Task SetDecodePixelWidthAsync(int width)
        {
            _decodePixelWidth = width;
            await _settingsStore.UpsertAsync("Browser.DecodePixelWidth", width.ToString());
            _messenger.Send(new SettingsChangedMessage("DecodePixelWidth"));
        }

        public async Task SetPageSizeAsync(int size)
        {
            _pageSize = size;
            await _settingsStore.UpsertAsync("Browser.PageSize", size.ToString());
            _messenger.Send(new SettingsChangedMessage("PageSize"));
        }

        // Preview Settings
        public async Task SetPreviewMaxZoomAsync(double zoom)
        {
            _previewMaxZoom = zoom;
            await _settingsStore.UpsertAsync("Preview.MaxZoom", zoom.ToString(CultureInfo.InvariantCulture));
            _messenger.Send(new SettingsChangedMessage("PreviewMaxZoom"));
        }

        public async Task SetPreviewFixedDisplaySizeAsync(double size)
        {
            _previewFixedDisplaySize = size;
            await _settingsStore.UpsertAsync("Preview.FixedDisplaySize", size.ToString(CultureInfo.InvariantCulture));
            _messenger.Send(new SettingsChangedMessage("PreviewFixedDisplaySize"));
        }
    }
}
