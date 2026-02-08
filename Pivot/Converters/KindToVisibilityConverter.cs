using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Pivot.Engine.Models;

namespace Pivot.Converters
{
	public sealed class KindToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value is not AssetKind kind || parameter is null) return Visibility.Collapsed;
			var p = parameter.ToString();
			if (string.IsNullOrWhiteSpace(p)) return Visibility.Collapsed;
			bool match = string.Equals(p, kind.ToString(), StringComparison.OrdinalIgnoreCase);
			return match ? Visibility.Visible : Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			// OneWay binding only - ConvertBack not used
			return value;
		}
	}
}


