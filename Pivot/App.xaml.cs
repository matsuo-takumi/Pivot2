using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Services;
using Pivot.ViewModels;
using Pivot.Messages; // ThemeChangedMessage を使用するために追加
using Microsoft.EntityFrameworkCore;
using System.IO;

using Pivot.CodeModule.ViewModels;

namespace Pivot
{
	public partial class App : Application, IRecipient<ThemeChangedMessage> // IRecipient<ThemeChangedMessage> を追加
	{
		public static new App Current => (App)Application.Current;
		public IServiceProvider Services { get; private set; } = default!;
		public Window? MainWindow { get; private set; }
		public IConfiguration Configuration { get; private set; } = default!;

		public App()
		{
			InitializeComponent();
			BuildConfiguration();
			ConfigureServices();

			// メッセージの受信を開始
			Services.GetRequiredService<IMessenger>().Register<ThemeChangedMessage>(this);
			
			// Handle unhandled exceptions to work around WinUI SystemBackdrop bug
			this.UnhandledException += App_UnhandledException;
		}
		
		private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
		{
			// Workaround for WinUI bug: MicaBackdrop and SystemBackdrop-based classes throw
			// "ArgumentException: The parameter is incorrect" during theme changes.
			// This happens in OnDefaultSystemBackdropConfigurationChanged internally.
			if (e.Exception is ArgumentException argEx && 
			    argEx.Message.Contains("The parameter is incorrect"))
			{
				System.Diagnostics.Debug.WriteLine($"[App] Suppressed SystemBackdrop theme change exception: {argEx.Message}");
				e.Handled = true;
				return;
			}
			
			// Log other unhandled exceptions for debugging
			System.Diagnostics.Debug.WriteLine($"[App] UnhandledException: {e.Exception}");
		}

