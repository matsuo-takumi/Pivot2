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

        // (カスタムAcrylic/Luminosity設定は削除)
    }
}
