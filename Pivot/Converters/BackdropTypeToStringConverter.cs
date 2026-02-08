using Microsoft.UI.Xaml.Data;
using System;
using System.Globalization;
using Pivot.Engine.Models; // BackdropType moved here

namespace Pivot.Converters
{
    public class BackdropTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is BackdropType backdropType)
            {
                return backdropType switch
                {
                    BackdropType.None => "なし",
                    BackdropType.Mica => "Mica",
                    BackdropType.MicaAlt => "Mica Alt",
                    BackdropType.Acrylic => "Acrylic",
                    BackdropType.AcrylicThin => "Acrylic Thin",
                    _ => value.ToString() ?? string.Empty
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // OneWay binding only - ConvertBack not used
            return value;
        }
    }
}
