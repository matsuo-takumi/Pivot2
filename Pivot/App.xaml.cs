using System;
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
			
			// Load directory settings
			try { await Services.GetRequiredService<DirectorySettingsService>().LoadAsync(); } catch { }
			// Initialize SettingsService (loads theme, backdrop, and other settings)
			try { await Services.GetRequiredService<SettingsService>().InitializeAsync(); } catch { }
			// Ensure text color resources are initialized
			try { _ = Services.GetRequiredService<ITextColorResourceManager>(); } catch { }
			// Kick main view model initialization (auto-scan if possible)
			try { await Services.GetRequiredService<MainViewModel>().InitializeAsync(); } catch { }
            
            // Migrate legacy codehub.db data to pivot.db
            try { await Services.GetRequiredService<LegacyRescueService>().MigrateToAssetsAsync(); } catch { }

			MainWindow = new MainWindow();
			MainWindow.Activate();

			// Apply theme on startup
			try
			{
				// Default to System theme
				if (MainWindow?.Content is FrameworkElement root)
				{
					root.RequestedTheme = ElementTheme.Default;
				}
			}
			catch { }
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
			sc.AddSingleton<MetadataService>(); // Legacy stub for compatibility
			sc.AddSingleton<SettingsService>(); // Stub for Phase 2
            sc.AddSingleton<DirectorySettingsService>();
            sc.AddSingleton<ThemeSettingsService>();
            sc.AddSingleton<FilterSettingsService>(); // Extracted from SettingsService
            sc.AddSingleton<ViewportSettingsService>(); // Extracted from SettingsService
            sc.AddSingleton<MaterialSettingsService>(); // Extracted from SettingsService
            sc.AddSingleton<ImageDisplaySettingsService>(); // Extracted from SettingsService
            sc.AddSingleton<AssetDisplaySettingsService>(); // Extracted from SettingsService
            sc.AddSingleton<CodeSettingsService>(); // Extracted from SettingsService
            sc.AddSingleton<ITextColorResourceManager, TextColorResourceManager>();
			// Database services (EF Core + SQLite)
			var dbPath = System.IO.Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"Pivot", "pivot.db");
			sc.AddDbContext<Pivot.Data.PivotDbContext>(options =>
				options.UseSqlite($"Data Source={dbPath}"));
			sc.AddScoped<Pivot.Repositories.IAssetRepository, Pivot.Repositories.AssetRepository>();
			
			// File scanner service
			sc.AddSingleton<FileScannerService>();
			sc.AddSingleton<ICatalogService, JsonCatalogService>();
			sc.AddSingleton<IThumbnailService, ThumbnailService>();
            
            // New Code Logic Layer
            sc.AddScoped<CodeService>();
            sc.AddTransient<LegacyRescueService>();

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
			sc.AddTransient<PreferencePageViewModel>();
            // Filter service (depends on SettingsService)
            sc.AddSingleton<FilterService>();

            // Code module: SQLite DB and services (lazy local appdata path)
            // Legacy services - will be removed after CodeViewModel migration
            // Legacy CodeModule services removed
            // Consolidated into CodeService and PivotDbContext
            
            sc.AddTransient<CodeViewModel>();
            
            Services = sc.BuildServiceProvider();


		}

		public void Receive(ThemeChangedMessage message)
		{
			if (MainWindow != null && MainWindow.Content is FrameworkElement root)
			{
				root.RequestedTheme = message.Value;
			}
		}
	}
}
