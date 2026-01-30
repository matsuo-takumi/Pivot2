using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;

namespace Pivot.Converters
{
    public class BoolToSelectionModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isSelectionMode && isSelectionMode)
            {
                return ListViewSelectionMode.Extended;
            }
            return ListViewSelectionMode.None;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is ListViewSelectionMode mode)
            {
                return mode == ListViewSelectionMode.Multiple || mode == ListViewSelectionMode.Extended;
            }
            return false;
        }
    }
}
