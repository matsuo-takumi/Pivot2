using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Services;
using Pivot.ViewModels;
using Pivot.Messages; // ThemeChangedMessage を使用するために追加

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
			sc.AddSingleton<SettingsService>();
			sc.AddSingleton<MetadataService>();
			sc.AddSingleton<ICatalogService, JsonCatalogService>();
			sc.AddSingleton<FileScannerService>();
			sc.AddSingleton<IThumbnailService, ThumbnailService>();

			// ViewModels
			sc.AddTransient<MainViewModel>();
			sc.AddTransient<ThemeViewModel>();
			sc.AddTransient<DirectoryViewModel>();
			sc.AddTransient<ImageViewModel>();
			sc.AddTransient<AssetViewModel>();
			sc.AddTransient<WindowViewModel>();
			sc.AddTransient<PreferencePageViewModel>();
            // Filter service (depends on SettingsService)
            sc.AddSingleton<FilterService>();

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


