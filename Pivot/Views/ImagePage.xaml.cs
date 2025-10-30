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
    public sealed partial class ImagePage : Page
    {
        public ImageViewModel ViewModel { get; set; }
        private TagFilterViewModel? _tagVm = null;

        public ImagePage()
        {
            // Ensure TagFilterVm resource exists before XAML load
            _tagVm = App.Current.Services.GetService<TagFilterViewModel>();
            if (_tagVm != null) this.Resources["TagFilterVm"] = _tagVm;
            this.InitializeComponent();
            ViewModel = new ImageViewModel();
            this.DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // responsive handlers
            SizeChanged += ImagePage_SizeChanged;

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
                        _ = ViewModel.LoadFromDirectoriesAsync(dirs, 300);
                    }
                }
            }
            catch { }

            this.Unloaded += ImagePage_Unloaded;
        }

        private void TagToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string name && _tagVm != null)
            {
                _tagVm.ToggleTag(name);
            }
        }

        private void ImagePage_Unloaded(object sender, RoutedEventArgs e)
        {
            try { ViewModel?.CancelLoads(); } catch { }
        }

        private void ImagePage_SizeChanged(object sender, SizeChangedEventArgs e)
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
                        ViewModel.BuildJustifiedRows(width - 48, 8);
                    });
                }
                catch { }
            });
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImageViewModel.CurrentLayout))
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
                case LayoutType.List:
                    ItemsRepeaterMain.Layout = new StackLayout() { Orientation = Orientation.Vertical };
                    ItemsRepeaterMain.Visibility = Visibility.Visible;
                    MasonryColumnsControl.Visibility = Visibility.Collapsed;
                    JustifiedRowsControl.Visibility = Visibility.Collapsed;
                    break;

                case LayoutType.Grid:
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

                case LayoutType.Masonry:
                    UpdateResponsive(ActualWidth);
                    if (ViewModel != null)
                    {
                        double available = System.Math.Max(0, ActualWidth - 48);
                        int cols = ViewModel.MasonryColumnCount > 0 ? ViewModel.MasonryColumnCount : 1;
                        if (cols <= 0) cols = 1;
                        ViewModel.MasonryColumnWidth = System.Math.Floor(available / cols) - 16;
                    }
                    ViewModel.BuildMasonryColumns();
                    ItemsRepeaterMain.Visibility = Visibility.Collapsed;
                    MasonryColumnsControl.Visibility = Visibility.Visible;
                    JustifiedRowsControl.Visibility = Visibility.Collapsed;
                    break;

                case LayoutType.Justified:
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
