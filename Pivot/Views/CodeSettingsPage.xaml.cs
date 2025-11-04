using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Services;
using System.Linq;
using Pivot.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pivot.Views
{
    public sealed partial class CodeSettingsPage : Page
    {
        private readonly SettingsService? _settings;

        public CodeSettingsPage()
        {
            this.InitializeComponent();
            _settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
            LoadFilters();
            LoadCategories();
        }

        private void LoadFilters()
        {
            try
            {
                var settings = _settings?.GetUserSettings();
                if (settings != null)
                {
                    var visible = _settings.GetVisibleFiltersForTab("Code");
                    var viewItems = settings.CodeFilters.Select(f => new CodeFilterViewItem
                    {
                        Id = f.Id,
                        Name = f.Name,
                        Visible = visible != null && visible.Contains(f.Id)
                    }).ToList();

                    FiltersList.ItemsSource = viewItems;
                }
            }
            catch { }
        }

        private async void AddFilter_Click(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox { Header = "Name" };

            var panel = new StackPanel();
            panel.Children.Add(nameBox);

            var dialog = new ContentDialog()
            {
                Title = "Add Code Filter",
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                Content = panel
            };

            dialog.XamlRoot = this.XamlRoot;
            try { dialog.RequestedTheme = _settings?.GetTheme() ?? ElementTheme.Default; } catch { }

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    var name = nameBox.Text?.Trim() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(name) && _settings != null)
                    {
                        var user = _settings.GetUserSettings();
                        var list = user.CodeFilters;
                        var nf = new CustomFilter { Name = name };
                        list.Add(nf);
                        await _settings.SetCodeFiltersAsync(list);
                        LoadFilters();
                    }
                }
                catch { }
            }
        }

        private async void EditFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is Guid id && _settings != null)
            {
                var user = _settings.GetUserSettings();
                var target = user.CodeFilters.FirstOrDefault(f => f.Id == id);
                if (target == null) return;

                var nameBox = new TextBox { Header = "Name", Text = target.Name };
                var panel = new StackPanel();
                panel.Children.Add(nameBox);

                var dialog = new ContentDialog()
                {
                    Title = "Edit Code Filter",
                    PrimaryButtonText = "Save",
                    CloseButtonText = "Cancel",
                    Content = panel
                };

                dialog.XamlRoot = this.XamlRoot;
                try { dialog.RequestedTheme = _settings?.GetTheme() ?? ElementTheme.Default; } catch { }

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    try
                    {
                        var name = nameBox.Text?.Trim() ?? string.Empty;
                        target.Name = name;
                        await _settings.SetCodeFiltersAsync(user.CodeFilters);
                        LoadFilters();
                    }
                    catch { }
                }
            }
        }

        private async void DeleteFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is Guid id && _settings != null)
            {
                var user = _settings.GetUserSettings();
                var target = user.CodeFilters.FirstOrDefault(f => f.Id == id);
                if (target == null) return;
                if (target.IsBuiltIn) return; // do not delete built-in
                user.CodeFilters.Remove(target);
                await _settings.SetCodeFiltersAsync(user.CodeFilters);
                LoadFilters();
            }
        }

        private async void FilterVisible_Toggled(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleSwitch ts) || !(ts.Tag is Guid id) || _settings == null) return;
            try
            {
                var current = _settings.GetVisibleFiltersForTab("Code") ?? new List<Guid>();
                if (ts.IsOn)
                {
                    if (!current.Contains(id)) current.Add(id);
                }
                else
                {
                    current.Remove(id);
                }
                await _settings.SetVisibleFiltersForTabAsync("Code", current);
                LoadFilters();
            }
            catch { }
        }

        private async void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            try
            {
                await _settings.SetCodeFiltersAsync(new List<CustomFilter>
                {
                    new CustomFilter { Name = "C#", IsBuiltIn=true },
                    new CustomFilter { Name = "Python", IsBuiltIn=true },
                    new CustomFilter { Name = "JavaScript", IsBuiltIn=true },
                    new CustomFilter { Name = "HTML", IsBuiltIn=true },
                    new CustomFilter { Name = "CSS", IsBuiltIn=true },
                    new CustomFilter { Name = "SQL", IsBuiltIn=true },
                    new CustomFilter { Name = "Markdown", IsBuiltIn=true },
                    new CustomFilter { Name = "Other", IsBuiltIn=true }
                });
                LoadFilters();
            }
            catch { }
        }

        private class CodeFilterViewItem
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool Visible { get; set; } = true;
        }

        private void LoadCategories()
        {
            try
            {
                var cats = _settings?.GetCodeCategories() ?? new List<CodeCategory>();
                var filters = _settings?.GetCodeFilters() ?? new List<CustomFilter>();
                var items = cats.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                    .Select(c => new CodeCategoryViewItem
                    {
                        Id = c.Id,
                        Name = c.Name,
                        FilterIds = c.FilterIds?.ToList() ?? new List<Guid>(),
                        FilterNames = string.Join(", ", (c.FilterIds ?? new List<Guid>()).Select(id => filters.FirstOrDefault(f => f.Id == id)?.Name).Where(n => !string.IsNullOrWhiteSpace(n)))
                    }).ToList();
                var list = this.FindName("CategoriesList") as ItemsControl;
                if (list != null) list.ItemsSource = items;
            }
            catch { }
        }

        private async void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox { Header = "Name" };
            var dlg = new ContentDialog { Title = "Add Category", PrimaryButtonText = "Add", CloseButtonText = "Cancel", Content = nameBox };
            dlg.XamlRoot = this.XamlRoot;
            try { dlg.RequestedTheme = _settings?.GetTheme() ?? ElementTheme.Default; } catch { }
            var result = await dlg.ShowAsync();
            if (result != ContentDialogResult.Primary || _settings == null) return;
            try
            {
                var cats = _settings.GetCodeCategories();
                var name = nameBox.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) return;
                cats.Add(new CodeCategory { Name = name });
                await _settings.SetCodeCategoriesAsync(cats);
                LoadCategories();
            }
            catch { }
        }

        private async void RenameCategory_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe) || !(fe.Tag is Guid id) || _settings == null) return;
            try
            {
                var cats = _settings.GetCodeCategories();
                var target = cats.FirstOrDefault(c => c.Id == id);
                if (target == null) return;
                var nameBox = new TextBox { Header = "Name", Text = target.Name };
                var dlg = new ContentDialog { Title = "Rename Category", PrimaryButtonText = "Save", CloseButtonText = "Cancel", Content = nameBox };
                dlg.XamlRoot = this.XamlRoot;
                try { dlg.RequestedTheme = _settings.GetTheme(); } catch { }
                var result = await dlg.ShowAsync();
                if (result != ContentDialogResult.Primary) return;
                target.Name = nameBox.Text?.Trim() ?? target.Name;
                await _settings.SetCodeCategoriesAsync(cats);
                LoadCategories();
            }
            catch { }
        }

        private async void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe) || !(fe.Tag is Guid id) || _settings == null) return;
            try
            {
                var cats = _settings.GetCodeCategories();
                var tgt = cats.FirstOrDefault(c => c.Id == id);
                if (tgt == null) return;
                cats.Remove(tgt);
                await _settings.SetCodeCategoriesAsync(cats);
                LoadCategories();
            }
            catch { }
        }

        private async void AssignFilters_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe) || !(fe.Tag is Guid id) || _settings == null) return;
            try
            {
                var cats = _settings.GetCodeCategories();
                var target = cats.FirstOrDefault(c => c.Id == id);
                var filters = _settings.GetCodeFilters();
                if (target == null) return;

                // Build a simple checklist
                var panel = new StackPanel();
                var checkboxes = new List<CheckBox>();
                foreach (var f in filters)
                {
                    var cb = new CheckBox { Content = f.Name, IsChecked = target.FilterIds.Contains(f.Id), Tag = f.Id, Margin = new Thickness(0,2,0,2) };
                    checkboxes.Add(cb);
                    panel.Children.Add(cb);
                }

                var dlg = new ContentDialog { Title = "Assign Filters", PrimaryButtonText = "Save", CloseButtonText = "Cancel", Content = new ScrollViewer { Content = panel, Height = 320 } };
                dlg.XamlRoot = this.XamlRoot;
                try { dlg.RequestedTheme = _settings.GetTheme(); } catch { }
                var result = await dlg.ShowAsync();
                if (result != ContentDialogResult.Primary) return;
                target.FilterIds = checkboxes.Where(cb => cb.IsChecked == true).Select(cb => (Guid)cb.Tag).ToList();
                await _settings.SetCodeCategoriesAsync(cats);
                LoadCategories();
            }
            catch { }
        }

        private class CodeCategoryViewItem
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public List<Guid> FilterIds { get; set; } = new List<Guid>();
            public string FilterNames { get; set; } = string.Empty;
        }
    }
}


