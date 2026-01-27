using Microsoft.UI;
using System.Collections.Generic;
using Windows.UI;
using Pivot.Utilities;

namespace Pivot.ViewModels
{
    public sealed class CodeColorRoleDefinition
    {
        public string SettingKey { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public Color DefaultColor { get; }
        public string DefaultHex => TextColorHelper.FormatHex(DefaultColor);
        public string MonacoToken { get; } // e.g., "keyword", "string", "comment"

        public CodeColorRoleDefinition(string settingKey, string displayName, string description, Color defaultColor, string monacoToken)
        {
            SettingKey = settingKey;
            DisplayName = displayName;
            Description = description;
            DefaultColor = defaultColor;
            MonacoToken = monacoToken;
        }
    }

    public static class CodeColorRoleDefinitions
    {
        // Default colors based on VS Code Dark+ theme (approximate)
        public static readonly IReadOnlyList<CodeColorRoleDefinition> Roles = new[]
        {
            new CodeColorRoleDefinition("Code.Background", "Editor Background", "Background color of the code editor.", Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30), ""),
            new CodeColorRoleDefinition("Code.Foreground", "Editor Foreground", "Default text color.", Microsoft.UI.ColorHelper.FromArgb(255, 212, 212, 212), ""),
            
            new CodeColorRoleDefinition("Code.Keyword", "Keywords", "Configuration for keywords (if, else, class, etc.).", Microsoft.UI.ColorHelper.FromArgb(255, 197, 134, 192), "keyword"),
            new CodeColorRoleDefinition("Code.String", "Strings", "Configuration for string literals.", Microsoft.UI.ColorHelper.FromArgb(255, 206, 145, 120), "string"),
            new CodeColorRoleDefinition("Code.Comment", "Comments", "Configuration for comments.", Microsoft.UI.ColorHelper.FromArgb(255, 106, 153, 85), "comment"),
            new CodeColorRoleDefinition("Code.Number", "Numbers", "Configuration for numeric literals.", Microsoft.UI.ColorHelper.FromArgb(255, 181, 206, 168), "number"),
        };
    }
}
