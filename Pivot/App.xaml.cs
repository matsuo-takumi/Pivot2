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
using Pivot.CodeModule.Services;
using Pivot.Engine; // Added
using Pivot.Engine.Models; // Added


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
			/* 
			Moved to PivotEngine.InitializeAsync to ensure proper Migration
			try
			{
				using var scope = Services.CreateScope();
				var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<Pivot.Engine.Data.PivotDbContext>>();
				using var dbContext = await factory.CreateDbContextAsync();
				// await dbContext.Database.MigrateAsync();
				await dbContext.Database.EnsureCreatedAsync();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Database initialization failed: {ex}");
			}
			*/
			
			// Initialize Pivot Engine (thumbnail generation, background processing)
			try
			{
				var engine = Services.GetRequiredService<Pivot.Engine.IPivotEngine>();
				var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				var cachePath = System.IO.Path.Combine(appData, "Pivot", "Cache");
				var dbPath = System.IO.Path.Combine(appData, "Pivot", "pivot.db");
				
				await engine.InitializeAsync(cachePath);
				System.Diagnostics.Debug.WriteLine("[Startup] Pivot Engine initialized successfully");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Engine initialization failed: {ex}");
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
            try { await Services.GetRequiredService<CodeSettingsService>().LoadAsync(); } catch { }
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
					var engine = Services.GetRequiredService<IPivotEngine>();

					
					// Get directories from settings
					var imageDirectories = directorySettings.ImageDirectories?.ToList() ?? new List<string>();
					var assetDirectories = directorySettings.AssetDirectories?.ToList() ?? new List<string>();
                    var codeDirectories = directorySettings.CodeDirectories?.ToList() ?? new List<string>();
					
					// Combine all directories for reconcile only
					var allDirectories = imageDirectories.Concat(assetDirectories).Concat(codeDirectories).Distinct().ToList();
					
					// Phase 0: Reconcile - delete orphaned assets from removed directories
					// Phase 0: Reconcile - delete orphaned assets from removed directories
					System.Diagnostics.Debug.WriteLine($"[Startup] Reconciling database with {allDirectories.Count} configured directories");
					await engine.ReconcileAsync(allDirectories);


					
					// Phase 1a: Scan Image directories (all asset types)
					if (imageDirectories.Count > 0)
					{
						System.Diagnostics.Debug.WriteLine($"[Startup] Scanning {imageDirectories.Count} Image directories (all types)");
						await engine.ScanAsync(imageDirectories);
					}

					
					// Phase 1b: Scan Asset directories (Model3D only to avoid duplicate images)
					// Only scan Asset dirs that are NOT also Image dirs
					System.Diagnostics.Debug.WriteLine($"[Startup] Image directories: {string.Join(", ", imageDirectories)}");
					System.Diagnostics.Debug.WriteLine($"[Startup] Asset directories: {string.Join(", ", assetDirectories)}");
					
					var assetOnlyDirs = assetDirectories
						.Where(d => !imageDirectories.Any(img => 
							d.Equals(img, StringComparison.OrdinalIgnoreCase) ||
							d.StartsWith(img + "\\", StringComparison.OrdinalIgnoreCase)))
						.ToList();
					
					System.Diagnostics.Debug.WriteLine($"[Startup] Asset-only directories (after filter): {string.Join(", ", assetOnlyDirs)}");
					
					if (assetOnlyDirs.Count > 0)
					{
						System.Diagnostics.Debug.WriteLine($"[Startup] Scanning {assetOnlyDirs.Count} Asset directories (Model3D only)");
						var model3DOnly = new HashSet<Pivot.Engine.Models.AssetKind> { Pivot.Engine.Models.AssetKind.Model3D };
						await engine.ScanAsync(assetOnlyDirs, progress: null, ct: default, allowedKinds: model3DOnly);
					}

					else
					{
						System.Diagnostics.Debug.WriteLine("[Startup] No Asset-only directories to scan (all overlap with Image directories)");
					}
                    
                    if (codeDirectories.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Startup] Scanning {codeDirectories.Count} Code directories");
                        var codeKinds = new HashSet<Pivot.Engine.Models.AssetKind> { Pivot.Engine.Models.AssetKind.Script, Pivot.Engine.Models.AssetKind.Code };
                        await engine.ScanAsync(codeDirectories, progress: null, ct: default, allowedKinds: codeKinds);
                    }


					
					System.Diagnostics.Debug.WriteLine("[Startup] Directory scan completed");
					
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
			sc.AddDbContextFactory<Pivot.Engine.Data.PivotDbContext>(options =>
			options.UseSqlite($"Data Source={dbPath}"));
		sc.AddScoped<Pivot.Engine.Repositories.IAssetRepository, Pivot.Engine.Repositories.AssetRepository>();
		sc.AddScoped<Pivot.Repositories.IAssetRepository, Pivot.Repositories.AssetRepository>();

			
			// Pivot Engine (thumbnail generation, metadata indexing)
			sc.AddSingleton<Pivot.Engine.IPivotEngine, Pivot.Engine.PivotEngine>();
			
			// File scanner service
			sc.AddSingleton<Pivot.Engine.Services.FileScannerService>();
			sc.AddSingleton<IThumbnailService, ThumbnailService>();
			sc.AddSingleton<Pivot.Engine.Services.IAnalysisQueryService, Pivot.Engine.Services.AnalysisQueryService>();
            
            sc.AddSingleton<SmartFolderService>();
            sc.AddScoped<CodeService>();
            sc.AddSingleton<CodeTagService>();
            sc.AddSingleton<CodeNavigationService>();
            
            sc.AddSingleton<HotkeyService>();

            
            // Phase 2: Query and Indexing Services
            sc.AddSingleton<AssetQueryService>();
            sc.AddSingleton<MetadataIndexingService>();

			// Preset services (汎用的なプリセットサービス)
			sc.AddSingleton<IPresetService<Pivot.Models.TextColorPresetData>>(sp =>
				new PresetService<Pivot.Models.TextColorPresetData>(
					sp.GetRequiredService<ILogger<PresetService<Pivot.Models.TextColorPresetData>>>(),
					sp.GetRequiredService<ISettingsStore>(),
					"TextColorPresets"));

            sc.AddSingleton<IPresetService<Pivot.Models.CodeColorPresetData>>(sp =>
                new PresetService<Pivot.Models.CodeColorPresetData>(
                    sp.GetRequiredService<ILogger<PresetService<Pivot.Models.CodeColorPresetData>>>(),
                    sp.GetRequiredService<ISettingsStore>(),
                    "CodeColorPresets"));

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

            // Code Module Services and ViewModels
            sc.AddSingleton<LanguageDetectionService>();
            sc.AddTransient<QuickAddViewModel>();
            sc.AddTransient<CodeViewModel>();
            sc.AddTransient<CodeFilterViewModel>();
            sc.AddTransient<CodeListViewModel>();
            sc.AddTransient<CodeEditorViewModel>();
            sc.AddSingleton<CodeSettingsService>();
			sc.AddTransient<AssetSettingsViewModel>();
            sc.AddTransient<CodeSettingsViewModel>();
            sc.AddTransient<Pivot.CodeModule.ViewModels.CodeColorPresetViewModel>();
            sc.AddTransient<Pivot.CodeModule.ViewModels.CodeColorSettingViewModel>();
            

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
