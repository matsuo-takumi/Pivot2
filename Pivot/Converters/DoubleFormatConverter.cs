using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace Pivot.Converters
{
    public sealed class DoubleFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double doubleValue && parameter is string format)
            {
                try
                {
                    return string.Format(CultureInfo.CurrentCulture, format, doubleValue);
                }
                catch
                {
                    // Fall through to safe return
                }
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}

