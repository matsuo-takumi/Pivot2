using Microsoft.UI.Xaml.Data;
using System;

namespace Pivot.CodeModule.Converters
{
    public class CardPanelSizeConverter : IValueConverter
    {
        // Convert CardPanel ActualWidth into per-item width for ItemsWrapGrid
        // ConverterParameter (optional): number of columns (default 4)
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                if (!(value is double panelWidth)) return 200.0;

                int columns = 4;
                if (parameter is string s && int.TryParse(s, out int p)) columns = Math.Max(1, p);

                // reserve small gaps/margins: approximate per-item margin of 16
                double totalMargin = (columns + 1) * 8; // assumes Margin="8" on items
                double available = Math.Max(0, panelWidth - totalMargin - 16); // extra padding guard

                double itemWidth = available / columns;

                // enforce sensible min/max
                if (itemWidth < 120) itemWidth = 120;
                if (itemWidth > 720) itemWidth = 720;

                return itemWidth;
            }
            catch
            {
                return 200.0;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}


