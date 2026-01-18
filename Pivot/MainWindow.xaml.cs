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
    public sealed partial class MainWindow : Window, IRecipient<BackdropTypeChangedMessage>, IRecipient<OverlayColorChangedMessage>, IRecipient<ThemeChangedMessage>
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
        private readonly ThemeSettingsService _themeSettings;
        private readonly INavigationService _navigationService;
        private readonly IBackdropService _backdropService;
        private readonly HotkeyService _hotkeyService;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            ThemeViewModel = App.Current.Services.GetRequiredService<ThemeViewModel>();
            _messenger = App.Current.Services.GetRequiredService<IMessenger>();
            _themeSettings = App.Current.Services.GetRequiredService<ThemeSettingsService>();
            _navigationService = App.Current.Services.GetRequiredService<INavigationService>();
            _backdropService = App.Current.Services.GetRequiredService<IBackdropService>();
            _navigationService.RegisterNavigationHandler(NavigateTo);
            
            _hotkeyService = App.Current.Services.GetRequiredService<HotkeyService>();
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            _hotkeyService.Register(this);

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

            // ThemeSettingsServiceから初期のBackdropTypeを取得して設定
            SetSystemBackdrop(_themeSettings.AppBackdropType);
            // Update backdrop theme to match current theme
            _backdropService.UpdateTheme(_themeSettings.AppTheme);

            // Register all IRecipient<T> implementations for message handling
            _messenger.RegisterAll(this);

            // 初期ナビゲーション（ViewModelからの要求でも遷移可能）
            NavigateTo(NavigationRegion.Home);
            NavigateTo(NavigationRegion.Asset);
            NavigateTo(NavigationRegion.Image);
            NavigateTo(NavigationRegion.Project);
            NavigateTo(NavigationRegion.Code);
            NavigateTo(NavigationRegion.Preference);

            // ナビゲーション要求はNavigationServiceが処理 - 直接購読不要
        }

        #region Loading Indicator

        /// <summary>
        /// Shows the loading indicator with a custom message.
        /// </summary>
        public void ShowLoading(string message = "Loading...")
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LoadingText.Text = message;
                LoadingIndicator.Visibility = Visibility.Visible;
            });
        }

        /// <summary>
        /// Hides the loading indicator.
        /// </summary>
        public void HideLoading()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>
        /// Updates the loading indicator text.
        /// </summary>
        public void UpdateLoadingText(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LoadingText.Text = message;
            });
        }

        #endregion

        public void Receive(BackdropTypeChangedMessage message)
        {
            SetSystemBackdrop(message.Value);
            // Preserve current theme when changing backdrop
            _backdropService.UpdateTheme(_themeSettings.AppTheme);
        }

        public void Receive(OverlayColorChangedMessage message)
        {
            try
            {
                // If overlay mode is active, refresh the overlay brush
                if (_themeSettings.AppBackdropType == BackdropType.Overlay)
                {
                    SetSystemBackdrop(BackdropType.Overlay);
                }
            }
            catch { }
        }

        public void Receive(ThemeChangedMessage message)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Receive(ThemeChangedMessage): {message.Value}");
                if (Content is FrameworkElement root)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Setting RequestedTheme to {message.Value}");
                    root.RequestedTheme = message.Value;
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] RequestedTheme set successfully");
                }
                // Update backdrop theme to match
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Calling _backdropService.UpdateTheme");
                _backdropService.UpdateTheme(message.Value);
                System.Diagnostics.Debug.WriteLine($"[MainWindow] _backdropService.UpdateTheme completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Receive(ThemeChangedMessage) ERROR: {ex}");
            }
        }

        public void SetSystemBackdrop(BackdropType type)
        {
            // Unregister event handlers before backdrop change
            this.Activated -= Window_Activated;
            this.Closed -= Window_Closed;
            if (Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged -= Window_ThemeChanged;
            }

            // Delegate to BackdropService - pass Content (top-level Grid) for proper backdrop application
            _backdropService.SetBackdrop(this, type, Content as FrameworkElement, AppTitleBar);

            // Handle None mode text colors
            if (type == BackdropType.None)
            {
                if (Content is FrameworkElement rootElementForNone)
                {
                    rootElementForNone.ActualThemeChanged += Window_ThemeChanged;
                    ForceTextColorsForNone(GetTextColorForTheme(rootElementForNone.ActualTheme));
                }
                return;
            }

            // Restore text colors from settings when not in None mode
            RestoreTextColorsFromSettings();

            // Register event handlers for active backdrops (idempotent: unregister first to prevent double-registration)
            Activated -= Window_Activated;
            Activated += Window_Activated;
            Closed -= Window_Closed;
            Closed += Window_Closed;
            if (Content is FrameworkElement rootElement2)
            {
                rootElement2.ActualThemeChanged -= Window_ThemeChanged;
                rootElement2.ActualThemeChanged += Window_ThemeChanged;
            }
        }

        private Windows.UI.Color GetTextColorForTheme(ElementTheme theme)
        {
            return theme == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(255, 255, 255, 255)
                : Windows.UI.Color.FromArgb(255, 0, 0, 0);
        }

        // SetTransparentRootAndTitleBar removed - now in BackdropService

        // SetSolidBackgroundForNone removed - now in BackdropService

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
                    var hex = _themeSettings.GetTextColorOverride(role.SettingKey, role.DefaultHex);
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
            _backdropService.SetInputActive(args.WindowActivationState != WindowActivationState.Deactivated);
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            _backdropService.Dispose();
            Activated -= Window_Activated;
            if (Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged -= Window_ThemeChanged;
            }
            _hotkeyService.Unregister();
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            // Update backdrop theme via service
            _backdropService.UpdateTheme(sender.ActualTheme);
            
            // If backdrop type is None, update text colors for theme
            if (_backdropService.CurrentBackdropType == BackdropType.None)
            {
                ForceTextColorsForNone(GetTextColorForTheme(sender.ActualTheme));
            }
            
            // Update text colors if customization is disabled (use default theme colors)
            try
            {
                if (!_themeSettings.IsTextColorCustomizationEnabled)
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

        // SetConfigurationSourceTheme removed - now in BackdropService

        private void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainPivot.SelectedItem is PivotItem selectedPivotItem)
            {
                switch (selectedPivotItem.Header as string)
                {
                    case "Home":
                        NavigateTo(NavigationRegion.Home);
                        break;
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
            // 既にナビゲート済みの場合は再ナビゲートしない（パフォーマンス最適化）
            var transition = new Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo() { Effect = Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionEffect.FromRight };
            switch (region)
            {
                case NavigationRegion.Home:
                    // 既にHomePageが表示されている場合は再ナビゲートしない
                    if (HomeFrame.Content?.GetType() != typeof(Views.HomePage))
                    {
                        HomeFrame.Navigate(typeof(Views.HomePage), null, transition);
                    }
                    break;
                case NavigationRegion.Asset:
                    // 既にAssetPageが表示されている場合は再ナビゲートしない
                    if (AssetFrame.Content?.GetType() != typeof(Views.AssetPage))
                    {
                        AssetFrame.Navigate(typeof(Views.AssetPage), null, transition);
                    }
                    break;
                case NavigationRegion.Image:
                    // 既にImagePageが表示されている場合は再ナビゲートしない
                    if (ImageFrame.Content?.GetType() != typeof(Views.ImagePage))
                    {
                        ImageFrame.Navigate(typeof(Views.ImagePage), null, transition);
                    }
                    break;
                case NavigationRegion.Project:
                    // 既にProjectPageが表示されている場合は再ナビゲートしない
                    if (ProjectFrame.Content?.GetType() != typeof(Views.ProjectPage))
                    {
                        ProjectFrame.Navigate(typeof(Views.ProjectPage), null, transition);
                    }
                    break;
                    case NavigationRegion.Code:
                        var pivotItem = MainPivot.Items.OfType<PivotItem>().FirstOrDefault(pi => (pi.Header as string) == "Code");
                        if (pivotItem?.Content is Frame codeFrameObj)
                        {
                            // 既にCodePageが表示されている場合は再ナビゲートしない
                            if (codeFrameObj.Content?.GetType() != typeof(Pivot.CodeModule.Views.CodePage))
                            {
                                codeFrameObj.Navigate(typeof(Pivot.CodeModule.Views.CodePage), null, transition);
                            }
                        }
                        break;
                case NavigationRegion.Preference:
                    // 既にPreferencePageが表示されている場合は再ナビゲートしない
                    if (PreferenceFrame.Content?.GetType() != typeof(Views.PreferencePage))
                    {
                        PreferenceFrame.Navigate(typeof(Views.PreferencePage), null, transition);
                    }
                    break;
            }
        }

        // NavigationRequestMessage handling moved to NavigationService

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

        private void OnHotkeyPressed(object? sender, EventArgs e)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var foreground = HotkeyService.GetForegroundWindow();

            if (hwnd == foreground)
            {
                // Minimize if currently focused
                HotkeyService.ShowWindow(hwnd, HotkeyService.SW_MINIMIZE);
            }
            else
            {
                // Bring to front
                if (HotkeyService.IsIconic(hwnd))
                {
                    HotkeyService.ShowWindow(hwnd, HotkeyService.SW_RESTORE);
                }
                HotkeyService.SetForegroundWindow(hwnd);
            }
        }
    }
}
