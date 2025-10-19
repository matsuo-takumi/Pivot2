using Microsoft.UI.Xaml.Data;
using System;

namespace Pivot.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return string.Empty;
            if (!(value is long)) return value.ToString();

            long bytes = (long)value;
            if (bytes < 1024) return $"{bytes} B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return $"{kb:N1} KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return $"{mb:N1} MB";
            double gb = mb / 1024.0;
            return $"{gb:N1} GB";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
