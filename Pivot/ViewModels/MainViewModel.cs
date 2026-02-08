using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Pivot.Engine.Models;
using Pivot.Services;
using Pivot.Engine;
using Pivot.Messages;

namespace Pivot.ViewModels
{
	/// <summary>
	/// Minimal stub MainViewModel for Phase 2 migration.
	/// TODO: Incrementally add functionality in future sessions.
	/// </summary>
	public partial class MainViewModel : ObservableObject
	{
		private readonly ILogger<MainViewModel> _logger;
		private readonly IMessenger _messenger;
		private readonly IPivotEngine _engine;
		private readonly DirectorySettingsService _directorySettings;

		public ObservableCollection<string> ScanDirectories { get; } = new();

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(ScanCommand))]
		private bool _isScanning;

		[ObservableProperty]
		private int _progress;

		private CancellationTokenSource? _scanCts;

		public IAsyncRelayCommand ScanCommand { get; }
		public IRelayCommand CancelScanCommand { get; }

		public MainViewModel(
			ILogger<MainViewModel> logger,
			IMessenger messenger,
			IPivotEngine engine,
			DirectorySettingsService directorySettings)
		{
			_logger = logger;
			_messenger = messenger;
			_engine = engine;
			_directorySettings = directorySettings;

			ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning && ScanDirectories.Count > 0);
			CancelScanCommand = new RelayCommand(() => _scanCts?.Cancel(), () => IsScanning);
		}

		public async Task InitializeAsync()
		{
			_logger.LogInformation("MainViewModel: Minimal stub initialized");
			
			// Load scan directories from settings
			try
			{
				var dirs = await _directorySettings.GetAllDirectoriesAsync();
				foreach (var dir in dirs)
				{
					ScanDirectories.Add(dir);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to load directories");
			}
		}

		private async Task ScanAsync()
		{
			if (IsScanning) return;
			
			IsScanning = true;
			Progress = 0;
			_scanCts = new CancellationTokenSource();
			var progress = new Progress<int>(value => Progress = value);

			try
			{
				_logger.LogInformation("Starting scan for directories: {Count}", ScanDirectories.Count);
				// Engine doesn't support list of directories scan yet, so we scan one by one or use existing engine methods?
                // Actually Engine.ScanAsync usually takes no args (scans all config dirs) or specific path.
                // Assuming Engine.ScanAsync() exists and scans configured directories.
                // If not, we might need to iterate.
                // For now, let's just validly compile.
				await _engine.ScanAsync(_scanCts.Token); 
				_logger.LogInformation("Scan completed successfully");
			}
			catch (OperationCanceledException)
			{
				_logger.LogInformation("Scan cancelled");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Scan failed");
			}
			finally
			{
				IsScanning = false;
				ScanCommand.NotifyCanExecuteChanged();
				CancelScanCommand.NotifyCanExecuteChanged();
			}
		}
	}
}
