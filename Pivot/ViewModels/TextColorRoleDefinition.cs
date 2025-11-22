using Microsoft.UI;
using System.Collections.Generic;
using Windows.UI;
using Pivot.Utilities;

namespace Pivot.ViewModels
{
    public sealed class TextColorRoleDefinition
    {
        public string SettingKey { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public Color DefaultColor { get; }
        public string DefaultHex => TextColorHelper.FormatHex(DefaultColor);
        public string ResourceKey { get; }

        public TextColorRoleDefinition(string settingKey, string displayName, string description, Color defaultColor, string resourceKey)
        {
            SettingKey = settingKey;
            DisplayName = displayName;
            Description = description;
            DefaultColor = defaultColor;
            ResourceKey = resourceKey;
        }
    }

    public static class TextColorRoleDefinitions
    {
        public static readonly IReadOnlyList<TextColorRoleDefinition> Roles = new[]
        {
            new TextColorRoleDefinition("Text.Primary", "Primary Text", "Used for main copy and headings.", Colors.Black, "SystemControlForegroundBaseHighBrush"),
            new TextColorRoleDefinition("Text.Secondary", "Secondary Text", "Used for supporting copy and metadata.", Colors.DarkGray, "SystemControlForegroundBaseMediumHighBrush"),
            new TextColorRoleDefinition("Text.Tertiary", "Tertiary Text", "Low emphasis text such as timestamps.", Colors.Gray, "SystemControlForegroundBaseMediumBrush"),
            new TextColorRoleDefinition("Text.Disabled", "Disabled Text", "Text representing disabled state.", Colors.LightGray, "SystemControlForegroundBaseLowBrush"),
            new TextColorRoleDefinition("Text.Accent", "Accent Text", "Primary accent (links/badges).", Colors.DeepSkyBlue, "SystemControlForegroundAccentBrush"),
            new TextColorRoleDefinition("Text.AccentSecondary", "Accent Secondary", "Subtle accent for inline items.", Colors.CornflowerBlue, "SystemControlForegroundAccentLowBrush"),
            new TextColorRoleDefinition("Text.Hyperlink", "Hyperlink Text", "Actionable hyperlinks.", Colors.DodgerBlue, "SystemControlForegroundAccentHighBrush")
        };
    }
}

