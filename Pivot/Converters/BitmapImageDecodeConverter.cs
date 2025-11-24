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
				
				// デコードサイズを設定してメモリ使用量を削減（4K画像の最適化）
				if (parameter != null && int.TryParse(parameter.ToString(), out var px) && px > 0)
				{
					bmp.DecodePixelWidth = px;
					// アスペクト比を維持するため、高さも比例して設定
					// ただし、明示的な高さ指定がない場合は幅のみで十分
				}
				else
				{
					// パラメータがない場合でも、デフォルトで最大幅を制限（メモリ保護）
					bmp.DecodePixelWidth = 800; // デフォルト最大幅
				}
				
				// 画像の読み込み最適化設定
				bmp.CreateOptions = BitmapCreateOptions.None;
				
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


