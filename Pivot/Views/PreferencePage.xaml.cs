using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pivot.ViewModels;
using Microsoft.UI.Xaml; // for RoutedEventHandler
using Microsoft.UI.Xaml.Media.Animation; // for SuppressNavigationTransitionInfo
using Pivot.Models;

namespace Pivot.Views
{
    public sealed partial class PreferencePage : Page
    {
        public PreferencePage()
        {
            this.InitializeComponent();
            this.DataContext = App.Current.Services.GetRequiredService<MainViewModel>(); // MainViewModel に修正

            // 初回表示時に確実に初期化
            this.Loaded += PreferencePage_Loaded;
        }

        private void PreferencePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (nvSample.SelectedItem == null)
            {
                nvSample.SelectedItem = ThemeItem;
            }

            if (contentFrame.Content == null)
            {
                contentFrame.Navigate(typeof(ThemePage), null, new EntranceNavigationTransitionInfo());
            }

            // 初期メニュー表示モードを適用
            UpdateMenuDisplayMode();

            // ContentFrame のナビゲーション完了時にメニュー表示モードを更新
            contentFrame.NavigationFailed += ContentFrame_NavigationFailed;
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                // 設定項目が選択された場合の処理 (今回は使わない)
            }
            else if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                switch (selectedItem.Tag?.ToString())
                {
                    case "ThemePage":
                        contentFrame.Navigate(typeof(ThemePage), null, new EntranceNavigationTransitionInfo());
                        break;
                    case "WindowPage":
                        contentFrame.Navigate(typeof(WindowPage), null, new EntranceNavigationTransitionInfo());
                        break;
                    case "DirectoryPage":
                        contentFrame.Navigate(typeof(DirectoryPage), null, new EntranceNavigationTransitionInfo());
                        break;
                    // 他のメニュー項目に対するナビゲーションロジック
                }
            }
        }

        private void UpdateMenuDisplayMode()
        {
            var settingsService = App.Current.Services.GetRequiredService<Pivot.Services.SettingsService>();
            var mode = settingsService.GetMenuDisplayMode();
            
            nvSample.PaneDisplayMode = mode == MenuDisplayMode.Wide ?
                NavigationViewPaneDisplayMode.Left :
                NavigationViewPaneDisplayMode.LeftCompact;
        }

        private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            // ナビゲーション失敗時の処理
        }
    }
}
