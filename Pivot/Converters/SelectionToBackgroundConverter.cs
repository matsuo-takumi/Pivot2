using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace Pivot.Converters
{
    public class SelectionToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // Background remains unchanged (no highlight background)
            // Only border is used for selection indication
            return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)); // Transparent
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

