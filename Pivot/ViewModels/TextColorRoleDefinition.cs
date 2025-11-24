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
            new TextColorRoleDefinition("Text.Hyperlink", "Hyperlink Text", "Actionable hyperlinks.", Colors.DodgerBlue, "SystemControlForegroundAccentHighBrush"),
            new TextColorRoleDefinition("Text.DirectorySecondary", "Directory Labels", "Paths shown in Directory/Code screens.", Colors.Gray, "TextFillColorSecondaryBrush"),
            new TextColorRoleDefinition("Text.App.Body", "App Body Text", "Body copy in lists and cards.", ColorHelper.FromArgb(255, 68, 68, 68), "AppTextBodyBrush"),
            new TextColorRoleDefinition("Text.App.Secondary", "App Secondary Text", "Explanatory or helper copy inside panels.", ColorHelper.FromArgb(255, 102, 102, 102), "AppTextSecondaryBrush"),
            new TextColorRoleDefinition("Text.App.Caption", "App Caption Text", "Small labels and status lines.", ColorHelper.FromArgb(255, 90, 90, 90), "AppTextCaptionBrush"),
            new TextColorRoleDefinition("Text.App.Muted", "App Muted Text", "Metadata and tertiary values.", ColorHelper.FromArgb(255, 85, 85, 85), "AppTextMutedBrush"),
            new TextColorRoleDefinition("Text.App.Dim", "App Dim Text", "Placeholder text and hints.", ColorHelper.FromArgb(255, 119, 119, 119), "AppTextDimBrush"),
            new TextColorRoleDefinition("Text.App.Accent", "App Accent Text", "Linky accents and glyphs.", ColorHelper.FromArgb(255, 0, 120, 212), "AppTextAccentBrush"),
            new TextColorRoleDefinition("Text.App.AccentDim", "App Accent Dim", "Less prominent accent glyphs.", ColorHelper.FromArgb(255, 138, 138, 138), "AppTextAccentDimBrush"),
            new TextColorRoleDefinition("Text.App.Highlight", "App Highlight Text", "Text rendered on brightly colored backgrounds.", Colors.White, "AppTextHighlightBrush")
        };
    }
}

