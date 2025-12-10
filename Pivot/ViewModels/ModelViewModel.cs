using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Services;

namespace Pivot.ViewModels
{
    public partial class ModelViewModel : ObservableObject
    {
        public ObservableCollection<TemplateItem> Models { get; set; }

        // FilterService-backed filters
        public ObservableCollection<FilterViewModel> Filters { get; } = new ObservableCollection<FilterViewModel>();

        [ObservableProperty]
        private FilterViewModel? _selectedFilter;

        private readonly FilterService? _filterService;

        // Models after applying current filter
        public ObservableCollection<TemplateItem> DisplayedModels { get; } = new ObservableCollection<TemplateItem>();

        public ObservableCollection<ObservableCollection<TemplateItem>> MasonryColumns { get; } = new ObservableCollection<ObservableCollection<TemplateItem>>();

        private int _masonryColumnCount = 3;
        public int MasonryColumnCount
        {
            get => _masonryColumnCount;
            set
            {
                if (value <= 0) return;
                _masonryColumnCount = value;
                BuildMasonryColumns(DisplayedModels);
            }
        }

        private double _masonryColumnWidth = 200.0;
        public double MasonryColumnWidth
        {
            get => _masonryColumnWidth;
            set
            {
                if (value <= 0) return;
                _masonryColumnWidth = value;
                BuildMasonryColumns(DisplayedModels);
            }
        }

        [ObservableProperty]
        private LayoutType _currentLayout = LayoutType.Grid;

        [ObservableProperty]
        private bool _useTextListMode = false;

        [ObservableProperty]
        private bool _showThumbnails = true;

        [ObservableProperty]
        private double _selectionBorderThickness = 2.0;

        [ObservableProperty]
        private TemplateItem? _selectedModel;

        public SelectionManagerViewModel<TemplateItem> SelectionManager { get; }

        [RelayCommand]
        private void ToggleLayout()
        {
            CurrentLayout = (LayoutType)(((int)CurrentLayout + 1) % 3);
        }

        private System.Threading.CancellationTokenSource? _loadCts;