		protected override async void OnLaunched(LaunchActivatedEventArgs args)
		{
			// Initialize SQLite database with migrations (allows schema evolution without data loss)
			try
			{
				using var scope = Services.CreateScope();
				var dbContext = scope.ServiceProvider.GetRequiredService<Pivot.Data.PivotDbContext>();
				dbContext.Database.Migrate();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Database initialization failed: {ex}");
			}
			
			// Initialize the settings store (load settings.json into memory cache)
			try
			{
				var settingsStore = Services.GetRequiredService<ISettingsStore>();
				if (settingsStore is JsonSettingsStore jsonStore)
				{
					await jsonStore.InitializeAsync();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Settings store initialization failed: {ex}");
			}

			// Load directory settings
			try { await Services.GetRequiredService<DirectorySettingsService>().LoadAsync(); } catch { }
			// Initialize theme settings
			try { await Services.GetRequiredService<ThemeSettingsService>().LoadAsync(); } catch { }
			// Load remaining settings services
			try { await Services.GetRequiredService<FilterSettingsService>().LoadAsync(); } catch { }
			try { await Services.GetRequiredService<ViewportSettingsService>().LoadAsync(); } catch { }
			try { await Services.GetRequiredService<MaterialSettingsService>().LoadAsync(); } catch { }
			try { await Services.GetRequiredService<BrowserSettingsService>().LoadAsync(); } catch { }
			// Ensure text color resources are initialized
			try { _ = Services.GetRequiredService<ITextColorResourceManager>(); } catch { }
			// Kick main view model initialization (auto-scan if possible)
			try { await Services.GetRequiredService<MainViewModel>().InitializeAsync(); } catch { }

			MainWindow = new MainWindow();
			MainWindow.Activate();

			// Apply saved theme on startup
			try
			{
				var themeSettings = Services.GetRequiredService<ThemeSettingsService>();
				if (MainWindow?.Content is FrameworkElement root)
				{
					root.RequestedTheme = themeSettings.EffectiveTheme;
				}
			}
			catch { }

			// Start background directory scan and metadata indexing (non-blocking)
			var mainWindow = MainWindow as Pivot.MainWindow;
			_ = Task.Run(async () =>
			{
				try
				{
					// Phase 1: Scan directories from settings and populate database
					var directorySettings = Services.GetRequiredService<DirectorySettingsService>();
					var scanner = Services.GetRequiredService<FileScannerService>();
					
					// Get all directories from settings (Image + Asset)
					var imageDirectories = directorySettings.ImageDirectories?.ToList() ?? new List<string>();
					var assetDirectories = directorySettings.AssetDirectories?.ToList() ?? new List<string>();
					
					// Combine all directories for scanning (deduped)
					var allDirectories = imageDirectories.Concat(assetDirectories).Distinct().ToList();
					
					// Phase 0: Reconcile - delete orphaned assets from removed directories
					System.Diagnostics.Debug.WriteLine($"[Startup] Reconciling database with {allDirectories.Count} configured directories");
					await scanner.ReconcileAsync(allDirectories);
					
					if (allDirectories.Count > 0)
					{
						System.Diagnostics.Debug.WriteLine($"[Startup] Starting scan for {allDirectories.Count} directories (Images: {imageDirectories.Count}, Assets: {assetDirectories.Count})");
						await scanner.ScanAsync(allDirectories);
						System.Diagnostics.Debug.WriteLine("[Startup] Directory scan completed");
					}
					
					// Phase 2: Index pending assets (extract metadata) - runs silently
					var indexer = Services.GetRequiredService<MetadataIndexingService>();
					await indexer.IndexPendingAssetsAsync();
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[Startup] Background scan/index error: {ex.Message}");
				}
			});
		}

		private void BuildConfiguration()
		{
			Configuration = new ConfigurationBuilder()
				.SetBasePath(AppContext.BaseDirectory)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
				.Build();
		}

		private void ConfigureServices()
		{
			var sc = new ServiceCollection();
			sc.AddSingleton<IConfiguration>(Configuration);
			sc.AddLogging(b => b.AddDebug().AddConsole());
			sc.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

			// Core stores/services
			sc.AddSingleton<ISettingsStore, JsonSettingsStore>();
			// SnippetCacheService removed (legacy)
			sc.AddSingleton<MetadataService>();
            sc.AddSingleton<DirectorySettingsService>();
            sc.AddSingleton<ThemeSettingsService>();
            sc.AddSingleton<FilterSettingsService>(); // Extracted from SettingsService
            sc.AddSingleton<ViewportSettingsService>(); // Extracted from SettingsService
            sc.AddSingleton<MaterialSettingsService>(); // Extracted from SettingsService
            sc.AddSingleton<BrowserSettingsService>(); // Unified browser settings
            sc.AddSingleton<ITextColorResourceManager, TextColorResourceManager>();
            sc.AddSingleton<IDialogService, DialogService>(); // UI dialog abstraction
            sc.AddSingleton<INavigationService, NavigationService>(); // Navigation abstraction
            sc.AddSingleton<IBackdropService, BackdropService>(); // Backdrop management
			// Database services (EF Core + SQLite)
			var dbPath = System.IO.Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"Pivot", "pivot.db");
			sc.AddDbContext<Pivot.Data.PivotDbContext>(options =>
				options.UseSqlite($"Data Source={dbPath}"));
			sc.AddScoped<Pivot.Repositories.IAssetRepository, Pivot.Repositories.AssetRepository>();
			
			// File scanner service
			sc.AddSingleton<FileScannerService>();
			sc.AddSingleton<IThumbnailService, ThumbnailService>();
            
            // New Code Logic Layer
            sc.AddScoped<CodeService>();
            sc.AddSingleton<CodeTagService>();
            sc.AddSingleton<CodeNavigationService>();

            
            // Phase 2: Query and Indexing Services
            sc.AddSingleton<AssetQueryService>();
            sc.AddSingleton<MetadataIndexingService>();

			// Preset services (汎用的なプリセットサービス)
			sc.AddSingleton<IPresetService<Pivot.Models.TextColorPresetData>>(sp =>
				new PresetService<Pivot.Models.TextColorPresetData>(
					sp.GetRequiredService<ILogger<PresetService<Pivot.Models.TextColorPresetData>>>(),
					sp.GetRequiredService<ISettingsStore>(),
					"TextColorPresets"));

			// ViewModels
			sc.AddTransient<MainViewModel>();
			sc.AddTransient<HomeViewModel>();
			sc.AddTransient<ThemeViewModel>();
			sc.AddTransient<DirectoryViewModel>();
			sc.AddTransient<ImageViewModel>();
			sc.AddTransient<AssetViewModel>();
			sc.AddTransient<WindowViewModel>();
			sc.AddTransient<BrowserViewModel>();  // Phase 3: Unified browser
			sc.AddTransient<PreferencePageViewModel>();
            // Filter service (depends on SettingsService)
            sc.AddSingleton<FilterService>();

            sc.AddTransient<CodeViewModel>();
            sc.AddTransient<CodeFilterViewModel>();
            sc.AddTransient<CodeListViewModel>();
            sc.AddTransient<CodeEditorViewModel>();
            
            Services = sc.BuildServiceProvider();


		}

		public void Receive(ThemeChangedMessage message)
		{
			// Note: MainWindow.Receive already handles RequestedTheme setting.
			// Setting it twice can cause WinUI internal issues during theme transitions.
			// So we do nothing here to avoid the duplicate setting.
			System.Diagnostics.Debug.WriteLine($"[App] Receive(ThemeChangedMessage): {message.Value} (delegated to MainWindow)");
		}
	}
}
