using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pivot.ViewModels;
using Microsoft.UI.Xaml;
using System;

namespace Pivot.Views
{
    public sealed partial class ThemePage : UserControl
    {
        public ThemePage()
        {
            this.InitializeComponent();
            
            // Set ThemeViewModel as DataContext to manage all theme-related settings
            this.DataContext = App.Current.Services.GetRequiredService<ThemeViewModel>();
        }

        public ThemeViewModel ViewModel => (ThemeViewModel)this.DataContext;

        private void BackdropButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && this.DataContext is ThemeViewModel vm)
            {
                var tag = button.Tag as string;
                if (tag != null && Enum.TryParse<Pivot.Models.BackdropType>(tag, out var backdropType))
                {
                    vm.AppBackdropType = backdropType;
                }
            }
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && this.DataContext is ThemeViewModel vm)
            {
                var tag = button.Tag as string;
                if (tag != null && Enum.TryParse<ElementTheme>(tag, out var theme))
                {
                    vm.AppTheme = theme;
                }
            }
        }
    }
}
