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
        }

        private static IServiceProvider ConfigureServices()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // Serilogの統合
                    Log.Logger = new Serilog.LoggerConfiguration()
                        .ReadFrom.Configuration(context.Configuration)
                        .CreateLogger();

                    services.AddLogging(loggingBuilder =>
                    {
                        loggingBuilder.AddSerilog(dispose: true);
                    });

                    // ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<DirectoryViewModel>(); // DirectoryViewModelを追加
                    services.AddSingleton<ThemeViewModel>(); // ThemeViewModelを追加

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
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            // SettingsServiceから初期テーマを取得して設定
            var settingsService = Services.GetRequiredService<SettingsService>();
            if (_window?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = settingsService.GetTheme();
            }

            _window.Activate();

            // LoggerのDI取得とテストログ出力
            var logger = Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<App>>();
            logger.LogInformation("Pivot application launched.");

            // データベースの非同期初期化
            var metadataService = Services.GetRequiredService<MetadataService>();
            _ = metadataService.InitializeDatabase(); // Waitせずに起動継続
        }

        public void Receive(ThemeChangedMessage message)
        {
            if (_window?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = message.Value;
            }
        }
    }
}
