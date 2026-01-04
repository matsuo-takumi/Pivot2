using Microsoft.UI.Xaml.Data;
using System;

namespace Pivot.Converters
{
    public class BoolToIconConverter : IValueConverter
    {
        // ViewAll (Grid)
        private const string GridIcon = "\uE80A"; 
        // List
        private const string ListIcon = "\uE8FD";

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isGrid && isGrid)
            {
                // Current is Grid, show List icon (to toggle to list)
                return ListIcon;
            }
            // Current is List, show Grid icon (to toggle to grid)
            return GridIcon;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
