using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Pivot.ViewModels;
using Microsoft.UI.Xaml.Media;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Pivot.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AssetPage : Page
    {
        private AssetViewModel? _viewModel;
        private ScrollViewer? _scrollViewer;

        public AssetPage()
        {
            this.InitializeComponent();
            this.DataContext = App.Current.Services.GetRequiredService<AssetViewModel>();
            _viewModel = this.DataContext as AssetViewModel;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 表示モードに応じて対象の ScrollViewer を選択
            _scrollViewer = (_viewModel != null && _viewModel.IsGridMode)
                ? FindByName(this, "GridScrollViewer") as ScrollViewer
                : FindByName(this, "ListScrollViewer") as ScrollViewer;
            if (_scrollViewer != null)
            {
                _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // スクロールイベント購読を解除
            if (_scrollViewer != null)
            {
                _scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
            }
        }

        private void ScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_scrollViewer == null || _viewModel == null)
                return;

            // スクロール位置をチェック（下端に近いかどうか）
            double scrollPercentage = _scrollViewer.ScrollableHeight > 0
                ? (_scrollViewer.VerticalOffset / _scrollViewer.ScrollableHeight) * 100
                : 0;

            // 下端から 80% 以上の位置に達したら次のバッチをロード
            if (scrollPercentage >= 80)
            {
                _ = _viewModel.LoadMoreAssetsAsync();
            }
        }

        /// <summary>
        /// ScrollViewerを再帰的に探索して取得
        /// </summary>
        private DependencyObject? FindByName(DependencyObject obj, string name)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is FrameworkElement fe && fe.Name == name)
                {
                    return child;
                }
                var result = FindByName(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}
