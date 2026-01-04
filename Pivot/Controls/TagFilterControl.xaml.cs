using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pivot.Services;
using Pivot.ViewModels;
using Pivot.CodeModule.Models;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;

namespace Pivot.Controls
{
    public sealed partial class TagFilterControl : UserControl
    {
        public string TabId { get; set; } = "Asset";

        private FilterService? _filterService;
        private FilterSettingsService? _filterSettings;

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
                _filterSettings = App.Current.Services.GetService(typeof(FilterSettingsService)) as FilterSettingsService;
                // Keep default horizontal orientation defined in XAML for all tabs (including Code)

                RefreshItems();
                if (_filterService != null) _filterService.PropertyChanged += (s, ev) => RefreshItems();
            }
            catch { }
        }

        private void RefreshItems()
        {
            if (_filterSettings == null) return;

            // If used for Code tab, prefer tags configured in user Preferences (FilterSettingsService)
            if (string.Equals(TabId, "Code", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var codeFilters = _filterSettings.GetCodeFilters();
                    if (codeFilters != null && codeFilters.Count > 0)
                    {
                        var prefTags = codeFilters.Select(f => new TagItem { Name = f.Name, IsSelected = false }).ToList();
                        TagItems.ItemsSource = prefTags;
                        return;
                    }

                    // Fallback: try code service and derive tags using CodeTagService
                    var codeService = App.Current.Services.GetService(typeof(Pivot.Services.CodeService)) as Pivot.Services.CodeService;
                    var codeTagService = App.Current.Services.GetService(typeof(CodeTagService)) as CodeTagService;

                    if (codeService != null && codeTagService != null)
                    {
                        TagItems.ItemsSource = null;
                        _ = LoadCodeTagsAsync(codeService, codeTagService);
                        return;
                    }

                    TagItems.ItemsSource = null;
                    return;
                }
                catch { TagItems.ItemsSource = null; return; }
            }

            // Default behavior: use FilterService for Asset/other tabs
            if (_filterService == null) return;
            var visible = _filterSettings?.GetVisibleFiltersForTab(TabId) ?? new List<Guid>();
            var items = _filterService.Filters.Where(f => visible.Contains(f.Id)).ToList();
            TagItems.ItemsSource = items;
            // No further initialization; Tag_Toggled handles state changes
        }

        private async void Tag_Toggled(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleButton tb)) return;
            try
            {
                // If Code tab, publish a tag selection message so CodeViewModel can filter
                if (string.Equals(TabId, "Code", StringComparison.OrdinalIgnoreCase))
                {
                    var tagName = tb.Content?.ToString() ?? string.Empty;
                    var messenger = App.Current.Services.GetService(typeof(IMessenger)) as IMessenger;
                    messenger?.Send(new TagSelectionMessage(TabId, tagName, tb.IsChecked == true));
                    return;
                }

                // Default behavior: use FilterService (e.g., Asset tab)
                if (!(tb.DataContext is FilterViewModel vm)) return;
                if (_filterService == null || _filterSettings == null) return;
                if (tb.IsChecked == true)
                {
                    _filterService.AddSelectedFilter(vm.Id);
                }
                else
                {
                    _filterService.RemoveSelectedFilter(vm.Id);
                }
                // persist selection for this tab
                await _filterSettings.SetSelectedFiltersForTabAsync(TabId, _filterService.SelectedFilterIds.ToList());
            }
            catch { }
        }

        /// <summary>
        /// Async helper to load code tags without blocking the UI thread.
        /// </summary>
        private async Task LoadCodeTagsAsync(Pivot.Services.CodeService codeService, CodeTagService tagService)
        {
            try
            {
                var all = await codeService.GetAllSnippetsAsync();
                // Use CodeTagService to logic
                var names = tagService.CollectAvailableTags(all);

                var items = names.Select(n => new TagItem { Name = n, IsSelected = false }).ToList();
                TagItems.ItemsSource = items;
            }
            catch { TagItems.ItemsSource = null; }
        }
    }
}
