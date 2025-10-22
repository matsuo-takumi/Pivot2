using Microsoft.UI.Xaml.Controls;
using Pivot.ViewModels;
using Microsoft.UI.Xaml;

namespace Pivot.Views
{
    public sealed partial class PreferencePage : Page
    {
        public PreferencePage()
        {
            this.InitializeComponent();
            
            // Create PreferencePageViewModel for menu management
            this.DataContext = new PreferencePageViewModel();
            
            // Register for NavigationView events
            this.Loaded += PreferencePage_Loaded;
        }

        private void PreferencePage_Loaded(object sender, RoutedEventArgs e)
        {
            nvSample.PaneOpening += NvSample_PaneOpening;
            nvSample.PaneClosing += NvSample_PaneClosing;
            nvSample.PaneOpened += NvSample_PaneOpened;
            nvSample.PaneClosed += NvSample_PaneClosed;
        }

        private void NvSample_PaneOpening(NavigationView sender, object args)
        {
            // Ensure pane opens properly
        }

        private void NvSample_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        {
            // Ensure pane closes properly
        }

        private void NvSample_PaneOpened(NavigationView sender, object args)
        {
            // Pane opened
        }

        private void NvSample_PaneClosed(NavigationView sender, object args)
        {
            // Pane closed
        }

        private void nvSample_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer == null) return;
            var tag = args.SelectedItemContainer.Tag as string;
            if (string.IsNullOrEmpty(tag)) return;

            if (this.DataContext is PreferencePageViewModel vm)
            {
                vm.SelectMenuItem(tag);
            }
        }
    }
}
