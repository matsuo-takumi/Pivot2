using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Services;
using Pivot.ViewModels;

namespace Pivot.Views
{
    public sealed partial class ColorSettingsPage : Page
    {
        public ColorSettingsViewModel ViewModel { get; }

        public ColorSettingsPage()
        {
            this.InitializeComponent();
            var settings = App.Current.Services.GetRequiredService<SettingsService>();
            var textColorManager = App.Current.Services.GetRequiredService<ITextColorResourceManager>();
            ViewModel = new ColorSettingsViewModel(settings, textColorManager);
            this.DataContext = ViewModel;
        }

        private void ColorSwatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
        }
    }
}


