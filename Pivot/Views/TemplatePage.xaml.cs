using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Pivot.ViewModels;
using System.ComponentModel;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml;

namespace Pivot.Views
{
	public sealed partial class TemplatePage : Page
	{
		public TemplateViewModel ViewModel { get; set; }

		public TemplatePage()
		{
			this.InitializeComponent();
			ViewModel = new TemplateViewModel();
			this.DataContext = ViewModel;
			ViewModel.PropertyChanged += ViewModel_PropertyChanged;

			// responsive handlers
			SizeChanged += TemplatePage_SizeChanged;

			// 初期レイアウトを適用
			ApplyLayout(ViewModel.CurrentLayout);
		}

		private void TemplatePage_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			UpdateResponsive(e.NewSize.Width);
		}

		private void UpdateResponsive(double width)
		{
			if (width <= 0) return;
			// columns based on available width (approx 220 item + spacing)
			int columns = (int)System.Math.Max(1, System.Math.Floor((width - 48) / 220));
			if (columns != ViewModel.MasonryColumnCount)
			{
				ViewModel.MasonryColumnCount = columns;
			}

			// rebuild justified rows for current width
			ViewModel.BuildJustifiedRows(width - 48, 8);
		}

		private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(TemplateViewModel.CurrentLayout))
			{
				if (ViewModel != null)
				{
					ApplyLayout(ViewModel.CurrentLayout);
				}
			}
		}

		private void ApplyLayout(LayoutType layout)
		{
			switch (layout)
			{
				case LayoutType.List: // index 0 -> List
					ItemsRepeaterMain.Layout = new StackLayout() { Orientation = Orientation.Vertical };
					ItemsRepeaterMain.Visibility = Visibility.Visible;
					MasonryColumnsControl.Visibility = Visibility.Collapsed;
					JustifiedRowsControl.Visibility = Visibility.Collapsed;
					break;

				case LayoutType.Grid: // index 1 -> Grid
					ItemsRepeaterMain.Layout = new UniformGridLayout
					{
						MinItemWidth = 220,
						MinItemHeight = 160,
						MinRowSpacing = 8,
						MinColumnSpacing = 8
					};
					ItemsRepeaterMain.Visibility = Visibility.Visible;
					MasonryColumnsControl.Visibility = Visibility.Collapsed;
					JustifiedRowsControl.Visibility = Visibility.Collapsed;
					break;

				case LayoutType.Masonry: // index 2 -> Masonry (本格実装)
					UpdateResponsive(ActualWidth);
					ViewModel.BuildMasonryColumns();
					ItemsRepeaterMain.Visibility = Visibility.Collapsed;
					MasonryColumnsControl.Visibility = Visibility.Visible;
					JustifiedRowsControl.Visibility = Visibility.Collapsed;
					break;

				case LayoutType.Justified: // index 3 -> Justified
					UpdateResponsive(ActualWidth);
					ItemsRepeaterMain.Visibility = Visibility.Collapsed;
					MasonryColumnsControl.Visibility = Visibility.Collapsed;
					JustifiedRowsControl.Visibility = Visibility.Visible;
					break;

				default:
					ItemsRepeaterMain.Layout = new UniformGridLayout
					{
						MinItemWidth = 220,
						MinItemHeight = 160,
						MinRowSpacing = 8,
						MinColumnSpacing = 8
					};
					ItemsRepeaterMain.Visibility = Visibility.Visible;
					MasonryColumnsControl.Visibility = Visibility.Collapsed;
					JustifiedRowsControl.Visibility = Visibility.Collapsed;
					break;
			}
		}
	}
}
