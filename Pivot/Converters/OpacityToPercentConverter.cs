using Microsoft.UI.Xaml.Data;
using System;

namespace Pivot.Converters
{
    public class OpacityToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double opacity)
            {
                // Convert 0-1 range to 0-100 percent
                return Math.Round(opacity * 100.0, 0).ToString("F0");
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

