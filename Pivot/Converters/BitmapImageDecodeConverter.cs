using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Pivot.Converters
{
	public sealed class BitmapImageDecodeConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			var s = value as string;
			if (string.IsNullOrWhiteSpace(s)) return null!;
			try
			{
				var uri = new Uri(s, UriKind.RelativeOrAbsolute);
				var bmp = new BitmapImage(uri);
				if (parameter != null && int.TryParse(parameter.ToString(), out var px) && px > 0)
				{
					bmp.DecodePixelWidth = px;
				}
				return bmp;
			}
			catch
			{
				return null!;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new NotImplementedException();
		}
	}
}


