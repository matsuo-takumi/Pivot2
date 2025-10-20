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

        // UI バインディング用プロパティ
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

        // フィルタプロパティ
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

        // フィルタオプション
        public ObservableCollection<string> AssetTypes { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>();

        // コマンド
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
                await LoadFilterOptionsAsync();
                // 設定の復元
                SelectedDisplayMode = _settingsService.GetAssetDisplayMode();
                ShowMetadata = _settingsService.GetShowAssetMetadata();
                await LoadAssetsAsync();

                // スキャン完了メッセージをリッスン
                _messenger.Register<AssetViewModel, ScanCompletedMessage>(this, (r, m) =>
                {
                    _ = r.LoadAssetsAsync();
                });

                // Asset 変更メッセージをリッスン
                _messenger.Register<AssetViewModel, AssetChangedMessage>(this, async (r, m) =>
                {
                    try
                    {
                        _logger.LogInformation($"Asset changed: {m.Value.Type} - {m.Value.FilePath}");
                        var path = m.Value.FilePath;
                        switch (m.Value.Type)
                        {
                            case AssetChangedMessageData.ChangeType.Added:
                            case AssetChangedMessageData.ChangeType.Updated:
                            {
                                var entry = await _metadataService.GetAssetEntryByPathAsync(path);
                                if (entry == null) break;

                                // 既存アイテム検索
                                var existing = Assets.FirstOrDefault(a => string.Equals(a.Path, path, StringComparison.OrdinalIgnoreCase));
                                if (existing == null)
                                {
                                    // フィルタ条件に合致する場合のみ追加
                                    if ((SelectedAssetType == null || string.Equals(entry.Type, SelectedAssetType, StringComparison.OrdinalIgnoreCase)) &&
                                        (SelectedCategory == null || string.Equals(entry.TagsJson, SelectedCategory, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        Assets.Insert(0, entry);
                                        TotalCount++;
                                    }
                                }
                                else
                                {
                                    var index = Assets.IndexOf(existing);
                                    if (index >= 0)
                                    {
                                        Assets[index] = entry;
                                    }
                                }
                                break;
                            }
                            case AssetChangedMessageData.ChangeType.Deleted:
                            {
                                var existing = Assets.FirstOrDefault(a => string.Equals(a.Path, path, StringComparison.OrdinalIgnoreCase));
                                if (existing != null)
                                {
                                    Assets.Remove(existing);
                                    TotalCount = Math.Max(0, TotalCount - 1);
                                }
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error applying incremental asset update for {Path}", m.Value.FilePath);
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
            try
            {
                AssetTypes.Clear();
                Categories.Clear();

                // データベースから利用可能なタイプとカテゴリを取得
                var allAssets = await _metadataService.GetAssetsByPropertyAsync(0, int.MaxValue);
                
                var types = allAssets.Select(a => a.Type).Distinct().OrderBy(t => t).ToList();
                foreach (var type in types)
                {
                    if (!string.IsNullOrEmpty(type))
                    {
                        AssetTypes.Add(type);
                    }
                }

                var categories = allAssets.Select(a => a.TagsJson).Distinct().OrderBy(c => c).ToList();
                foreach (var category in categories)
                {
                    if (!string.IsNullOrEmpty(category))
                    {
                        Categories.Add(category);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading filter options");
            }
        }

        private async Task LoadAssetsAsync()
        {
            if (IsLoading)
                return;

            try
            {
                IsLoading = true;
                CurrentPage = 0;

                // フィルタ条件を適用して最初のページを取得
                var assets = await _metadataService.GetAssetsByPropertyAsync(
                    skip: 0,
                    take: PageSize,
                    type: SelectedAssetType,
                    category: SelectedCategory
                );

                Assets.Clear();
                foreach (var asset in assets)
                {
                    Assets.Add(asset);
                }

                // 総数を取得
                TotalCount = await _metadataService.GetAssetCountAsync(
                    type: SelectedAssetType,
                    category: SelectedCategory
                );

                CurrentPage = 1;
                _logger.LogInformation($"Loaded {assets.Count} assets. Total: {TotalCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading assets");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadMoreAssetsAsync()
        {
            if (IsLoadingMore || IsLoading)
                return;

            // すべてのアセットがロード済みかチェック
            if (Assets.Count >= TotalCount)
            {
                _logger.LogInformation("All assets already loaded");
                return;
            }

            try
            {
                IsLoadingMore = true;

                var skip = CurrentPage * PageSize;
                var assets = await _metadataService.GetAssetsByPropertyAsync(
                    skip: skip,
                    take: PageSize,
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
                    _logger.LogInformation($"Loaded more assets. Total in view: {Assets.Count}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading more assets");
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private async Task ResetAndLoadAssetsAsync()
        {
            await LoadAssetsAsync();
        }
    }
}
