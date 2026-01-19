using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Pivot.Converters
{
    public class TagDeleteVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // If tag is "All", hide the delete button
            if (value is string tag && string.Equals(tag, "All", StringComparison.OrdinalIgnoreCase))
            {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
