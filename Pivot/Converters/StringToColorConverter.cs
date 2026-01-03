using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;

namespace Pivot.Converters
{
    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string colorString)
            {
                try
                {
                    // 例: "#RRGGBB" または "Red" のような文字列を Color に変換
                    // ColorConverter を直接使用することはできないため、ColorHelper を利用
                    if (colorString.StartsWith("#"))
                    {
                        return ColorHelper.FromArgb(255, byte.Parse(colorString.Substring(1, 2), System.Globalization.NumberStyles.HexNumber),
                                                    byte.Parse(colorString.Substring(3, 2), System.Globalization.NumberStyles.HexNumber),
                                                    byte.Parse(colorString.Substring(5, 2), System.Globalization.NumberStyles.HexNumber));
                    }
                    else // 名前付きカラーを扱う場合は、switch文などでの対応が必要になる
                    {
                        switch (colorString.ToLower())
                        {
                            case "skyblue": return Colors.SkyBlue;
                            case "blue": return Colors.Blue;
                            case "green": return Colors.Green;
                            case "red": return Colors.Red;
                            case "white": return Colors.White;
                            case "black": return Colors.Black;
                            default: return Colors.Transparent; // 不明な場合は透明
                        }
                    }
                }
                catch
                {
                    return Colors.Transparent; // 変換失敗時は透明
                }
            }
            return Colors.Transparent; // 値が文字列でない場合も透明
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // Convert Color back to hex string
            if (value is Windows.UI.Color color)
            {
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            return string.Empty;
        }
    }
}