        public ModelViewModel()
        {
            SelectionManager = new SelectionManagerViewModel<TemplateItem>();
            SelectionManager.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectionManagerViewModel<TemplateItem>.SelectedCount))
                {
                    if (Models != null)
                    {
                        foreach (var item in Models)
                        {
                            item.IsSelected = SelectionManager.IsSelected(item);
                        }
                    }
                    foreach (var item in DisplayedModels)
                    {
                        item.IsSelected = SelectionManager.IsSelected(item);
                    }
                }
            };

            Models = new ObservableCollection<TemplateItem>();

            BuildMasonryColumns();
            UseTextListMode = _currentLayout == LayoutType.List;
            ShowThumbnails = !UseTextListMode;

            try
            {
                _filterService = App.Current.Services.GetService<FilterService>();
                if (_filterService != null)
                {
                    foreach (var f in _filterService.Filters) Filters.Add(f);
                    _filterService.PropertyChanged += (s, e) =>
                    {
                        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(FilterService.SelectedFilterIds))
                        {
                            ApplyFilterFromService();
                        }
                    };
                }
            }
            catch { }

            ApplyFilterFromService();

            try
            {
                var settings = App.Current.Services.GetService<SettingsService>();
                if (settings != null)
                {
                    SelectionBorderThickness = settings.GetImageSelectionBorderThickness();
                }
            }
            catch { }
        }

        partial void OnSelectedFilterChanged(FilterViewModel? value)
        {
            try
            {
                if (_filterService != null)
                {
                    if (value == null) _filterService.ClearSelectedFilters();
                    else _filterService.SetSelectedFilters(new[] { value.Id });
                }
            }
            catch { }
            ApplyFilterFromService();
        }

        private void ApplyFilterFromService()
        {
            DisplayedModels.Clear();
            try
            {
                if (_filterService == null)
                {
                    foreach (var m in Models) DisplayedModels.Add(m);
                    return;
                }
                var res = _filterService.ApplyFilter(Models);
                foreach (var m in res) DisplayedModels.Add(m);
            }
            catch
            {
                foreach (var m in Models) DisplayedModels.Add(m);
            }

            try
            {
                BuildMasonryColumns(DisplayedModels);
            }
            catch { }
        }

        public async Task LoadFromDirectoriesAsync(IEnumerable<string> directories, int maxFiles = 200)
        {
            if (directories == null) return;
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".fbx", ".obj", ".glb" };
            var files = new List<string>();
            await Task.Run(() =>
            {
                foreach (var d in directories)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(d) || !Directory.Exists(d)) continue;
                        foreach (var f in Directory.EnumerateFiles(d, "*.*", SearchOption.AllDirectories))
                        {
                            if (exts.Contains(Path.GetExtension(f)))
                            {
                                files.Add(f);
                                if (files.Count >= maxFiles) return;
                            }
                        }
                    }
                    catch { }
                }
            });

            if (files.Count == 0) return;

            try { _loadCts?.Cancel(); } catch { }
            _loadCts = new System.Threading.CancellationTokenSource();
            var ct = _loadCts.Token;

            Models.Clear();
            var thumbService = App.Current.Services.GetService<Pivot.Services.IThumbnailService>();
            try
            {
                if (thumbService != null)
                {
                    var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var cacheDir = System.IO.Path.Combine(local, "Pivot", "cache", "thumbnails");
                    await thumbService.InitializeAsync(cacheDir, 500L * 1024 * 1024);
                }
            }
            catch { }

            foreach (var f in files)
            {
                var item = new TemplateItem
                {
                    Kind = AssetKind.Model,
                    Path = f,
                    Name = Path.GetFileName(f)
                };
                try
                {
                    if (thumbService != null)
                    {
                        var cached = thumbService.TryGetCachedThumbnailPath(f, 300, 200);
                        if (!string.IsNullOrWhiteSpace(cached))
                        {
                            item.ThumbnailPath = new System.Uri(cached).AbsoluteUri;
                        }
                    }
                }
                catch { }
                Models.Add(item);
            }

            try { ApplyFilterFromService(); } catch { }

            BuildMasonryColumns();

            try
            {
                var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                if (thumbService == null) return;

                var tasks = new List<Task>();
                foreach (var item in Models.ToList())
                {
                    var originalPath = item.Path;
                    if (string.IsNullOrWhiteSpace(originalPath) || !File.Exists(originalPath)) continue;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var thumbPath = await thumbService.GetOrCreateThumbnailAsync(originalPath, 300, 200, ct).ConfigureAwait(false);
                            var thumbUri = new System.Uri(thumbPath).AbsoluteUri;
                            if (dispatcher != null)
                            {
                                dispatcher.TryEnqueue(() => item.ThumbnailPath = thumbUri);
                            }
                            else
                            {
                                item.ThumbnailPath = thumbUri;
                            }
                        }
                        catch { }
                    }));
                }

                try { await Task.WhenAll(tasks).ConfigureAwait(false); } catch { }
            }
            catch { }
        }

        public void BuildMasonryColumns(IEnumerable<TemplateItem>? source = null)
        {
            var items = source ?? Models;
            MasonryColumns.Clear();
            for (int i = 0; i < MasonryColumnCount; i++)
            {
                MasonryColumns.Add(new ObservableCollection<TemplateItem>());
            }

            var columnHeights = new int[MasonryColumnCount];
            foreach (var item in items)
            {
                int h = EstimateHeightFromUrl(item.ThumbnailPath);
                int minIndex = 0;
                for (int i = 1; i < MasonryColumnCount; i++)
                {
                    if (columnHeights[i] < columnHeights[minIndex]) minIndex = i;
                }
                MasonryColumns[minIndex].Add(item);
                columnHeights[minIndex] += h;
            }
        }

        public void CancelLoads()
        {
            try { _loadCts?.Cancel(); } catch { }
        }

        private static int EstimateHeightFromUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return 180;
            var m = Regex.Match(url, "(\\d+)x(\\d+)");
            if (m.Success && int.TryParse(m.Groups[2].Value, out int h))
            {
                return h;
            }
            return 180;
        }
    }
}

