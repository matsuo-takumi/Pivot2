using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using Pivot.Services;
using Pivot.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;

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
                // Set items panel orientation based on TabId: default horizontal in XAML, but use vertical for Code when needed
                try
                {
                    if (string.Equals(TabId, "Code", StringComparison.OrdinalIgnoreCase))
                    {
                        // Build a vertical ItemsPanelTemplate dynamically to avoid missing resource keys
                        var xaml = "<ItemsPanelTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
                                   "<StackPanel Orientation=\"Vertical\"/>" +
                                   "</ItemsPanelTemplate>";
                        try
                        {
                            var tpl = Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml) as ItemsPanelTemplate;
                            if (tpl != null) TagItems.ItemsPanel = tpl;
                        }
                        catch { }
                    }
                }
                catch { }

                RefreshItems();
                if (_filterService != null) _filterService.PropertyChanged += (s, ev) => RefreshItems();
            }
            catch { }
        }

        private void RefreshItems()
        {
            if (_settings == null) return;

            // If used for Code tab, prefer tags configured in user Preferences (SettingsService)
            if (string.Equals(TabId, "Code", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var user = _settings.GetUserSettings();
                    if (user != null && user.CodeFilters != null && user.CodeFilters.Count > 0)
                    {
                        var prefTags = user.CodeFilters.Select(f => new { Name = f.Name }).ToList();
                        TagItems.ItemsSource = prefTags;
                        return;
                    }

                    // Fallback: try code repository and derive tags from snippets
                    var repo = App.Current.Services.GetService(typeof(Pivot.CodeModule.Services.ICodeRepository)) as Pivot.CodeModule.Services.ICodeRepository;
                    if (repo == null)
                    {
                        TagItems.ItemsSource = null;
                        return;
                    }

                    var tags = repo.GetAllTags().Select(t => new { Name = t.Name }).ToList();
                    if (tags == null || tags.Count == 0)
                    {
                        try
                        {
                            var all = repo.GetAll() ?? Enumerable.Empty<Pivot.CodeModule.Models.CodeFile>();
                            var derived = all.SelectMany(f => (f.Tags ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()))
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Select(n => new { Name = n })
                                .ToList();
                            tags = derived;
                        }
                        catch { }
                    }
                    TagItems.ItemsSource = tags;
                    return;
                }
                catch { TagItems.ItemsSource = null; return; }
            }

            // Default behavior: use FilterService for Asset/other tabs
            if (_filterService == null) return;
            var visible = _settings.GetVisibleFiltersForTab(TabId) ?? new List<Guid>();
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


