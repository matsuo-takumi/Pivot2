using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
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

                // Convert mouse wheel delta to horizontal scroll offset
                // WinUI3's ChangeView with animation=false uses native smooth scrolling at optimal frame rate
                double scrollStep = delta * -2.0; // adjust multiplier for desired scroll distance
                var desiredOffset = scroller.HorizontalOffset + scrollStep;
                var maxOffset = scroller.ScrollableWidth;
                var newOffset = Math.Min(Math.Max(desiredOffset, 0), maxOffset);

                // Use WinUI3's native smooth scrolling (false = enable smooth animation)
                // The system automatically handles frame rate optimization
                scroller.ChangeView(newOffset, null, null, false);
                
                try { e.Handled = true; } catch { }
            }
            catch { }
        }
    }
}


