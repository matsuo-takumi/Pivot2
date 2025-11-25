using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace Pivot.Converters
{
    public sealed class DateTimeFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime dateTime)
            {
                var format = parameter as string ?? "yyyy-MM-dd HH:mm";
                try
                {
                    return dateTime.ToString(format, CultureInfo.CurrentCulture);
                }
                catch
                {
                    return dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
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


