using Microsoft.UI.Xaml.Data;
using System;

namespace Pivot.Converters
{
    public class BooleanToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isEnabled = false;
            if (value is bool b)
            {
                isEnabled = b;
            }
            // When enabled: 1.0 (fully visible), when disabled: 0.5 (grayed out)
            return isEnabled ? 1.0 : 0.5;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is double d)
            {
                return d >= 0.75; // Threshold for enabled state
            }
            return false;
        }
    }
}





