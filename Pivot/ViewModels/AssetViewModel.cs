using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Pivot.Models;
using Pivot.Services;
using Pivot.Messages;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Pivot.ViewModels
{
    public class AssetViewModel : ObservableObject
    {
        private readonly ILogger<AssetViewModel> _logger;
        private readonly MetadataService _metadataService;
        private readonly SettingsService _settingsService;
        private readonly IMessenger _messenger;

        private const int PageSize = 50;

        public ObservableCollection<AssetEntry> Assets { get; } = new ObservableCollection<AssetEntry>();

        private int _currentPage = 0;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        private int _totalCount = 0;
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _isLoadingMore = false;
        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set => SetProperty(ref _isLoadingMore, value);
        }

        private bool _isLoadingAssets = false;
        private bool _isLoadingMoreAssets = false;
        private bool _isLoadingFilterOptions = false;

        private string? _selectedAssetType;
        public string? SelectedAssetType
        {
            get => _selectedAssetType;
            set
            {
                if (SetProperty(ref _selectedAssetType, value))
                {
                    _ = ResetAndLoadAssetsAsync();
                }
            }
        }

        private string? _selectedCategory;
        public string? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    _ = ResetAndLoadAssetsAsync();
                }
            }
        }

        public ObservableCollection<string> AssetTypes { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>();

        public IAsyncRelayCommand LoadAssetsCommand { get; }
        public IAsyncRelayCommand LoadMoreAssetsCommand { get; }
        public IAsyncRelayCommand ResetFiltersCommand { get; }
        public IRelayCommand ToggleDisplayModeCommand { get; }
        public IRelayCommand ToggleMetadataCommand { get; }
        public IRelayCommand<string> SetDisplayModeCommand { get; }

        private AssetDisplayMode _selectedDisplayMode = AssetDisplayMode.List;
        public AssetDisplayMode SelectedDisplayMode
        {
            get => _selectedDisplayMode;
            set
            {
                if (SetProperty(ref _selectedDisplayMode, value))
                {
                    _ = _settingsService.SetAssetDisplayModeAsync(value);
                    OnPropertyChanged(nameof(IsListMode));
                    OnPropertyChanged(nameof(IsGridMode));
                }
            }
        }

        private bool _showMetadata = true;
        public bool ShowMetadata
        {
            get => _showMetadata;
            set
            {
                if (SetProperty(ref _showMetadata, value))
                {
                    _ = _settingsService.SetShowAssetMetadataAsync(value);
                }
            }
        }

        public bool IsListMode => SelectedDisplayMode == AssetDisplayMode.List;
        public bool IsGridMode => SelectedDisplayMode == AssetDisplayMode.Grid;

        public AssetViewModel(
            ILogger<AssetViewModel> logger,
            MetadataService metadataService,
            SettingsService settingsService,
            IMessenger messenger)
        {
            _logger = logger;
            _metadataService = metadataService;
            _settingsService = settingsService;
            _messenger = messenger;

            LoadAssetsCommand = new AsyncRelayCommand(LoadAssetsAsync);
            LoadMoreAssetsCommand = new AsyncRelayCommand(LoadMoreAssetsAsync);
            ResetFiltersCommand = new AsyncRelayCommand(ResetAndLoadAssetsAsync);
            ToggleDisplayModeCommand = new RelayCommand(() =>
            {
                SelectedDisplayMode = SelectedDisplayMode == AssetDisplayMode.List ? AssetDisplayMode.Grid : AssetDisplayMode.List;
            });
            ToggleMetadataCommand = new RelayCommand(() =>
            {
                ShowMetadata = !ShowMetadata;
            });
            SetDisplayModeCommand = new RelayCommand<string>(mode =>
            {
                SelectedDisplayMode = string.Equals(mode, "Grid", StringComparison.OrdinalIgnoreCase)
                    ? AssetDisplayMode.Grid
                    : AssetDisplayMode.List;
            });

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                SelectedDisplayMode = _settingsService.GetAssetDisplayMode();
                ShowMetadata = _settingsService.GetShowAssetMetadata();
                
                // ????????????????????????????????                await LoadAssetsAsync();

                // ????????????????????????????????????????????                _ = LoadFilterOptionsAsync();

                // ????????????????
                _messenger.Register<AssetViewModel, ScanCompletedMessage>(this, (r, m) =>
                {
                    if (!r._isLoadingAssets && !r.IsLoading)
                    {
                        _ = r.LoadAssetsAsync();
                    }
                });

                _messenger.Register<AssetViewModel, AssetChangedMessage>(this, (r, m) =>
                {
                    _logger.LogInformation("Asset changed: {0} - {1}", m.Value.Type, m.Value.FilePath);
                    if (!r._isLoadingAssets && !r.IsLoading)
                    {
                        _ = r.LoadAssetsAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing AssetViewModel");
            }
        }

        private async Task LoadFilterOptionsAsync()
        {
            if (_isLoadingFilterOptions) return;

            try
            {
                _isLoadingFilterOptions = true;

                var settings = _settingsService.GetUserSettings();
                var assetDirectories = settings.AssetDirectories;

                if (assetDirectories.Count == 0)
                {
                    _logger.LogInformation("No asset directories configured");
                    return;
                }

                // ????5000????????????????????????????????                var allAssets = await _metadataService.GetAssetsByDirectoriesAsync(0, 5000, assetDirectories);
                
                var types = allAssets.Select(a => a.Type).Distinct().OrderBy(t => t).ToList();
                foreach (var type in types)
                {
                    if (!string.IsNullOrEmpty(type) && !AssetTypes.Contains(type))
                    {
                        AssetTypes.Add(type);
                    }
                }

                var categories = allAssets.Select(a => a.TagsJson).Distinct().OrderBy(c => c).ToList();
                foreach (var category in categories)
                {
                    if (!string.IsNullOrEmpty(category) && !Categories.Contains(category))
                    {
                        Categories.Add(category);
                    }
                }

                _logger.LogInformation("LoadFilterOptionsAsync: Loaded filter options for {TypeCount} types and {CategoryCount} categories", AssetTypes.Count, Categories.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading filter options");
            }
            finally
            {
                _isLoadingFilterOptions = false;
            }
        }

        private async Task LoadAssetsAsync()
        {
            if (_isLoadingAssets || IsLoading)
            {
                _logger.LogDebug("LoadAssetsAsync: Already loading, skipping duplicate request");
                return;
            }

            try
            {
                _isLoadingAssets = true;
                IsLoading = true;
                CurrentPage = 0;

                var settings = _settingsService.GetUserSettings();
                var assetDirectories = settings.AssetDirectories;

                if (assetDirectories.Count == 0)
                {
                    _logger.LogInformation("No asset directories configured");
                    Assets.Clear();
                    TotalCount = 0;
                    CurrentPage = 1;
                    return;
                }

                // ????????????????????
                var assets = await _metadataService.GetAssetsByDirectoriesAsync(
                    skip: 0,
                    take: PageSize,
                    directories: assetDirectories,
                    type: SelectedAssetType,
                    category: SelectedCategory
                );

                Assets.Clear();
                foreach (var asset in assets)
                {
                    Assets.Add(asset);
                }

                // ????????????????????????                TotalCount = await _metadataService.GetAssetCountByDirectoriesAsync(
                    directories: assetDirectories,
                    type: SelectedAssetType,
                    category: SelectedCategory
                );

                CurrentPage = 1;
                _logger.LogInformation("Loaded {Count} assets in configured directories. Total: {Total}", assets.Count, TotalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading assets");
            }
            finally
            {
                IsLoading = false;
                _isLoadingAssets = false;
            }
        }

        public async Task LoadMoreAssetsAsync()
        {
            if (_isLoadingMoreAssets || IsLoadingMore || IsLoading)
            {
                _logger.LogDebug("LoadMoreAssetsAsync: Already loading, skipping duplicate request");
                return;
            }

            if (Assets.Count >= TotalCount)
            {
                _logger.LogInformation("All assets already loaded");
                return;
            }

            try
            {
                _isLoadingMoreAssets = true;
                IsLoadingMore = true;

                var settings = _settingsService.GetUserSettings();
                var assetDirectories = settings.AssetDirectories;

                if (assetDirectories.Count == 0)
                {
                    _logger.LogInformation("No asset directories configured");
                    return;
                }

                var skip = CurrentPage * PageSize;
                var assets = await _metadataService.GetAssetsByDirectoriesAsync(
                    skip: skip,
                    take: PageSize,
                    directories: assetDirectories,
                    type: SelectedAssetType,
                    category: SelectedCategory
                );

                if (assets.Count > 0)
                {
                    foreach (var asset in assets)
                    {
                        Assets.Add(asset);
                    }

                    CurrentPage++;
                    _logger.LogInformation("Loaded more assets. Total in view: {Count}", Assets.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading more assets");
            }
            finally
            {
                IsLoadingMore = false;
                _isLoadingMoreAssets = false;
            }
        }

        private async Task ResetAndLoadAssetsAsync()
        {
            await LoadAssetsAsync();
        }
    }
}
