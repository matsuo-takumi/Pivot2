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
using Pivot.Models;
using System.Linq;

namespace Pivot.ViewModels
{
	public partial class MainViewModel : ObservableObject
	{
		//private readonly ILogger<MainViewModel> _logger; // コメントアウト
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

		private bool _isScanning;
		public bool IsScanning
		{
			get => _isScanning;
			set => SetProperty(ref _isScanning, value);
		}

		private int _progress;
		public int Progress
		{
			get => _progress;
			set => SetProperty(ref _progress, value);
		}

		public ObservableCollection<FileEntry> Files { get; } = new ObservableCollection<FileEntry>();

		private CancellationTokenSource? _scanCts;

        public IAsyncRelayCommand ScanCommand { get; }
        public IRelayCommand CancelScanCommand { get; }
        public IRelayCommand<NavigationRegion> RequestNavigateCommand { get; }

		[ObservableProperty]
		private bool _isPreferencePaneOpen;

        public MainViewModel(
			//ILogger<MainViewModel> logger, // コメントアウト
			IConfiguration configuration,
			FileScannerService fileScannerService,
			MetadataService metadataService,
            SettingsService settingsService,
            IMessenger messenger) // SettingsServiceをDIに追加
		{
			//_logger = logger; // コメントアウト
			_configuration = configuration;
			_fileScannerService = fileScannerService;
			_metadataService = metadataService;
            _settingsService = settingsService; // SettingsServiceを初期化
            _messenger = messenger;

			// _rootPath = _configuration["AppSettings:ScanRootFolder"] ?? string.Empty; // 削除
			LoadScanDirectories(); // ディレクトリ設定を読み込む

            ScanCommand = new AsyncRelayCommand(ScanAsync, CanStartScan);
			CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
            RequestNavigateCommand = new RelayCommand<NavigationRegion>(region =>
            {
                _messenger.Send(new NavigationRequestMessage(region));
            });

			// PreferencePaneの初期状態を設定 (例: 起動時は開いておく)
			IsPreferencePaneOpen = true;
		}

		private bool CanStartScan()
		{
			// return !IsScanning && !string.IsNullOrWhiteSpace(RootPath); // 変更
			return !IsScanning && ScanDirectories.Any(d => !string.IsNullOrWhiteSpace(d) && System.IO.Directory.Exists(d));
		}

		private async Task ScanAsync()
		{
			if (!CanStartScan()) return;
			IsScanning = true;
			Progress = 0;
			Files.Clear();
			_scanCts = new CancellationTokenSource();
			var progress = new Progress<int>(value => Progress = value);
			try
			{
				await _fileScannerService.ScanAsync(ScanDirectories, progress, _scanCts.Token);

				var all = await _metadataService.GetFilesAsync(0, 1000);
				foreach (var f in all)
				{
					Files.Add(f);
				}
			}
			catch (OperationCanceledException)
			{
				//_logger.LogInformation("Scan cancelled."); // コメントアウト
			}
			catch (Exception ex)
			{
				//_logger.LogError(ex, "Scan failed."); // コメントアウト
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

		private void LoadScanDirectories()
		{
			var settings = _settingsService.GetUserSettings();
			ScanDirectories.Clear();
			foreach (var dir in settings.AssetDirectories) ScanDirectories.Add(dir);
			foreach (var dir in settings.ImageDirectories) ScanDirectories.Add(dir);
			foreach (var dir in settings.ProjectDirectories) ScanDirectories.Add(dir);

			// ScanDirectoriesの変更をUIに通知し、CanExecuteChangedを呼び出す
			OnPropertyChanged(nameof(ScanDirectories));
			(ScanCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
		}
	}
}
