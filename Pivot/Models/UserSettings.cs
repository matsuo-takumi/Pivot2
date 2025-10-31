using System.Collections.Generic;
using Microsoft.UI.Xaml; // ElementThemeを使用するために追加
// using static Pivot.MainWindow; // BackdropTypeを使用するために追加

namespace Pivot.Models
{
    public class UserSettings
    {
        public List<string> AssetDirectories { get; set; } = new List<string>();
        public List<string> ImageDirectories { get; set; } = new List<string>();
        public List<string> ProjectDirectories { get; set; } = new List<string>();
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

        // Visible filters per tab (tab id -> list of filter Ids)
        public Dictionary<string, List<System.Guid>> VisibleFiltersByTab { get; set; } = new Dictionary<string, List<System.Guid>>();
        // Selected filters per tab (tab id -> list of selected filter Ids)
        public Dictionary<string, List<System.Guid>> SelectedFiltersByTab { get; set; } = new Dictionary<string, List<System.Guid>>();

        // (カスタムAcrylic/Luminosity設定は削除)
    }
}
