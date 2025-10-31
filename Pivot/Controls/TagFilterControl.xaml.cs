using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using Pivot.Services;
using Pivot.ViewModels;

namespace Pivot.Controls
{
    public sealed partial class TagFilterControl : UserControl
    {
        public string TabId { get; set; } = "Asset";

        private FilterService? _filterService;
        private SettingsService? _settings;

        public TagFilterControl()
        {
            this.InitializeComponent();
            this.Loaded += TagFilterControl_Loaded;
        }

        private void TagFilterControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _filterService = App.Current.Services.GetService(typeof(FilterService)) as FilterService;
                _settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                RefreshItems();
                if (_filterService != null) _filterService.PropertyChanged += (s, ev) => RefreshItems();
            }
            catch { }
        }

        private void RefreshItems()
        {
            if (_filterService == null || _settings == null) return;
            var visible = _settings.GetVisibleFiltersForTab(TabId) ?? new List<Guid>();
            var items = _filterService.Filters.Where(f => visible.Contains(f.Id)).ToList();
            TagItems.ItemsSource = items;
            // set toggle state
            foreach (var container in TagItems.Items)
            {
                // nothing here; toggles set by Tag_Toggled handler on interaction
            }
        }

        private async void Tag_Toggled(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleButton tb) || !(tb.DataContext is FilterViewModel vm)) return;
            try
            {
                if (_filterService == null || _settings == null) return;
                if (tb.IsChecked == true)
                {
                    _filterService.AddSelectedFilter(vm.Id);
                }
                else
                {
                    _filterService.RemoveSelectedFilter(vm.Id);
                }
                // persist selection for this tab
                await _settings.SetSelectedFiltersForTabAsync(TabId, _filterService.SelectedFilterIds.ToList());
            }
            catch { }
        }
    }
}


