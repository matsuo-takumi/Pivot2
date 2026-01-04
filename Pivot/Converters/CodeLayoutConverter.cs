using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;

namespace Pivot.Converters
{
    public class CodeLayoutConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isGrid && isGrid)
            {
                return new UniformGridLayout 
                { 
                    MinItemWidth = 260, 
                    MinItemHeight = 160,
                    MinColumnSpacing = 12, 
                    MinRowSpacing = 12,
                    ItemsStretch = UniformGridLayoutItemsStretch.Fill
                };
            }
            else
            {
                return new StackLayout { Spacing = 8 };
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
