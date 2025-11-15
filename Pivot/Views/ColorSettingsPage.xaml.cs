using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            ViewModel = new ColorSettingsViewModel(settings);
            this.DataContext = ViewModel;
        }
    }
}


