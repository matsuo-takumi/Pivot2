using System.Collections.Generic;
using Microsoft.UI.Xaml; // ElementThemeを使用するために追加
using Windows.UI; // Colorを使用するために追加
using static Pivot.MainWindow; // BackdropTypeを使用するために追加

namespace Pivot.Models
{
    public class UserSettings
    {
        public List<string> AssetDirectories { get; set; } = new List<string>();
        public List<string> ImageDirectories { get; set; } = new List<string>();
        public List<string> ProjectDirectories { get; set; } = new List<string>();
        public ElementTheme AppTheme { get; set; } = ElementTheme.Default; // デフォルトはシステム設定に従う
        public BackdropType AppBackdropType { get; set; } = BackdropType.Mica; // デフォルトはMica
        public Color AccentColor { get; set; } = Microsoft.UI.Colors.DeepSkyBlue;
    }
}
