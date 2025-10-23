using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pivot.Models;
using Pivot.Services;
using Pivot.Messages;
using System.Linq;

namespace Pivot.ViewModels
{
	public partial class MainViewModel : ObservableObject, IRecipient<DirectoryChangedMessage>
	{
		private readonly ILogger<MainViewModel> _logger;
		private readonly IConfiguration _configuration;
		private readonly FileScannerService _fileScannerService;
		private readonly MetadataService _metadataService;
        private readonly SettingsService _settingsService; // SettingsServiceを追加
        private readonly IMessenger _messenger;

		// private string _rootPath; // 単一のRootPathは非推奨になるため、プライベートフィールドとして残し、SetPropertyを削除
		// public string RootPath // このプロパティは今後使用しないので削除または変更
		// {
		//	get => _rootPath;
		//	set => SetProperty(ref _rootPath, value);
		// }

		public ObservableCollection<string> ScanDirectories { get; } = new ObservableCollection<string>(); // 新しいスキャン対象ディレクトリリスト

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ScanCommand))]
		private bool _isScanning; // _isScanning プロパティを再追加

		[ObservableProperty]
		private int _progress; // _progress プロパティを再追加

		[ObservableProperty]
		private ObservableCollection<FileEntry> _files = new();

		private CancellationTokenSource? _scanCts;

        public IAsyncRelayCommand ScanCommand { get; }
        public IRelayCommand CancelScanCommand { get; }
        public IRelayCommand<NavigationRegion> RequestNavigateCommand { get; }

        // Directory preferences commands
        public IAsyncRelayCommand<string> AddAssetDirectoryCommand { get; private set; }
        public IAsyncRelayCommand<string> AddImageDirectoryCommand { get; private set; }
        public IAsyncRelayCommand<string> AddProjectDirectoryCommand { get; private set; }
        public IAsyncRelayCommand<string> RemoveDirectoryCommand { get; private set; }

        public MainViewModel(
			ILogger<MainViewModel> logger,
			IConfiguration configuration,
			FileScannerService fileScannerService,
			MetadataService metadataService,
            SettingsService settingsService,
            IMessenger messenger)
		{
			_logger = logger;
			_configuration = configuration;
			_fileScannerService = fileScannerService;
			_metadataService = metadataService;
            _settingsService = settingsService;
            _messenger = messenger;

            // コンストラクタでのLoadScanDirectories呼び出しと自動スキャンロジックを削除
            // LoadScanDirectories(); 
            // _logger.LogInformation("LoadScanDirectories completed. Count: {Count}", ScanDirectories.Count);

            ScanCommand = new AsyncRelayCommand(ScanAsync, CanStartScan);
            CancelScanCommand = new RelayCommand(() => _scanCts?.Cancel(), () => IsScanning);
            RequestNavigateCommand = new RelayCommand<NavigationRegion>(region =>
            {
                _messenger.Send(new NavigationRequestMessage(region));
            });

            AddAssetDirectoryCommand = new AsyncRelayCommand<string>(async path =>
            {
                if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path)) return;
                _logger.LogInformation("Adding Asset Directory: {Path}", path);
                await _settingsService.AddDirectoryAsync(DirectoryCategory.Asset, path);
                LoadScanDirectories(); // ディレクトリ追加後に再度ロード

                // 監視を更新
                try { _fileScannerService.EnsureWatchers(ScanDirectories); }
                catch (Exception ex) { _logger.LogWarning(ex, "EnsureWatchers failed after adding asset directory: {Path}", path); }

                // 即時ストリーム表示（先頭500件）
                try
                {
                    var immediate = System.IO.Directory.EnumerateFiles(path, "*", System.IO.SearchOption.AllDirectories)
                        .Take(500)
                        .Select(p => new FileEntry { Path = p, Type = string.Empty, Size = 0, Hash = string.Empty, UpdatedAt = DateTime.UtcNow })
                        .ToList();
                    foreach (var f in immediate) { Files.Add(f); }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Immediate listing failed for: {Path}", path); }

                // バックグラウンドで対象ルートのみ増分スキャン
                _logger.LogInformation("Asset Directory added. Triggering background scan for: {Path}", path);
                _messenger.Send(new ScanStartedMessage(true));
                _ = _fileScannerService.ScanWithCacheAsync(new[] { path }, null, CancellationToken.None);
            });
            AddImageDirectoryCommand = new AsyncRelayCommand<string>(async path =>
            {
                if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path)) return;
                _logger.LogInformation("Adding Image Directory: {Path}", path);
                await _settingsService.AddDirectoryAsync(DirectoryCategory.Image, path);
                LoadScanDirectories();
                try { _fileScannerService.EnsureWatchers(ScanDirectories); } catch (Exception ex) { _logger.LogWarning(ex, "EnsureWatchers failed after adding image directory: {Path}", path); }
                try
                {
                    var immediate = System.IO.Directory.EnumerateFiles(path, "*", System.IO.SearchOption.AllDirectories)
                        .Take(500)
                        .Select(p => new FileEntry { Path = p, Type = string.Empty, Size = 0, Hash = string.Empty, UpdatedAt = DateTime.UtcNow })
                        .ToList();
                    foreach (var f in immediate) { Files.Add(f); }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Immediate listing failed for: {Path}", path); }
                _logger.LogInformation("Image Directory added. Triggering background scan for: {Path}", path);
                _messenger.Send(new ScanStartedMessage(true));
                _ = _fileScannerService.ScanWithCacheAsync(new[] { path }, null, CancellationToken.None);
            });
            AddProjectDirectoryCommand = new AsyncRelayCommand<string>(async path =>
            {
                if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path)) return;
                _logger.LogInformation("Adding Project Directory: {Path}", path);
                await _settingsService.AddDirectoryAsync(DirectoryCategory.Project, path);
                LoadScanDirectories();
                try { _fileScannerService.EnsureWatchers(ScanDirectories); } catch (Exception ex) { _logger.LogWarning(ex, "EnsureWatchers failed after adding project directory: {Path}", path); }
                try
                {
                    var immediate = System.IO.Directory.EnumerateFiles(path, "*", System.IO.SearchOption.AllDirectories)
                        .Take(500)
                        .Select(p => new FileEntry { Path = p, Type = string.Empty, Size = 0, Hash = string.Empty, UpdatedAt = DateTime.UtcNow })
                        .ToList();
                    foreach (var f in immediate) { Files.Add(f); }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Immediate listing failed for: {Path}", path); }
                _logger.LogInformation("Project Directory added. Triggering background scan for: {Path}", path);
                _messenger.Send(new ScanStartedMessage(true));
                _ = _fileScannerService.ScanWithCacheAsync(new[] { path }, null, CancellationToken.None);
            });

            RemoveDirectoryCommand = new AsyncRelayCommand<string>(async path =>
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                _logger.LogInformation("Removing directory: {Path}", path);
                // 全てのカテゴリから削除を試みる
                await _settingsService.RemoveDirectoryAsync(DirectoryCategory.Asset, path);
                await _settingsService.RemoveDirectoryAsync(DirectoryCategory.Image, path);
                await _settingsService.RemoveDirectoryAsync(DirectoryCategory.Project, path);
                LoadScanDirectories(); // ディレクトリ削除後に再度ロード
                try { _fileScannerService.EnsureWatchers(ScanDirectories); } catch (Exception ex) { _logger.LogWarning(ex, "EnsureWatchers failed after removing directory: {Path}", path); }

                // DB側クリーンアップと集約通知（バックグラウンド）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var deletedPaths = await _metadataService.DeleteEntriesByRootAsync(path);
                        var batch = deletedPaths.Select(p => new ItemChangeData<AssetEntry>(ItemChangeData<AssetEntry>.ChangeType.Deleted, new AssetEntry { Path = p })).ToList();
                        if (batch.Count > 0)
                        {
                            _messenger.Send(new BulkItemsChangedMessage<AssetEntry>(batch));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during DB cleanup for removed directory: {Path}", path);
                    }
                });

                _logger.LogInformation("Directory removed. Current ScanDirectories count: {Count}", ScanDirectories.Count);
            });

            // DirectoryChangedMessage をリッスン
            _messenger.Register<MainViewModel, DirectoryChangedMessage>(this, (r, m) => r.Handle(m));

            // IsPreferencePaneOpen = true; // Removed as per edit hint
        }

        public void Handle(DirectoryChangedMessage message)
        {
            if (message.Value.Category == DirectoryCategory.Project)
            {
                // Project ディレクトリが変更されたらスキャンディレクトリを再ロード
                LoadScanDirectories();
                // ウォッチャーを再設定 (変更後のディレクトリリストを渡す)
                _fileScannerService.EnsureWatchers(ScanDirectories);
            }
        }

        // IRecipient<T> implementation required by CommunityToolkit
        public void Receive(DirectoryChangedMessage message) => Handle(message);

        /// <summary>
        /// MainViewModelの初期化処理。
        /// App.xaml.csでSettingsServiceの初期化後に呼び出されることを想定。
        /// </summary>
        public async Task InitializeAsync()
        {
            _logger.LogInformation("MainViewModel: Initializing asynchronously...");
            LoadScanDirectories();
            _logger.LogInformation("MainViewModel: LoadScanDirectories completed in InitializeAsync. Count: {Count}", ScanDirectories.Count);

            // 起動直後はDBにある既存データを即時表示（UIブロックを避けつつ逐次追加）
            try
            {
                await foreach (var f in _metadataService.StreamFilesAsync(200))
                {
                    Files.Add(f);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MainViewModel: Streaming existing files failed.");
            }

            if (CanStartScan())
            {
                _logger.LogInformation("MainViewModel: Auto-scan triggered from InitializeAsync.");
                _messenger.Send(new ScanStartedMessage(true));
                // スキャンはバックグラウンドで開始（起動をブロックしない）
                _ = ScanAsync();
            }
            else
            {
                _logger.LogWarning("MainViewModel: Auto-scan skipped in InitializeAsync: CanStartScan returned false. IsScanning={IsScanning}, ScanDirectories.Count={Count}", IsScanning, ScanDirectories.Count);
            }
        }

		private async Task InitializeAndAutoScanAsync()
		{
			try
			{
				await Task.Delay(500); // UI 初期化を待つ
				if (CanStartScan())
				{
					_messenger.Send(new ScanStartedMessage(true));
					await ScanAsync();
				}
			}
			catch (Exception)
			{
				//_logger?.LogError(ex, "Auto scan failed");
			}
		}

		private bool CanStartScan()
		{
			_logger.LogInformation("CanStartScan: Checking {Count} directories", ScanDirectories.Count);
			foreach (var dir in ScanDirectories)
			{
				bool exists = System.IO.Directory.Exists(dir);
				_logger.LogInformation("CanStartScan: Directory '{Directory}' exists: {Exists}", dir, exists);
				if (!string.IsNullOrWhiteSpace(dir) && exists)
				{
					_logger.LogInformation("CanStartScan: Found valid directory, returning true");
					return !IsScanning;
				}
			}
			_logger.LogWarning("CanStartScan: No valid directories found");
			return false;
		}

		private async Task ScanAsync()
		{
			_logger.LogInformation("ScanAsync called. CanStartScan={CanStartScan}", CanStartScan());
			if (!CanStartScan()) 
			{
				_logger.LogWarning("ScanAsync returning early: CanStartScan is false");
				return;
			}
			IsScanning = true;
			Progress = 0;
            // 起動時の即時表示を維持するため、開始時点ではクリアしない
			_scanCts = new CancellationTokenSource();
			var progress = new Progress<int>(value => Progress = value);
			try
			{
				_logger.LogInformation("Starting scan for directories: {Directories}", string.Join(", ", ScanDirectories));
                // スキャンキャッシュの定期クリーンアップ
                try { await _metadataService.CleanupStaleScanCacheAsync(30); } catch { }

                // 設定に応じて増分スキャンをデフォルト使用
                if (_settingsService.GetForceFullScan())
                {
                    await _fileScannerService.ScanAsync(ScanDirectories, progress, _scanCts.Token);
                }
                else
                {
                    await _fileScannerService.ScanWithCacheAsync(ScanDirectories, progress, _scanCts.Token);
                }

                var all = await _metadataService.GetFilesAsync(0, 1000);
                // スキャン完了時に最新のDB内容で一覧をリフレッシュ
                Files.Clear();
				foreach (var f in all)
				{
					Files.Add(f); // _files.Add(f) を Files.Add(f) に変更
				}

				_logger.LogInformation("Scan completed successfully. Files found: {Count}", all.Count);
				// スキャン完了メッセージを送信
				_messenger.Send(new ScanCompletedMessage(true));
			}
			catch (OperationCanceledException)
			{
				_logger.LogInformation("Scan cancelled.");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Scan failed.");
			}
			finally
			{
				IsScanning = false;
				(ScanCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
				CancelScanCommand.NotifyCanExecuteChanged();
			}
		}

		private void CancelScan()
		{
			_scanCts?.Cancel();
		}

        public void LoadScanDirectories()
        {
            _logger.LogInformation("MainViewModel: LoadScanDirectories called.");
            var settings = _settingsService.GetUserSettings();
            _logger.LogInformation("MainViewModel: GetUserSettings returned. AssetDirectories count: {AssetCount}, ImageDirectories count: {ImageCount}, ProjectDirectories count: {ProjectCount}",
                settings.AssetDirectories.Count, settings.ImageDirectories.Count, settings.ProjectDirectories.Count);
            
            ScanDirectories.Clear();
            foreach (var dir in settings.AssetDirectories) ScanDirectories.Add(dir);
            foreach (var dir in settings.ImageDirectories) ScanDirectories.Add(dir);
            foreach (var dir in settings.ProjectDirectories) ScanDirectories.Add(dir);

            _logger.LogInformation("MainViewModel: ScanDirectories updated. Total count: {Count}", ScanDirectories.Count);
            // ScanDirectoriesの変更をUIに通知し、CanExecuteChangedを呼び出す
            OnPropertyChanged(nameof(ScanDirectories));
            (ScanCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }
    }
}
