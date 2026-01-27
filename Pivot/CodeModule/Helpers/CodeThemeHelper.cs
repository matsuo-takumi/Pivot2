using Pivot.Services;
using Pivot.ViewModels;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace Pivot.CodeModule.Helpers
{
    public static class CodeThemeHelper
    {
        public static object GenerateThemeData(ThemeSettingsService themeSettings)
        {
            // Default dark colors
            var rules = new List<object>();
            var colors = new Dictionary<string, string>();

            // Helper to get hex
            string GetHex(string key, Color defaultColor)
            {
                var hex = themeSettings.GetTextColorOverride(key, TextColorRoleDefinitions.Roles.FirstOrDefault(r => r.SettingKey == key)?.DefaultHex);
                // Fallback to Code role definitions if not found in TextColorRoleDefinitions (which is likely, since we have new keys)
                if (hex == null || hex == TextColorRoleDefinitions.Roles.FirstOrDefault(r => r.SettingKey == key)?.DefaultHex)
                {
                     var codeDef = CodeColorRoleDefinitions.Roles.FirstOrDefault(r => r.SettingKey == key);
                     if (codeDef != null)
                     {
                         return themeSettings.GetTextColorOverride(key, codeDef.DefaultHex);
                     }
                }
                return hex ?? Pivot.Utilities.TextColorHelper.FormatHex(defaultColor);
            }

            // Map roles to Monaco tokens
            foreach (var role in CodeColorRoleDefinitions.Roles)
            {
                var hex = GetHex(role.SettingKey, role.DefaultColor);
                
                // If it has a token, add a rule
                if (!string.IsNullOrEmpty(role.MonacoToken))
                {
                    rules.Add(new { token = role.MonacoToken, foreground = hex });
                }
                
                // Specific editor colors
                if (role.SettingKey == "Code.Background") colors["editor.background"] = hex;
                if (role.SettingKey == "Code.Foreground") colors["editor.foreground"] = hex;
            }

            return new Dictionary<string, object>
            {
                { "base", "vs-dark" }, // Always base on dark for now as Pivot is dark-themed
                { "inherit", true },
                { "rules", rules },
                { "colors", colors }
            };
        }
    }
}
