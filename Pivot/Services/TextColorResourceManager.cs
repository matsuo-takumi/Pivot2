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
    }

    public class TextColorResourceManager : ITextColorResourceManager
    {
        private readonly Dictionary<string, TextColorRoleDefinition> _roleMap;
        private readonly SettingsService _settings;

        public TextColorResourceManager(SettingsService settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _roleMap = TextColorRoleDefinitions.Roles.ToDictionary(def => def.SettingKey);
            foreach (var role in TextColorRoleDefinitions.Roles)
            {
                var hex = _settings.GetTextColorOverride(role.SettingKey, role.DefaultHex);
                var color = TextColorHelper.ParseHexOrDefault(hex, role.DefaultColor);
                ApplyColor(role.SettingKey, color);
            }
        }

        public void ApplyColor(string settingKey, Color color)
        {
            if (!_roleMap.TryGetValue(settingKey, out var role)) return;
            var brush = GetOrCreateBrush(role.ResourceKey, role.DefaultColor);
            brush.Color = color;
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

