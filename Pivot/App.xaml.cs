using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Pivot.ViewModels; // MainViewModelを使用するために追加
using Serilog; // Serilogを使用するために追加
using Microsoft.Extensions.Logging; // ILoggerを使用するために追加
using Pivot.Services; // サービスを使用するために追加
using CommunityToolkit.Mvvm.Messaging; // IMessengerを使用するために追加
using Pivot.Messages; // ThemeChangedMessageを使用するために追加
// using Pivot.Messages; // AccentColorChangedMessageを使用するために追加
// using Windows.UI; // Colorを使用するために追加

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Pivot
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application, IRecipient<ThemeChangedMessage>
    {
        private Window? _window;

        public MainWindow? MainWindow => _window as MainWindow;

        /// <summary>
        /// Gets the current <see cref="App"/> instance in use
        /// </summary>
        public new static App Current => (App)Application.Current;

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
        /// </summary>
        public IServiceProvider Services { get; }
        private IMessenger _messenger; // IMessengerを追加
        private SettingsService _settingsService; // SettingsServiceを追加

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            Services = ConfigureServices();
            _messenger = Services.GetRequiredService<IMessenger>();
            _messenger.Register<ThemeChangedMessage>(this); // テーマ変更メッセージを購読
            // _messenger.Register<AccentColorChangedMessage>(this); // AccentColorChangedMessageを購読
            _settingsService = Services.GetRequiredService<SettingsService>(); // SettingsServiceを取得
        }

        private static IServiceProvider ConfigureServices()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .UseSerilog((context, services, configuration) =>
                {
                    var defaultLevel = context.Configuration.GetSection("Serilog:MinimumLevel:Default").Value;
                    Console.WriteLine($"[DEBUG] Serilog Default MinimumLevel from config: {defaultLevel}");
                    configuration // ★Serilogの初期化をここに統一
                        .ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext()
                        .WriteTo.Debug(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug) // Visual Studio の出力ウィンドウにもログを出力
                        .WriteTo.File("logs/pivot.log", rollingInterval: RollingInterval.Day, shared: true, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug); // ファイル出力 (shared: true で複数プロセスからの書き込みに対応)
                })
                .ConfigureServices((context, services) =>
                {
                    // 以前の Serilog 統合と AddLogging の呼び出しは削除

                    // ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<DirectoryViewModel>(); // DirectoryViewModelを追加
                    services.AddSingleton<ThemeViewModel>(); // ThemeViewModelを追加
                    services.AddSingleton<AssetViewModel>(); // AssetViewModelの追加
                    services.AddSingleton<ImageViewModel>(); // ImageViewModelの追加

                    // Services
                    services.AddSingleton<MetadataService>(); 
                    services.AddSingleton<FileScannerService>(); 
                    services.AddSingleton<SettingsService>(); // SettingsServiceを追加
                    services.AddSingleton<IMessenger, WeakReferenceMessenger>(); // IMessengerを追加

                    // Configuration
                    services.AddSingleton<IConfiguration>(context.Configuration);
                })
                .Build();

            return host.Services;
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            // アプリ終了時にサービスを確実にDispose
            if (_window != null)
            {
                _window.Closed += (_, __) =>
                {
                    try
                    {
                        var fs = Services.GetService(typeof(Pivot.Services.FileScannerService)) as Pivot.Services.FileScannerService;
                        fs?.Dispose();
                        var md = Services.GetService(typeof(Pivot.Services.MetadataService)) as Pivot.Services.MetadataService;
                        md?.Dispose();
                    }
                    catch { }
                };
            }
            
            // rootElement を確実に取得
            FrameworkElement? rootElement = _window?.Content as FrameworkElement;

            if (rootElement != null)
            {
                rootElement.RequestedTheme = _settingsService.GetTheme();
            }
            // 初期アクセントカラーを適用
            // ApplyAccentColor(settingsService.GetAccentColor()); // 削除

            // 初期タイトルバーボタンの色を更新
            if (rootElement != null) // rootElementがnullでないことを確認
            {
                UpdateTitleBarColors(_settingsService.GetTheme()); // ここでテーマを渡す
            }

            // LoggerのDI取得とテストログ出力
            var logger = Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<App>>();
            logger.LogInformation("Pivot application launched.");

            // データベースの非同期初期化
            var metadataService = Services.GetRequiredService<MetadataService>();
            await metadataService.InitializeDatabase(); // ここでawaitする

            // 設定の非同期初期化を待つ
            // var settingsService = Services.GetRequiredService<SettingsService>(); // ローカル変数として再宣言しない
            await _settingsService.InitializeAsync(); // ここでawaitする

            // メッセンジャー経由で他コンポーネントへ反映 (初期テーマ・背景タイプ)
            // var messenger = Services.GetRequiredService<IMessenger>(); // _messenger フィールドを使用
            var theme = _settingsService.GetTheme();
            var backdrop = _settingsService.GetBackdropType();

            _window?.DispatcherQueue.TryEnqueue(() =>
            {
                if (_window?.Content is FrameworkElement re)
                {
                    re.RequestedTheme = theme;
                    UpdateTitleBarColors(theme);
                }
                _messenger.Send(new ThemeChangedMessage(theme));
                _messenger.Send(new BackdropTypeChangedMessage(backdrop));
            });

            // MainViewModelを初期化し、自動スキャンを開始 (SettingsService初期化後に実行)
            // この処理は起動UIをブロックしないよう非同期で開始する
            var mainViewModel = Services.GetRequiredService<MainViewModel>();
            _ = mainViewModel.InitializeAsync(); // awaitしない

            _window?.Activate();
        }

        public void Receive(ThemeChangedMessage message)
        {
            if (_window?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = message.Value;
                UpdateTitleBarColors(message.Value); // テーマ変更時にタイトルバーの色を更新
            }
        }

        // AccentColorChangedMessage の Receive メソッドを削除

        // ApplyAccentColor メソッドを削除

        // ToLight メソッドを削除

        // ToDark メソッドを削除

        // タイトルバーボタンの色を更新するメソッド
        private void UpdateTitleBarColors(ElementTheme theme)
        {
            if (MainWindow == null) return;

            var titleBar = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(MainWindow)));

            if (titleBar.TitleBar != null)
            {
                // DarkとLightテーマに基づいて色を設定
                if (theme == ElementTheme.Dark)
                {
                    titleBar.TitleBar.ForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.TitleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.TitleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.TitleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
                    titleBar.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                    titleBar.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0x20, 0xFF, 0xFF, 0xFF); // 半透明の白
                    titleBar.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0x40, 0xFF, 0xFF, 0xFF); // さらに半透明の白
                }
                else // Light theme
                {
                    titleBar.TitleBar.ForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.TitleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.TitleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.TitleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
                    titleBar.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                    titleBar.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0x20, 0x00, 0x00, 0x00); // 半透明の黒
                    titleBar.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0x40, 0x00, 0x00, 0x00); // さらに半透明の黒
                }
            }
        }
    }
}
