using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Pivot.ViewModels;
using System.ComponentModel;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Services;

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

			// 自動ロード: 設定に保存された ImageDirectories があればテスト用に読み込む（安全策: try/catch）
			try
			{
				var settings = App.Current.Services.GetService<SettingsService>();
				if (settings != null)
				{
					var dirs = settings.GetUserSettings().ImageDirectories;
					if (dirs != null && dirs.Count > 0)
					{
						// fire-and-forget loading (safe for quick testing)
						_ = ViewModel.LoadFromDirectoriesAsync(dirs, 300);
					}
				}
			}
			catch { }

			this.Unloaded += TemplatePage_Unloaded;
		}

		private void TemplatePage_Unloaded(object sender, RoutedEventArgs e)
		{
			try { ViewModel?.CancelLoads(); } catch { }
		}

		private void TemplatePage_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			UpdateResponsive(e.NewSize.Width);
		}

		private System.Threading.CancellationTokenSource? _resizeCts;

		private void UpdateResponsive(double width)
		{
			if (width <= 0) return;
			// debounce heavy reactive layout rebuilds
			try { _resizeCts?.Cancel(); } catch { }
			_resizeCts = new System.Threading.CancellationTokenSource();
			var ct = _resizeCts.Token;
			_ = System.Threading.Tasks.Task.Run(async () =>
			{
				try
				{
					await System.Threading.Tasks.Task.Delay(150, ct);
					if (ct.IsCancellationRequested) return;
					var columns = (int)System.Math.Max(1, System.Math.Floor((width - 48) / 220));
					DispatcherQueue.TryEnqueue(() =>
					{
						if (ViewModel == null) return;
						if (columns != ViewModel.MasonryColumnCount)
						{
							ViewModel.MasonryColumnCount = columns;
						}
						ViewModel.BuildJustifiedRows(width - 48, 8);
					});
				}
				catch { }
			});
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
					// set column width based on available width and desired column count
					if (ViewModel != null)
					{
						// compute width per column including spacing/padding assumptions
						double available = System.Math.Max(0, ActualWidth - 48); // keep same margin logic
						int cols = ViewModel.MasonryColumnCount > 0 ? ViewModel.MasonryColumnCount : 1;
						if (cols <= 0) cols = 1;
						ViewModel.MasonryColumnWidth = System.Math.Floor(available / cols) - 16; // subtract margins
					}
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
