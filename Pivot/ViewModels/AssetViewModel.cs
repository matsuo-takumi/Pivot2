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
using System.Collections.Generic;
using System.IO;

namespace Pivot.ViewModels
{
    public class AssetViewModel : ObservableObject, IRecipient<DirectoryChangedMessage>
    {
        private readonly ILogger<AssetViewModel> _logger;
        private readonly MetadataService _metadataService;
        private readonly SettingsService _settingsService;
		private readonly IMessenger _messenger;
		private readonly ICatalogService _catalogService;

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

        // コマンド
        public IAsyncRelayCommand LoadAssetsCommand { get; }
        public IAsyncRelayCommand LoadMoreAssetsCommand { get; }
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
            IMessenger messenger,
            ICatalogService catalogService)
        {
            _logger = logger;
            _metadataService = metadataService;
            _settingsService = settingsService;
            _messenger = messenger;
            _catalogService = catalogService;

            LoadAssetsCommand = new AsyncRelayCommand(LoadAssetsAsync);
            LoadMoreAssetsCommand = new AsyncRelayCommand(LoadMoreAssetsAsync);
            ToggleDisplayModeCommand = new RelayCommand(() =>
            {
                SelectedDisplayMode = SelectedDisplayMode == AssetDisplayMode.List ? AssetDisplayMode.Grid : AssetDisplayMode.List;
            });
            SetDisplayModeCommand = new RelayCommand<string>(mode =>
            {
                SelectedDisplayMode = string.Equals(mode, "Grid", StringComparison.OrdinalIgnoreCase)
                    ? AssetDisplayMode.Grid
                    : AssetDisplayMode.List;
            });
            ToggleMetadataCommand = new RelayCommand(() =>
            {
                ShowMetadata = !ShowMetadata;
            });

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Scanモードが利用可能ならカタログを初期化
                if (IsScanModeAvailable())
                {
                    try
                    {
                        var s = _settingsService.GetUserSettings();
                        var roots = new List<string>();
                        roots.AddRange(s.AssetDirectories);
                        roots.AddRange(s.ImageDirectories);
                        roots.AddRange(s.ProjectDirectories);
                        await _catalogService.InitializeAsync(roots);
                    }
                    catch { }
                }

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
                                    Assets.Insert(0, entry);
                                    TotalCount++;
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

                // DirectoryChangedMessage をリッスン
                _messenger.Register<AssetViewModel, DirectoryChangedMessage>(this, (r, m) => r.Handle(m));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing AssetViewModel");
            }
        }

        public void Handle(DirectoryChangedMessage message)
        {
            if (message.Value.Category == DirectoryCategory.Asset)
            {
                // Asset ディレクトリが変更されたらアセットを再ロード
                _ = LoadAssetsAsync();
            }
        }

        // IRecipient<T> implementation required by CommunityToolkit
        public void Receive(DirectoryChangedMessage message) => Handle(message);

        private async Task LoadAssetsAsync()
        {
            if (IsLoading)
                return;

            try
            {
                IsLoading = true;
                CurrentPage = 0;

                if (IsScanModeAvailable())
                {
                    Assets.Clear();
                    int count = 0;
                    await foreach (var a in _catalogService.StreamAssets(null, 4000))
                    {
                        count++;
                        if (Assets.Count < PageSize)
                        {
                            Assets.Add(new AssetEntry { Path = Path.GetFileName(a.Path) ?? a.Path, Name = Path.GetFileName(a.Path) ?? string.Empty, Type = a.Type, Size = a.Size, Hash = a.Hash ?? string.Empty, TagsJson = a.Category });
                        }
                    }
                    TotalCount = count;
                    CurrentPage = 1;
                    _logger.LogInformation($"Loaded {Assets.Count} assets (scan). Total: {TotalCount}");
                }
                else
                {
                    var assets = await _metadataService.GetAssetsByPropertyAsync(
                        skip: 0,
                        take: PageSize,
                        type: null,
                        category: null
                    );

                    Assets.Clear();
                    foreach (var asset in assets)
                    {
                        Assets.Add(asset);
                    }

                    TotalCount = await _metadataService.GetAssetCountAsync(type: null, category: null);

                    CurrentPage = 1;
                    _logger.LogInformation($"Loaded {assets.Count} assets. Total: {TotalCount}");
                }
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

            if (Assets.Count >= TotalCount)
            {
                _logger.LogInformation("All assets already loaded");
                return;
            }

            try
            {
                IsLoadingMore = true;

                if (IsScanModeAvailable())
                {
                    int alreadyLoaded = CurrentPage * PageSize;
                    int count = 0;
                    await foreach (var a in _catalogService.StreamAssets(null, 4000))
                    {
                        if (count++ < alreadyLoaded) continue; // Skip already loaded items
                        if (Assets.Count < TotalCount)
                        {
                            Assets.Add(new AssetEntry { Path = a.Path, Name = Path.GetFileName(a.Path) ?? string.Empty, Type = a.Type, Size = a.Size, Hash = a.Hash ?? string.Empty, TagsJson = a.Category });
                        }
                        // If we've added enough for the next page, break.
                        if (Assets.Count >= (CurrentPage + 1) * PageSize) break; 
                    }
                    CurrentPage++;
                    _logger.LogInformation($"Loaded more assets (scan). Total in view: {Assets.Count}");
                }
                else
                {
                    var skip = CurrentPage * PageSize;
                    var assets = await _metadataService.GetAssetsByPropertyAsync(
                        skip: skip,
                        take: PageSize,
                        type: null,
                        category: null
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

		private static bool IsScanModeAvailable()
		{
			try
			{
				var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				var assetsJson = Path.Combine(local, "Pivot", "cache", "assets.json");
				return File.Exists(assetsJson);
			}
			catch { return false; }
		}
    }
}
