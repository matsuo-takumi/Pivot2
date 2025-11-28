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
            try
            {
                // Ensure mouse wheel scrolls horizontally
                var scroller = this.FindName("DirectoriesScrollViewer") as ScrollViewer;
                if (scroller != null)
                {
                    scroller.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(DirectoriesScrollViewer_PointerWheelChanged), true);
                }
            }
            catch { }
        }

        private void DirectoriesScrollViewer_PointerWheelChanged(object? sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                var scroller = this.FindName("DirectoriesScrollViewer") as ScrollViewer;
                if (scroller == null) return;
                var pt = e.GetCurrentPoint(scroller);
                var delta = pt.Properties.MouseWheelDelta; // typically +-120 units
                if (delta == 0) return;
                // Scroll horizontally by a scaled amount (reduced multiplier for smoother scrolling)
                double step = delta * -5.0; // more distance per wheel notch for wider travel, still smooth
                var newOffset = scroller.HorizontalOffset + step;
                var maxOffset = scroller.ScrollableWidth;
                if (newOffset < 0) newOffset = 0;
                if (newOffset > maxOffset) newOffset = maxOffset;
                // Use smooth scrolling animation (false = enable animation)
                scroller.ChangeView(newOffset, null, null, false);
                try { e.Handled = true; } catch { }
            }
            catch { }
        }
    }
}


