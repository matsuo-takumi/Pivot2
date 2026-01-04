using Microsoft.UI.Xaml.Data;
using Pivot.Services;
using System;
using Microsoft.UI.Xaml;

namespace Pivot.Converters
{
    public class SelectionToBorderThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // IMPORTANT: Return the SAME thickness for both selected and unselected states
            // to prevent layout shift. The visual difference is achieved through BorderBrush color only.
            double thickness = 2.0; // Default thickness
            
            try
            {
                var themeSettings = App.Current?.Services?.GetService(typeof(ThemeSettingsService)) as ThemeSettingsService;
                if (themeSettings != null)
                {
                    thickness = themeSettings.ImageSelectionBorderThickness;
                }
            }
            catch { }
            
            // Always return the same thickness regardless of selection state
            // This prevents layout reflow when selection changes
            return new Thickness(thickness);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // OneWay binding only - ConvertBack not used
            return null!;
        }
    }
}
