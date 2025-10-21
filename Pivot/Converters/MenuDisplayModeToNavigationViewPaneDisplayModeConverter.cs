using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Pivot.Models;
using System;

namespace Pivot.Converters
{
    public class MenuDisplayModeToNavigationViewPaneDisplayModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MenuDisplayMode mode)
            {
                return mode == MenuDisplayMode.Wide ? 
                    NavigationViewPaneDisplayMode.Left : 
                    NavigationViewPaneDisplayMode.LeftCompact;
            }
            return NavigationViewPaneDisplayMode.LeftCompact;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is NavigationViewPaneDisplayMode displayMode)
            {
                return displayMode == NavigationViewPaneDisplayMode.Left ? 
                    MenuDisplayMode.Wide : 
                    MenuDisplayMode.Compact;
            }
            return MenuDisplayMode.Compact;
        }
    }
}
