using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Pivot.Services;
using System;
using Windows.UI;

namespace Pivot.Converters
{
    public class SelectionToBorderBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isSelected && isSelected)
            {
                // Selected: DodgerBlue (User preference later)
                // For now hardcode standard blue to ensure visibility
                return new SolidColorBrush(Color.FromArgb(255, 30, 144, 255)); // DodgerBlue
            }
            // Not selected: Transparent
            return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // OneWay binding only - ConvertBack not used
            return null!;
        }
    }
}
