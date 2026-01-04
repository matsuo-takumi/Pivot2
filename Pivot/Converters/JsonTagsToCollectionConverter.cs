using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Pivot.Converters
{
    public class JsonTagsToCollectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string json && !string.IsNullOrEmpty(json))
            {
                try
                {
                    var tags = JsonSerializer.Deserialize<List<string>>(json);
                    return tags ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            return new List<string>();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
