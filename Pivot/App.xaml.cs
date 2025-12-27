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
using Pivot.CodeModule.Services;
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
			// Initialize settings early
			try { await Services.GetRequiredService<SettingsService>().InitializeAsync(); } catch { }
			// Ensure text color resources are initialized before preferences are shown
			try { _ = Services.GetRequiredService<ITextColorResourceManager>(); } catch { }
			// Kick main view model initialization (auto-scan if possible)
			try { await Services.GetRequiredService<MainViewModel>().InitializeAsync(); } catch { }

			MainWindow = new MainWindow();
			MainWindow.Activate();

			// 起動時に保存されたテーマを即座に適用する
			try
			{
				var theme = Services.GetRequiredService<SettingsService>().GetTheme();
				if (MainWindow?.Content is FrameworkElement root)
				{
					root.RequestedTheme = theme;
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
			sc.AddSingleton<SnippetCacheService>();
			sc.AddSingleton<SettingsService>();
            sc.AddSingleton<DirectorySettingsService>();
            sc.AddSingleton<ThemeSettingsService>();
            sc.AddSingleton<ITextColorResourceManager, TextColorResourceManager>();
			sc.AddSingleton<MetadataService>();
			sc.AddSingleton<ICatalogService, JsonCatalogService>();
			sc.AddSingleton<FileScannerService>();
			sc.AddSingleton<IThumbnailService, ThumbnailService>();

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
            try
            {
                var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dbDir = Path.Combine(localFolder, "Pivot");
                Directory.CreateDirectory(dbDir);
                var dbPath = Path.Combine(dbDir, "codehub.db");

                sc.AddDbContext<SQLiteDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath}"));

                sc.AddTransient<ICodeRepository, CodeRepository>();
                sc.AddTransient<CodeViewModel>();
            }
            catch (Exception ex)
            {
                // Best-effort registration; if System.IO or EF unavailable at runtime the app should still start.
                System.Diagnostics.Debug.WriteLine($"ConfigureServices: Code module registration failed: {ex}");
                // Register a file-based fallback repository so CodeViewModel can still save/export without SQLite.
                try
                {
                    var asm = typeof(Pivot.CodeModule.Services.CodeRepository).Assembly;
                    var t = asm.GetType("Pivot.CodeModule.Services.FileCodeRepository");
                    if (t != null)
                    {
                        sc.AddTransient(typeof(ICodeRepository), sp => (ICodeRepository)Activator.CreateInstance(t, sp.GetRequiredService<SettingsService>())!);
                        System.Diagnostics.Debug.WriteLine("ConfigureServices: registered FileCodeRepository fallback via reflection.");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("ConfigureServices: FileCodeRepository type not found in assembly; fallback not registered.");
                    }
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"ConfigureServices: failed to register FileCodeRepository fallback: {ex2}");
                }
            }

            // Ensure CodeViewModel is always registered so UI can still function even if DB registration failed.
            sc.AddTransient<CodeViewModel>();

			Services = sc.BuildServiceProvider();
			// Ensure SQLite DB is created if possible so EF queries don't fail due to missing tables.
			try
			{
				using var scope = Services.CreateScope();
				var ctx = scope.ServiceProvider.GetService<SQLiteDbContext>();
				if (ctx != null)
				{
					try
					{
						ctx.Database.EnsureCreated();
                        // Ensure Tags column exists in CodeFiles table; if missing, add it (SQLite ALTER TABLE ADD COLUMN)
                        try
                        {
                            var conn = ctx.Database.GetDbConnection();
                            conn.Open();
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "PRAGMA table_info('CodeFiles');";
                                using var rdr = cmd.ExecuteReader();
                                var hasTags = false;
                                while (rdr.Read())
                                {
                                    try
                                    {
                                        var name = rdr["name"]?.ToString();
                                        if (string.Equals(name, "Tags", StringComparison.OrdinalIgnoreCase)) { hasTags = true; break; }
                                    }
                                    catch { }
                                }
                                rdr.Close();
                                if (!hasTags)
                                {
                                    try
                                    {
                                        ctx.Database.ExecuteSqlRaw("ALTER TABLE CodeFiles ADD COLUMN Tags TEXT;");
                                        System.Diagnostics.Debug.WriteLine("ConfigureServices: added Tags column to CodeFiles table.");
                                    }
                                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ConfigureServices: failed to add Tags column: {ex}"); }
                                }
                            }
                            try { conn.Close(); } catch { }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"ConfigureServices: EnsureTagsColumn check failed: {ex}");
                        }
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"ConfigureServices: EnsureCreated failed: {ex}");
					}
				}
			}
			catch { }
		}

		public void Receive(ThemeChangedMessage message)
		{
			if (MainWindow != null && MainWindow.Content is FrameworkElement root)
			{
				root.RequestedTheme = message.Value;
				
				// Update text colors if customization is disabled (use default theme colors)
				try
				{
					var settings = Services.GetRequiredService<SettingsService>();
					if (!settings.IsTextColorCustomizationEnabled())
					{
						var textColorManager = Services.GetRequiredService<ITextColorResourceManager>();
						textColorManager.UpdateThemeColors();
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"App.Receive(ThemeChangedMessage): Failed to update theme colors: {ex.Message}");
				}
			}
		}
	}
}


