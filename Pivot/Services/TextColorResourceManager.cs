using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Pivot.ViewModels;
using Pivot.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Windows.UI;

namespace Pivot.Services
{
    public interface ITextColorResourceManager
    {
        void ApplyColor(string settingKey, Color color);

        SolidColorBrush EnsureBrush(string resourceKey, Color fallback);
        
        void UpdateThemeColors();
    }

    public class TextColorResourceManager : ITextColorResourceManager
    {
        private readonly Dictionary<string, TextColorRoleDefinition> _roleMap;
        private readonly SettingsService _settings;
        private readonly Dictionary<string, SolidColorBrush> _brushCache;

        public TextColorResourceManager(SettingsService settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _roleMap = TextColorRoleDefinitions.Roles.ToDictionary(def => def.SettingKey);
            _brushCache = new Dictionary<string, SolidColorBrush>();
            
            // Pre-initialize all brushes to avoid repeated lookups
            InitializeAllBrushes();
            
            // Initialize colors based on customization enabled state
            if (_settings.IsTextColorCustomizationEnabled())
            {
                // Apply custom colors from settings
                foreach (var role in TextColorRoleDefinitions.Roles)
                {
                    var hex = _settings.GetTextColorOverride(role.SettingKey, role.DefaultHex);
                    var color = TextColorHelper.ParseHexOrDefault(hex, role.DefaultColor);
                    ApplyColor(role.SettingKey, color);
                }
            }
            else
            {
                // Use default theme colors
                UpdateThemeColors();
            }
        }

        private void InitializeAllBrushes()
        {
            var app = Application.Current;
            if (app == null || app.Resources == null) return;

            // Pre-create all brushes for all roles to populate cache
            foreach (var role in TextColorRoleDefinitions.Roles)
            {
                var brush = GetOrCreateBrush(role.ResourceKey, role.DefaultColor);
                _brushCache[role.ResourceKey] = brush;
            }
        }

        public void ApplyColor(string settingKey, Color color)
        {
            // Only apply custom colors if customization is enabled
            if (!_settings.IsTextColorCustomizationEnabled()) return;
            
            if (!_roleMap.TryGetValue(settingKey, out var role)) return;
            var brush = GetOrCreateBrush(role.ResourceKey, role.DefaultColor);
            brush.Color = color;
        }
        
        public void UpdateThemeColors()
        {
            // Update all text colors to use default theme colors
            var app = Application.Current;
            if (app == null) return;
            
            if (app.Resources == null) return;
            
            // Get current theme
            ElementTheme theme = ElementTheme.Default;
            try
            {
                var pivotApp = app as App;
                var mainWindow = pivotApp?.MainWindow;
                var rootElement = mainWindow?.Content as FrameworkElement;
                theme = rootElement?.ActualTheme ?? ElementTheme.Default;
            }
            catch
            {
                // Fallback to Default if unable to determine theme
                theme = ElementTheme.Default;
            }
            
            // For each role, restore the default theme color from system resources
            foreach (var role in TextColorRoleDefinitions.Roles)
            {
                var brush = GetOrCreateBrush(role.ResourceKey, role.DefaultColor);
                
                // Try to get the default theme color from system resources
                // If not found, use the default color from the role definition
                Color themeColor = role.DefaultColor;
                
                // Try to get from theme dictionaries
                if (app.Resources.ThemeDictionaries != null)
                {
                    var themeKey = theme == ElementTheme.Dark ? "Dark" : "Light";
                    if (app.Resources.ThemeDictionaries.TryGetValue(themeKey, out var themeDict) && 
                        themeDict is ResourceDictionary themeDictionary)
                    {
                        if (themeDictionary.TryGetValue(role.ResourceKey, out var themeBrush) && 
                            themeBrush is SolidColorBrush themeColorBrush)
                        {
                            themeColor = themeColorBrush.Color;
                        }
                    }
                    
                    // Also check Default theme dictionary
                    if (app.Resources.ThemeDictionaries.TryGetValue("Default", out var defaultDict) && 
                        defaultDict is ResourceDictionary defaultDictionary)
                    {
                        if (defaultDictionary.TryGetValue(role.ResourceKey, out var defaultBrush) && 
                            defaultBrush is SolidColorBrush defaultColorBrush)
                        {
                            themeColor = defaultColorBrush.Color;
                        }
                    }
                }
                
                brush.Color = themeColor;
            }
        }

        public SolidColorBrush EnsureBrush(string resourceKey, Color fallback)
        {
            // Check cache first to avoid expensive dictionary lookups
            if (_brushCache.TryGetValue(resourceKey, out var cachedBrush))
            {
                return cachedBrush;
            }

            var brush = GetOrCreateBrush(resourceKey, fallback);
            _brushCache[resourceKey] = brush;
            return brush;
        }

        private SolidColorBrush GetOrCreateBrush(string key, Color fallback)
        {
            var app = Application.Current;
            if (app == null)
            {
                return new SolidColorBrush(fallback);
            }

            if (TryFindExistingBrush(app.Resources, key, out var existingBrush))
            {
                return existingBrush;
            }

            var newBrush = new SolidColorBrush(fallback);
            app.Resources[key] = newBrush;
            AddBrushToThemeDictionaries(app.Resources, key, newBrush);
            return newBrush;
        }

        private static bool TryFindExistingBrush(ResourceDictionary resources, string key, [NotNullWhen(true)] out SolidColorBrush? brush)
        {
            if (resources.TryGetValue(key, out var existing) && existing is SolidColorBrush solid)
            {
                brush = solid;
                return true;
            }

            foreach (ResourceDictionary themeDictionary in resources.ThemeDictionaries.Values)
            {
                if (themeDictionary.TryGetValue(key, out existing) && existing is SolidColorBrush themeBrush)
                {
                    brush = themeBrush;
                    return true;
                }
            }

            foreach (var merged in resources.MergedDictionaries)
            {
                if (TryFindExistingBrush(merged, key, out brush))
                {
                    return true;
                }
            }

            brush = null;
            return false;
        }

        private static void AddBrushToThemeDictionaries(ResourceDictionary resources, string key, SolidColorBrush brush)
        {
            foreach (ResourceDictionary themeDictionary in resources.ThemeDictionaries.Values)
            {
                themeDictionary[key] = brush;
            }
        }
    }
}

