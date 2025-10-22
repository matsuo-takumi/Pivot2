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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Pivot
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window, IRecipient<BackdropTypeChangedMessage>, IRecipient<NavigationRequestMessage>, IRecipient<MenuDisplayModeChangedMessage>
    {
        // Provide an implicit conversion so generated binding code can pass 'this' (MainWindow)
        // to APIs that expect a FrameworkElement (the generated code calls SetConverterLookupRoot(this)).
        public static implicit operator Microsoft.UI.Xaml.FrameworkElement(MainWindow window)
        {
            return window?.Content as Microsoft.UI.Xaml.FrameworkElement;
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

            // 初期ナビゲーション（ViewModelからの要求でも遷移可能）
            NavigateTo(NavigationRegion.Asset);
            NavigateTo(NavigationRegion.Image);
            NavigateTo(NavigationRegion.Project);
            NavigateTo(NavigationRegion.Preference);

            // ナビゲーション要求購読
            _messenger.Register<NavigationRequestMessage>(this);
            _messenger.Register<MenuDisplayModeChangedMessage>(this);
        }

        public void Receive(BackdropTypeChangedMessage message)
        {
            SetSystemBackdrop(message.Value);
        }

        public void Receive(MenuDisplayModeChangedMessage message)
        {
            // PreferenceFrame が現在表示されている場合、ナビゲーションビューの表示モードを更新
            // if (PreferenceFrame.Content is Views.PreferencePage preferencePage)
            // {
            //     preferencePage.UpdateMenuDisplayModeFromMessage(message.Value);
            // }
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
                return;
            }

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
                        if (Root != null) Root.Background = new SolidColorBrush(Colors.Transparent);
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
                        if (Root != null) Root.Background = new SolidColorBrush(Colors.Transparent);
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
                        if (Root != null) Root.Background = new SolidColorBrush(Colors.Transparent);
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
                        if (Root != null) Root.Background = new SolidColorBrush(Colors.Transparent);
                    }
                    break;
                // custom types removed
                default:
                    SystemBackdrop = null;
                    if (Root != null) Root.Background = new SolidColorBrush(Colors.Transparent);
                    break;
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
            if (this.Content is FrameworkElement fe)
            {
                if (fe.Resources != null && fe.Resources.ContainsKey(key))
                {
                    return fe.Resources[key] as Brush;
                }
            }
            return null;
        }
    }
}
