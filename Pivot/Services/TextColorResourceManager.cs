using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Pivot.ViewModels;
using Pivot.Utilities;
using System;
using System.Collections.Generic;
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

            if (app.Resources.TryGetValue(key, out var existing) && existing is SolidColorBrush brush)
            {
                return brush;
            }

            var newBrush = new SolidColorBrush(fallback);
            app.Resources[key] = newBrush;
            return newBrush;
        }
    }
}

