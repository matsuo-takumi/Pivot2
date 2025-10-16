using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pivot.ViewModels;
using Microsoft.UI.Xaml; // for RoutedEventHandler
using Microsoft.UI.Xaml.Media.Animation; // for SuppressNavigationTransitionInfo

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
                contentFrame.Navigate(typeof(ThemePage), null, new SuppressNavigationTransitionInfo());
            }
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
                        contentFrame.Navigate(typeof(ThemePage), null, new SuppressNavigationTransitionInfo()); // アニメーション無効化
                        break;
                    // 他のメニュー項目に対するナビゲーションロジック
                }
            }
        }
    }
}
