using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml; // ElementThemeを使用するために追加
using Pivot.ViewModels;
// using static Pivot.MainWindow; // BackdropTypeを使用するために追加

namespace Pivot.Models
{
    public class UserSettings
    {
        public List<string> AssetDirectories { get; set; } = new List<string>();
        public List<string> ImageDirectories { get; set; } = new List<string>();
        public List<string> ProjectDirectories { get; set; } = new List<string>();
        public List<string> CodeDirectories { get; set; } = new List<string>();
        public ElementTheme AppTheme { get; set; } = ElementTheme.Default; // デフォルトはシステム設定に従う
        public BackdropType AppBackdropType { get; set; } = BackdropType.Mica; // デフォルトはMica

        // Asset 表示関連のユーザー設定
        public AssetDisplayMode AssetDisplayMode { get; set; } = AssetDisplayMode.List;
        public bool ShowAssetMetadata { get; set; } = true;

        // スキャン戦略
        public bool ForceFullScan { get; set; } = false;

        // ウィンドウ設定
        public MenuDisplayMode MenuDisplayMode { get; set; } = MenuDisplayMode.Compact;

        // Asset filters (custom and built-in)
        public List<CustomFilter> AssetFilters { get; set; } = new List<CustomFilter>();

        // Code filters (for code file types / languages)
        public List<CustomFilter> CodeFilters { get; set; } = new List<CustomFilter>();

        // Code categories (groups for Code tab navigation)
        public List<CodeCategory> CodeCategories { get; set; } = new List<CodeCategory>();

        // Export settings
        public string ExportOutputDirectory { get; set; } = string.Empty;

        // Code save output directory (migrated from ExportOutputDirectory)
        public string CodeSaveOutputDirectory { get; set; } = string.Empty;

        // Code export format (e.g., Json, Markdown)
        public string CodeExportFormat { get; set; } = "Json";
        
        // Last selected snippet id (persisted to restore selection between page instances)
        public Guid LastSelectedSnippetId { get; set; } = Guid.Empty;

        // Visible filters per tab (tab id -> list of filter Ids)
        public Dictionary<string, List<System.Guid>> VisibleFiltersByTab { get; set; } = new Dictionary<string, List<System.Guid>>();
        // Selected filters per tab (tab id -> list of selected filter Ids)
        public Dictionary<string, List<System.Guid>> SelectedFiltersByTab { get; set; } = new Dictionary<string, List<System.Guid>>();

        // (カスタムAcrylic/Luminosity設定は削除)
        public TextColorTemplate PreferredTextColorTemplate { get; set; } = TextColorTemplate.FullControl;
        public string DominantColor { get; set; } = "#FF0078D4";
        public double DominantVariation { get; set; } = 0.25;
        public bool DominantGenerateAccent { get; set; } = true;
        public double DominantAccentStrength { get; set; } = 0.5;
        public int RandomSeed { get; set; } = 42;
        public double RandomnessLevel { get; set; } = 0.5;
        public double RandomSaturationMin { get; set; } = 0.2;
        public double RandomSaturationMax { get; set; } = 0.8;
        public double RandomBrightnessMin { get; set; } = 0.2;
        public double RandomBrightnessMax { get; set; } = 0.8;
        public bool RandomAllowExtreme { get; set; }
        // Overlay tint color for in-app acrylic (stored as RRGGBB)
        public string OverlayTintColor { get; set; } = "#0000FF";
        public double OverlayTintOpacity { get; set; } = 0.7;
        public double OverlayTintLuminosityOpacity { get; set; } = 0.0;
        public int OverlayTintTransitionDurationMs { get; set; } = 250;
        public Dictionary<string, string> TextColorOverrides { get; set; } = new Dictionary<string, string>();
        // Enable/disable text color customization (when disabled, use default theme colors)
        public bool IsTextColorCustomizationEnabled { get; set; } = false; // Default to false (use theme colors)

        // Image selection highlight settings
        public string ImageSelectionColor { get; set; } = "#0078D4"; // Default accent blue
        public double ImageSelectionOpacity { get; set; } = 0.5; // Default 50% opacity
        public double ImageSelectionBorderThickness { get; set; } = 1.0; // Default 1px border
        public string ImageDragSelectionColor { get; set; } = "#0078D4"; // Default accent blue for drag selection
        public double ImageDragSelectionOpacity { get; set; } = 0.3; // Default 30% opacity for drag selection
        
        // Viewport settings
        public CameraGesturePreset ViewportCameraGesture { get; set; } = CameraGesturePreset.Maya;
        public bool ViewportShowGrid { get; set; } = true;
        public float ViewportGridSize { get; set; } = 10f;
        public float ViewportGridSpacing { get; set; } = 1f;
        public string ViewportGridColor { get; set; } = "#404040";
        public bool ViewportShowAxisGizmo { get; set; } = true;
        
        // Background settings
        public ViewportBackgroundMode ViewportBackgroundMode { get; set; } = ViewportBackgroundMode.Custom;
        public string ViewportBackgroundColor { get; set; } = "#3399CC"; // Default: 51, 153, 204
    }
}
