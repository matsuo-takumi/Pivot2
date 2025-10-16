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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Pivot
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window, IRecipient<BackdropTypeChangedMessage>, IRecipient<NavigationRequestMessage>
    {
        public MainViewModel ViewModel { get; }
        private readonly IMessenger _messenger;
        private readonly SettingsService _settingsService;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            _messenger = App.Current.Services.GetRequiredService<IMessenger>();
            _settingsService = App.Current.Services.GetRequiredService<SettingsService>();

            var rootElement = this.Content as FrameworkElement;
            if (rootElement != null)
            {
                rootElement.DataContext = ViewModel;
            }
            
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
        }

        public void Receive(BackdropTypeChangedMessage message)
        {
            SetSystemBackdrop(message.Value);
        }

        public void SetSystemBackdrop(BackdropType type)
        {
            switch (type)
            {
                case BackdropType.Mica:
                    SystemBackdrop = new MicaBackdrop();
                    break;
                case BackdropType.AcrylicThin:
                    // TODO: AcrylicThinの実装
                    SystemBackdrop = new DesktopAcrylicBackdrop();
                    break;
                case BackdropType.MicaAlt:
                    // TODO: Mica Altの実装
                    SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.BaseAlt };
                    break;
                case BackdropType.None:
                default:
                    SystemBackdrop = null;
                    break;
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
    }
}
