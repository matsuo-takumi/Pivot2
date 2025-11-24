using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Pivot.ViewModels;
using System.ComponentModel;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Services;
using Pivot.Models;

namespace Pivot.Views
{
    public sealed partial class AssetPage : Page
    {
        public AssetViewModel ViewModel { get; set; }

        public AssetPage()
        {
            this.InitializeComponent();
            ViewModel = new AssetViewModel();
            this.DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            SizeChanged += AssetPage_SizeChanged;

            ApplyLayout(ViewModel.CurrentLayout);

            try
            {
                var settings = App.Current.Services.GetService<SettingsService>();
                if (settings != null)
                {
                    var dirs = settings.GetUserSettings().AssetDirectories;
                    if (dirs != null && dirs.Count > 0)
                    {
                        _ = ViewModel.LoadFromDirectoriesAsync(dirs, 300);
                    }
                }
            }
            catch { }

            this.Unloaded += AssetPage_Unloaded;
        }

        private void AssetPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try { ViewModel?.CancelLoads(); } catch { }
        }

        private void AssetPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsive(e.NewSize.Width);
        }

        private System.Threading.CancellationTokenSource? _resizeCts;

        private void UpdateResponsive(double width)
        {
            if (width <= 0) return;
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
                    });
                }
                catch { }
            });
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AssetViewModel.CurrentLayout))
            {
                if (ViewModel != null)
                {
                    ApplyLayout(ViewModel.CurrentLayout);
                }
            }
        }

        private void ApplyLayout(LayoutType layout)
        {
            var itemsRepeater = this.FindName("ItemsRepeaterMain") as Microsoft.UI.Xaml.Controls.ItemsRepeater;
            var masonryControl = this.FindName("MasonryColumnsControl") as Microsoft.UI.Xaml.Controls.ItemsControl;

            switch (layout)
            {
                case LayoutType.List:
                    if (itemsRepeater != null)
                    {
                        itemsRepeater.Layout = new Microsoft.UI.Xaml.Controls.StackLayout() { Orientation = Orientation.Vertical };
                        itemsRepeater.Visibility = Visibility.Visible;
                    }
                    if (masonryControl != null) masonryControl.Visibility = Visibility.Collapsed;
                    break;

                case LayoutType.Grid:
                    if (itemsRepeater != null)
                    {
                        itemsRepeater.Layout = new Microsoft.UI.Xaml.Controls.UniformGridLayout
                        {
                            MinItemWidth = 220,
                            MinItemHeight = 160,
                            MinRowSpacing = 8,
                            MinColumnSpacing = 8
                        };
                        itemsRepeater.Visibility = Visibility.Visible;
                    }
                    if (masonryControl != null) masonryControl.Visibility = Visibility.Collapsed;
                    break;

                case LayoutType.Masonry:
                    UpdateResponsive(ActualWidth);
                    if (ViewModel != null)
                    {
                        double available = System.Math.Max(0, ActualWidth - 48);
                        int cols = ViewModel.MasonryColumnCount > 0 ? ViewModel.MasonryColumnCount : 1;
                        if (cols <= 0) cols = 1;
                        ViewModel.MasonryColumnWidth = System.Math.Floor(available / cols) - 16;
                    }
                    // build masonry columns from currently displayed (filtered) assets so layout change preserves filtering
                    ViewModel.BuildMasonryColumns(ViewModel.DisplayedAssets);
                    if (itemsRepeater != null) itemsRepeater.Visibility = Visibility.Collapsed;
                    if (masonryControl != null) masonryControl.Visibility = Visibility.Visible;
                    break;

                
                default:
                    if (itemsRepeater != null)
                    {
                        itemsRepeater.Layout = new Microsoft.UI.Xaml.Controls.UniformGridLayout
                        {
                            MinItemWidth = 220,
                            MinItemHeight = 160,
                            MinRowSpacing = 8,
                            MinColumnSpacing = 8
                        };
                        itemsRepeater.Visibility = Visibility.Visible;
                    }
                    if (masonryControl != null) masonryControl.Visibility = Visibility.Collapsed;
                    break;
            }
        }
    }
}
