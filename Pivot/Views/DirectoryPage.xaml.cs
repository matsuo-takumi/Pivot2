using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.DependencyInjection;
using Pivot.ViewModels;

namespace Pivot.Views
{
    public sealed partial class DirectoryPage : Page
    {
        public DirectoryViewModel ViewModel { get; }

        public DirectoryPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<DirectoryViewModel>();
            this.DataContext = ViewModel;
            
            // マウスホイールによる横スクロールを有効化
            if (DirectoriesScrollViewer != null)
            {
                DirectoriesScrollViewer.AddHandler(
                    UIElement.PointerWheelChangedEvent, 
                    new PointerEventHandler(OnPointerWheelChanged), 
                    handledEventsToo: true);
            }
        }

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(DirectoriesScrollViewer).Properties.MouseWheelDelta;
            if (delta == 0) return;

            // スムーズな横スクロール（アニメーション有効）
            var newOffset = DirectoriesScrollViewer.HorizontalOffset - delta * 1.5;
            newOffset = System.Math.Clamp(newOffset, 0, DirectoriesScrollViewer.ScrollableWidth);
            DirectoriesScrollViewer.ChangeView(newOffset, null, null, disableAnimation: false);
            
            e.Handled = true;
        }
    }
}
