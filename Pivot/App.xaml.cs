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
using Pivot.Messages; // AccentColorChangedMessageを使用するために追加
using Windows.UI; // Colorを使用するために追加

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Pivot
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application, IRecipient<ThemeChangedMessage>, IRecipient<AccentColorChangedMessage>
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
            _messenger.Register<AccentColorChangedMessage>(this); // AccentColorChangedMessageを購読
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
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            var settingsService = Services.GetRequiredService<SettingsService>();
            
            // rootElement を確実に取得
            FrameworkElement? rootElement = _window?.Content as FrameworkElement;

            if (rootElement != null)
            {
                rootElement.RequestedTheme = settingsService.GetTheme();
            }
            // 初期アクセントカラーを適用
            ApplyAccentColor(settingsService.GetAccentColor());

            // 初期タイトルバーボタンの色を更新
            if (rootElement != null) // rootElementがnullでないことを確認
            {
                UpdateTitleBarColors(rootElement.RequestedTheme); // ここでテーマを渡す
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
                UpdateTitleBarColors(message.Value); // テーマ変更時にタイトルバーの色を更新
            }
        }

        public void Receive(AccentColorChangedMessage message)
        {
            ApplyAccentColor(message.Value);
            if (_window?.Content is FrameworkElement rootElement) // ここでキャスト
            {            
                UpdateTitleBarColors(rootElement.ActualTheme); // アクセントカラー変更時にタイトルバーの色を更新
            }
        }

        private void ApplyAccentColor(Color color)
        {
            // アクセントカラーをシステムリソースに設定
            Application.Current.Resources["SystemAccentColor"] = color;
            Application.Current.Resources["SystemAccentColorLight1"] = ToLight(color, 0.2f);
            Application.Current.Resources["SystemAccentColorLight2"] = ToLight(color, 0.4f);
            Application.Current.Resources["SystemAccentColorLight3"] = ToLight(color, 0.6f);
            Application.Current.Resources["SystemAccentColorDark1"] = ToDark(color, 0.2f);
            Application.Current.Resources["SystemAccentColorDark2"] = ToDark(color, 0.4f);
            Application.Current.Resources["SystemAccentColorDark3"] = ToDark(color, 0.6f);

            // DynamicAccentBrushを更新
            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out object systemAccentColorObj) &&
                systemAccentColorObj is Color systemAccentColor)
            {
                var dynamicAccentBrush = new SolidColorBrush(systemAccentColor);
                Application.Current.Resources["SystemControlForegroundAccentBrush"] = dynamicAccentBrush;
                Application.Current.Resources["SystemControlHighlightAccentBrush"] = dynamicAccentBrush;
                Application.Current.Resources["SystemControlBackgroundAccentBrush"] = dynamicAccentBrush;
                Application.Current.Resources["AccentTextFillColorPrimaryBrush"] = dynamicAccentBrush;
                Application.Current.Resources["AccentTextFillColorSecondaryBrush"] = dynamicAccentBrush;
                Application.Current.Resources["AccentTextFillColorTertiaryBrush"] = dynamicAccentBrush;
                Application.Current.Resources["AccentControlBackgroundAccentBrush"] = dynamicAccentBrush;
            }
        }

        private Color ToLight(Color color, float factor)
        {
            float red = color.R;
            float green = color.G;
            float blue = color.B;

            red = Math.Min(255, red + (255 - red) * factor);
            green = Math.Min(255, green + (255 - green) * factor);
            blue = Math.Min(255, blue + (255 - blue) * factor);

            return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
        }

        private Color ToDark(Color color, float factor)
        {
            float red = color.R;
            float green = color.G;
            float blue = color.B;

            red = Math.Max(0, red - red * factor);
            green = Math.Max(0, green - green * factor);
            blue = Math.Max(0, blue - blue * factor);

            return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
        }

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
                    titleBar.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF); // 半透明の白
                    titleBar.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF); // さらに半透明の白
                }
                else // Light theme
                {
                    titleBar.TitleBar.ForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.TitleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.TitleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.TitleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
                    titleBar.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                    titleBar.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x20, 0x00, 0x00, 0x00); // 半透明の黒
                    titleBar.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x40, 0x00, 0x00, 0x00); // さらに半透明の黒
                }
            }
        }
    }
}
