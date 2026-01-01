using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Pivot.Services;

namespace Pivot.Views
{
    public sealed partial class KeyConfigPage : Page
    {
        private readonly ViewportSettingsService? _settings;

        public KeyConfigPage()
        {
            this.InitializeComponent();
            _settings = App.Current?.Services?.GetService<ViewportSettingsService>();
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (_settings == null) return;
            
            // Load current shortcut settings
            LitShortcutBox.Text = _settings.GetAssetLitShortcut();
            DepthShortcutBox.Text = _settings.GetAssetDepthShortcut();
            WorldNormalShortcutBox.Text = _settings.GetAssetWorldNormalShortcut();
        }
    }
}
