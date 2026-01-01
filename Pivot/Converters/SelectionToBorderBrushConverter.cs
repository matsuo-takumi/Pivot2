using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
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
                // Selected: Default semi-transparent blue
                return new SolidColorBrush(Color.FromArgb(200, 0, 120, 212));
            }
            // Not selected: 薄いグレーのボーダー（カードの境界が見えるように）
            return new SolidColorBrush(Color.FromArgb(32, 128, 128, 128)); // #20808080
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

