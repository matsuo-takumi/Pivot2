using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using Microsoft.UI.Xaml;

namespace Pivot.Converters
{
    public class BoolToPinIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isPinned && isPinned)
            {
                return "\uE840"; // Pinned (Filled like)
            }
            return "\uE718"; // Pin (Outline)
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class BoolToPinColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isPinned && isPinned)
            {
                return Application.Current.Resources["AccentFillColorDefaultBrush"];
            }
            return Application.Current.Resources["TextFillColorSecondaryBrush"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
