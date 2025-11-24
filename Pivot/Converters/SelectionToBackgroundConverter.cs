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
            if (value is bool isSelected && isSelected)
            {
                // 選択時: わずかに明るい背景（ハイライト効果）
                return new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)); // #1EFFFFFF
            }
            // 非選択時: 半透明の背景（カードが見えるように）
            return new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)); // #10FFFFFF
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

