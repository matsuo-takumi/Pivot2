using Microsoft.UI.Xaml.Data;
using Pivot.Services;
using Pivot.Models;
using System;

namespace Pivot.Converters
{
    public class DirectoryCategoryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is string categoryString && Enum.TryParse(categoryString, out DirectoryCategory category))
            {
                return category;
            }
            return DirectoryCategory.Asset; // デフォルト値
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
