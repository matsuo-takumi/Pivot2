using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.ViewModels;
using System;

namespace Pivot.Views
{
    public sealed partial class PreferencePage : Page
    {
        private readonly ILogger<PreferencePage> _logger;

        public PreferencePage()
        {
            this.InitializeComponent();
            
            // Create PreferencePageViewModel for menu management
            this.DataContext = new PreferencePageViewModel();
            _logger = App.Current.Services.GetRequiredService<ILogger<PreferencePage>>();
            
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
                try
                {
                    _logger.LogInformation("Switching preference tab to {Tag}.", tag);
                    vm.SelectMenuItem(tag);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while selecting preference tab {Tag}.", tag);
                    throw;
                }
            }
        }
    }
}
