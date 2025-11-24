using Microsoft.UI.Xaml.Data;
using Pivot.Services;
using System;
using Microsoft.UI.Xaml;

namespace Pivot.Converters
{
    /// <summary>
    /// 選択状態に応じてPaddingを調整するコンバーター
    /// BorderThicknessが増えた分だけPaddingを減らして、コンテンツが押し込まれないようにする
    /// </summary>
    public class SelectionToPaddingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isSelected && isSelected)
            {
                double borderThickness = 2.0;
                
                // parameterとしてViewModelのSelectionBorderThicknessが渡される場合
                if (parameter is double thickness && thickness > 0)
                {
                    borderThickness = thickness;
                }
                else
                {
                    try
                    {
                        var settings = App.Current?.Services?.GetService(typeof(SettingsService)) as SettingsService;
                        if (settings != null)
                        {
                            borderThickness = settings.GetImageSelectionBorderThickness();
                        }
                    }
                    catch { }
                }
                
                // デフォルトのPadding（6）から、BorderThicknessの増加分を引く
                // BorderThicknessが2px増えたら、Paddingを2px減らす（左右上下それぞれ）
                var basePadding = 6.0;
                var paddingAdjustment = Math.Max(0, borderThickness - 1.0); // 非選択時のBorderThickness（1px）を考慮
                var adjustedPadding = Math.Max(2.0, basePadding - paddingAdjustment);
                return new Thickness(adjustedPadding);
            }
            // Not selected: デフォルトのPadding
            return new Thickness(6.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

