using Microsoft.UI.Xaml.Controls;
using Pivot.Models;

namespace Pivot.Views
{
    /// <summary>
    /// Test page for UnifiedBrowserControl verification.
    /// </summary>
    public sealed partial class BrowserTestPage : Page
    {
        public BrowserTestPage()
        {
            this.InitializeComponent();
            this.Loaded += BrowserTestPage_Loaded;
        }

        private async void BrowserTestPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // Initialize browser with Image assets
            await BrowserControl.InitializeAsync(AssetKind.Image);
        }
    }
}
