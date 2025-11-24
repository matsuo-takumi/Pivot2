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
            if (value is bool isSelected && isSelected)
            {
                try
                {
                    var settings = App.Current?.Services?.GetService(typeof(SettingsService)) as SettingsService;
                    if (settings != null)
                    {
                        var thickness = settings.GetImageSelectionBorderThickness();
                        return new Thickness(thickness);
                    }
                }
                catch { }
                
                // Fallback: Default 1px border
                return new Thickness(1.0);
            }
            // Not selected: No border
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

