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
    }
}


