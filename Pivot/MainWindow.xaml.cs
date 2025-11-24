using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Pivot.ViewModels;
using Microsoft.Extensions.DependencyInjection; // GetRequiredServiceを使用するために追加
using Microsoft.UI.Composition.SystemBackdrops; // SystemBackdropを使用するために追加
using CommunityToolkit.Mvvm.Messaging; // IMessengerを使用するために追加
using Pivot.Messages; // BackdropTypeChangedMessageを使用するために追加
using Pivot.Services; // SettingsServiceを使用するために追加
using Pivot.Models; // BackdropType moved here
using System.Runtime.InteropServices; // Interopを使用するために追加
using WinRT; // WinRT.As<T>()を使用するために追加
using Microsoft.UI.Composition; // ICompositionSupportsSystemBackdropを使用するために追加
using Microsoft.UI.Dispatching; // DispatcherQueueを使用するために追加
using Microsoft.UI; // Colors
using System.Diagnostics; // Debug logging

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Pivot
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window, IRecipient<BackdropTypeChangedMessage>, IRecipient<NavigationRequestMessage>, IRecipient<OverlayColorChangedMessage>
    {
        // Provide an implicit conversion so generated binding code can pass 'this' (MainWindow)
        // to APIs that expect a FrameworkElement (the generated code calls SetConverterLookupRoot(this)).
        public static implicit operator Microsoft.UI.Xaml.FrameworkElement(MainWindow window)
        {
            return window?.Content as Microsoft.UI.Xaml.FrameworkElement ?? throw new InvalidOperationException("Content is not a FrameworkElement");
        }
        public MainViewModel ViewModel { get; }
        public ThemeViewModel ThemeViewModel { get; }
        private readonly IMessenger _messenger;
        private readonly SettingsService _settingsService;

        // Backdrop Controllers
        private DesktopAcrylicController? _acrylicController; // Null許容型に変更
        private MicaController? _micaController; // MicaControllerも追加
        private SystemBackdropConfiguration? _configurationSource; // Null許容型に変更

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            ThemeViewModel = App.Current.Services.GetRequiredService<ThemeViewModel>();
            _messenger = App.Current.Services.GetRequiredService<IMessenger>();
            _settingsService = App.Current.Services.GetRequiredService<SettingsService>();

            var rootElement = this.Content as FrameworkElement;
            if (rootElement != null)
            {
                // Set DataContext to MainViewModel for general UI
                rootElement.DataContext = ViewModel;
            }
            // Also expose ThemeViewModel as a code-behind property for x:Bind in XAML
            
            Title = "Pivot - AI Asset Foundation App";

            // カスタムタイトルバーの設定
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar); // MainWindow.xamlで定義したGridをタイトルバーとして設定

            // SettingsServiceから初期のBackdropTypeを取得して設定
            SetSystemBackdrop(_settingsService.GetBackdropType());

            // BackdropTypeChangedMessageを購読
            _messenger.Register<BackdropTypeChangedMessage>(this);
            // Overlay color changesを購読
            _messenger.Register<OverlayColorChangedMessage>(this);

            // 初期ナビゲーション（ViewModelからの要求でも遷移可能）
            NavigateTo(NavigationRegion.Asset);
            NavigateTo(NavigationRegion.Image);
            NavigateTo(NavigationRegion.Project);
            NavigateTo(NavigationRegion.Code);
            NavigateTo(NavigationRegion.Preference);

            // ナビゲーション要求購読
            _messenger.Register<NavigationRequestMessage>(this);
        }

        public void Receive(BackdropTypeChangedMessage message)
        {
            SetSystemBackdrop(message.Value);
        }

        public void Receive(OverlayColorChangedMessage message)
        {
            try
            {
                // If overlay mode is active, refresh the overlay brush
                if (_settingsService.GetBackdropType() == BackdropType.Overlay)
                {
                    SetSystemBackdrop(BackdropType.Overlay);
                }
            }
            catch { }
        }

        public void SetSystemBackdrop(BackdropType type)
        {
            // Dispose any existing controllers
            if (_micaController != null)
            {
                _micaController.Dispose();
                _micaController = null;
            }
            if (_acrylicController != null)
            {
                _acrylicController.Dispose();
                _acrylicController = null;
            }

            this.Activated -= Window_Activated; // イベントハンドラの重複登録を避ける
            this.Closed -= Window_Closed;
            if (Content is FrameworkElement rootElement) // nullチェックを追加
            {
                rootElement.ActualThemeChanged -= Window_ThemeChanged;
            }
            
            _configurationSource = null;

            if (type == BackdropType.None)
            {
                SystemBackdrop = null;
                // Register theme change handler for None mode
                if (Content is FrameworkElement rootElementForNone)
                {
                    rootElementForNone.ActualThemeChanged += Window_ThemeChanged;
                }
                // Set solid background colors based on theme (light = white, dark = black)
                SetSolidBackgroundForNone();
                return;
            }

            // Restore text colors from Color settings when switching away from None
            RestoreTextColorsFromSettings();

            DispatcherQueue.EnsureSystemDispatcherQueue();

            _configurationSource = new SystemBackdropConfiguration();
            Activated += Window_Activated;
            Closed += Window_Closed;
            if (Content is FrameworkElement rootElement2) // nullチェックを追加
            {
                rootElement2.ActualThemeChanged += Window_ThemeChanged;
            }

            _configurationSource.IsInputActive = true;
            SetConfigurationSourceTheme();

            switch (type)
            {
                case BackdropType.Mica:
                    if (MicaController.IsSupported())
                    {
                        _micaController = new MicaController();
                        _micaController.Kind = MicaKind.Base;
                        _micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                        _micaController.SetSystemBackdropConfiguration(_configurationSource);
                        SystemBackdrop = null;
                        SetTransparentRootAndTitleBar();
                    }
                    break;
                case BackdropType.MicaAlt:
                    if (MicaController.IsSupported())
                    {
                        _micaController = new MicaController();
                        _micaController.Kind = MicaKind.BaseAlt;
                        _micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                        _micaController.SetSystemBackdropConfiguration(_configurationSource);
                        SystemBackdrop = null;
                        SetTransparentRootAndTitleBar();
                    }
                    break;
                case BackdropType.AcrylicThin:
                    if (DesktopAcrylicController.IsSupported())
                    {
                        _acrylicController = new DesktopAcrylicController();
                        _acrylicController.Kind = DesktopAcrylicKind.Thin;
                        _acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                        _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
                        SystemBackdrop = null;
                        SetTransparentRootAndTitleBar();
                    }
                    break;
                case BackdropType.Acrylic:
                    if (DesktopAcrylicController.IsSupported())
                    {
                        _acrylicController = new DesktopAcrylicController();
                        _acrylicController.Kind = DesktopAcrylicKind.Base;
                        _acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                        _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
                        SystemBackdrop = null;
                        SetTransparentRootAndTitleBar();
                    }
                    break;
                case BackdropType.Overlay:
                    // Use an in-app Acrylic brush created from settings for a lightweight overlay effect
                    SystemBackdrop = null;
                    try
                    {
                        var hex = _settingsService.GetOverlayTintColor();
                        if (!hex.StartsWith("#")) hex = "#" + hex;
                        byte r = 0, g = 0, b = 0;
                        if (hex.Length == 7)
                        {
                            r = Convert.ToByte(hex.Substring(1, 2), 16);
                            g = Convert.ToByte(hex.Substring(3, 2), 16);
                            b = Convert.ToByte(hex.Substring(5, 2), 16);
                        }
                    var brush = new Microsoft.UI.Xaml.Media.AcrylicBrush
                    {
                        TintColor = ColorHelper.FromArgb(255, r, g, b),
                        TintOpacity = _settingsService.GetOverlayTintOpacity(),
                        TintLuminosityOpacity = _settingsService.GetOverlayTintLuminosityOpacity(),
                        TintTransitionDuration = TimeSpan.FromMilliseconds(_settingsService.GetOverlayTintTransitionDurationMs()),
                        FallbackColor = Colors.Transparent
                    };
                        if (Root != null) Root.Background = brush;
                        if (AppTitleBar != null) AppTitleBar.Background = brush;
                    }
                    catch
                    {
                        if (Root != null) Root.Background = new SolidColorBrush(Colors.Transparent);
                    }
                    break;
                // custom types removed
                default:
                    SystemBackdrop = null;
                    SetTransparentRootAndTitleBar();
                    break;
            }
        }

        private void SetTransparentRootAndTitleBar()
        {
            var transparentBrush = new SolidColorBrush(Colors.Transparent);
            if (Root != null) Root.Background = transparentBrush;
            if (AppTitleBar != null) AppTitleBar.Background = transparentBrush;
        }

        private void SetSolidBackgroundForNone()
        {
            if (Content is FrameworkElement rootElement)
            {
                var theme = rootElement.ActualTheme;
                Windows.UI.Color backgroundColor;
                Windows.UI.Color textColor;

                if (theme == ElementTheme.Dark)
                {
                    // Dark mode: black background, white text
                    backgroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0); // Black
                    textColor = Windows.UI.Color.FromArgb(255, 255, 255, 255); // White
                }
                else
                {
                    // Light mode: white background, black text
                    backgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255); // White
                    textColor = Windows.UI.Color.FromArgb(255, 0, 0, 0); // Black
                }

                var backgroundBrush = new SolidColorBrush(backgroundColor);
                if (Root != null) Root.Background = backgroundBrush;
                if (AppTitleBar != null) AppTitleBar.Background = backgroundBrush;
                
                // Force all text color resources to match the theme (ignore Color settings)
                ForceTextColorsForNone(textColor);
            }
        }

        private void ForceTextColorsForNone(Windows.UI.Color textColor)
        {
            var app = Application.Current;
            if (app == null) return;

            var textBrush = new SolidColorBrush(textColor);
            
            // Force all text color resources to use the specified color
            // This overrides any Color settings when in None mode
            var resourceKeys = new[]
            {
                "SystemControlForegroundBaseHighBrush",
                "SystemControlForegroundBaseMediumHighBrush",
                "SystemControlForegroundBaseMediumBrush",
                "SystemControlForegroundBaseLowBrush",
                "SystemControlForegroundAccentBrush",
                "SystemControlForegroundAccentLowBrush",
                "SystemControlForegroundAccentHighBrush",
                "TextFillColorSecondaryBrush",
                "AppTextBodyBrush",
                "AppTextSecondaryBrush",
                "AppTextCaptionBrush",
                "AppTextMutedBrush",
                "AppTextDimBrush",
                "AppTextAccentBrush",
                "AppTextAccentDimBrush"
            };

            foreach (var key in resourceKeys)
            {
                // Update in main resources
                if (app.Resources.TryGetValue(key, out var existing) && existing is SolidColorBrush existingBrush)
                {
                    existingBrush.Color = textColor;
                }
                else
                {
                    app.Resources[key] = textBrush;
                }

                // Update in theme dictionaries
                foreach (ResourceDictionary themeDictionary in app.Resources.ThemeDictionaries.Values)
                {
                    if (themeDictionary.TryGetValue(key, out var themeExisting) && themeExisting is SolidColorBrush themeBrush)
                    {
                        themeBrush.Color = textColor;
                    }
                    else
                    {
                        themeDictionary[key] = textBrush;
                    }
                }
            }

            // AppTextHighlightBrush should be the inverse (for text on colored backgrounds)
            var highlightColor = Windows.UI.Color.FromArgb(255, 
                (byte)(255 - textColor.R), 
                (byte)(255 - textColor.G), 
                (byte)(255 - textColor.B));
            var highlightBrush = new SolidColorBrush(highlightColor);
            
            if (app.Resources.TryGetValue("AppTextHighlightBrush", out var highlightExisting) && highlightExisting is SolidColorBrush highlightExistingBrush)
            {
                highlightExistingBrush.Color = highlightColor;
            }
            else
            {
                app.Resources["AppTextHighlightBrush"] = highlightBrush;
            }

            foreach (ResourceDictionary themeDictionary in app.Resources.ThemeDictionaries.Values)
            {
                if (themeDictionary.TryGetValue("AppTextHighlightBrush", out var highlightThemeExisting) && highlightThemeExisting is SolidColorBrush highlightThemeBrush)
                {
                    highlightThemeBrush.Color = highlightColor;
                }
                else
                {
                    themeDictionary["AppTextHighlightBrush"] = highlightBrush;
                }
            }
        }

        private void RestoreTextColorsFromSettings()
        {
            try
            {
                var textColorManager = App.Current.Services.GetService(typeof(ITextColorResourceManager)) as ITextColorResourceManager;
                if (textColorManager == null) return;

                // Restore all text colors from settings
                foreach (var role in TextColorRoleDefinitions.Roles)
                {
                    var hex = _settingsService.GetTextColorOverride(role.SettingKey, role.DefaultHex);
                    var color = Pivot.Utilities.TextColorHelper.ParseHexOrDefault(hex, role.DefaultColor);
                    textColorManager.ApplyColor(role.SettingKey, color);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreTextColorsFromSettings: Error: {ex.Message}");
            }
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (_configurationSource != null)
            {
                _configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            if (_acrylicController != null)
            {
                _acrylicController.Dispose();
                _acrylicController = null;
            }
            if (_micaController != null)
            {
                _micaController.Dispose();
                _micaController = null;
            }
            Activated -= Window_Activated;
            if (Content is FrameworkElement rootElement) // nullチェックを追加
            {
                rootElement.ActualThemeChanged -= Window_ThemeChanged;
            }
            _configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
            
            // If backdrop type is None, update background colors when theme changes
            if (_settingsService.GetBackdropType() == BackdropType.None)
            {
                SetSolidBackgroundForNone();
            }
            
            // Update text colors if customization is disabled (use default theme colors)
            try
            {
                if (!_settingsService.IsTextColorCustomizationEnabled())
                {
                    var textColorManager = App.Current.Services.GetService(typeof(ITextColorResourceManager)) as ITextColorResourceManager;
                    textColorManager?.UpdateThemeColors();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Window_ThemeChanged: Failed to update theme colors: {ex.Message}");
            }
        }

        private void SetConfigurationSourceTheme()
        {
            if (_configurationSource != null && Content is FrameworkElement rootElement) // nullチェックを追加
            {
                switch (rootElement.ActualTheme)
                {
                    case ElementTheme.Dark: _configurationSource.Theme = SystemBackdropTheme.Dark; break;
                    case ElementTheme.Light: _configurationSource.Theme = SystemBackdropTheme.Light; break;
                    case ElementTheme.Default: _configurationSource.Theme = SystemBackdropTheme.Default; break;
                }
            }
        }

        private void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainPivot.SelectedItem is PivotItem selectedPivotItem)
            {
                switch (selectedPivotItem.Header as string)
                {
                    case "Asset":
                        NavigateTo(NavigationRegion.Asset);
                        break;
                    case "Image":
                        NavigateTo(NavigationRegion.Image);
                        break;
                    case "Project":
                        NavigateTo(NavigationRegion.Project);
                        break;
                    case "Code":
                        NavigateTo(NavigationRegion.Code);
                        break;
                    case "Preference":
                        NavigateTo(NavigationRegion.Preference);
                        break;
                }
            }
        }

        private void NavigateTo(NavigationRegion region)
        {
            var transition = new Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo() { Effect = Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionEffect.FromRight };
            switch (region)
            {
                case NavigationRegion.Asset:
                    AssetFrame.Navigate(typeof(Views.AssetPage), null, transition);
                    break;
                case NavigationRegion.Image:
                    ImageFrame.Navigate(typeof(Views.ImagePage), null, transition);
                    break;
                case NavigationRegion.Project:
                    ProjectFrame.Navigate(typeof(Views.ProjectPage), null, transition);
                    break;
                    case NavigationRegion.Code:
                        var pivotItem = MainPivot.Items.OfType<PivotItem>().FirstOrDefault(pi => (pi.Header as string) == "Code");
                        if (pivotItem?.Content is Frame codeFrameObj)
                        {
                            codeFrameObj.Navigate(typeof(Pivot.CodeModule.Views.CodePage), null, transition);
                        }
                        break;
                case NavigationRegion.Preference:
                    PreferenceFrame.Navigate(typeof(Views.PreferencePage), null, transition);
                    break;
            }
        }

        public void Receive(NavigationRequestMessage message)
        {
            NavigateTo(message.Value);
        }

        private Brush? GetResourceBrush(string key)
        {
            // Prefer application-level resources, then page/root resources
            try
            {
                if (Application.Current != null && Application.Current.Resources != null && Application.Current.Resources.ContainsKey(key))
                {
                    return Application.Current.Resources[key] as Brush;
                }
            }
            catch { }

            if (this.Content is FrameworkElement fe)
            {
                try
                {
                    if (fe.Resources != null && fe.Resources.ContainsKey(key))
                    {
                        return fe.Resources[key] as Brush;
                    }
                }
                catch { }
            }

            return null;
        }
    }
}
