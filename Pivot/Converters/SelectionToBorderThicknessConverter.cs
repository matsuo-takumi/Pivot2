using Microsoft.UI.Xaml.Data;
using Pivot.Services;
using System;
using Microsoft.UI.Xaml;

namespace Pivot.Converters
{
    public class SelectionToBorderThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isSelected && isSelected)
            {
                // parameterとしてViewModelのSelectionBorderThicknessが渡される場合
                if (parameter is double thickness && thickness > 0)
                {
                    return new Thickness(thickness);
                }
                
                try
                {
                    var settings = App.Current?.Services?.GetService(typeof(SettingsService)) as SettingsService;
                    if (settings != null)
                    {
                        var thicknessValue = settings.GetImageSelectionBorderThickness();
                        return new Thickness(thicknessValue);
                    }
                }
                catch { }
                
                // Fallback: Default 2px border
                return new Thickness(2.0);
            }
            // Not selected: 薄いボーダーでカードの境界を表示
            return new Thickness(1.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

