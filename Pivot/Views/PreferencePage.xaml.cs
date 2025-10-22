using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pivot.ViewModels;
using Microsoft.UI.Xaml; // for RoutedEventHandler
using Microsoft.UI.Xaml.Media.Animation; // for SuppressNavigationTransitionInfo
using Pivot.Models;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace Pivot.Views
{
    public sealed partial class PreferencePage : Page
    {
        public PreferencePage()
        {
            this.InitializeComponent();
            this.DataContext = App.Current.Services.GetRequiredService<MainViewModel>(); // MainViewModel に修正
        }

        private void nvSample_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 初期選択をThemeに設定
                nvSample.SelectedItem = ThemeItem;
                // 最初のページへ遷移（FrameはNavigationView外なので再親化の影響を受けない）
                PreferenceContentFrame.Navigate(typeof(ThemePage), null, new SuppressNavigationTransitionInfo());

                // Pane状態に応じて左カラムの幅をPane長に合わせる
                UpdatePaneColumnWidth();
            }
            catch
            {
                // ignore
            }
        }

        private void nvSample_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer == null) return;
            var tag = args.SelectedItemContainer.Tag as string;
            if (string.IsNullOrEmpty(tag)) return;

            Type? pageType = tag switch
            {
                "ThemePage" => typeof(ThemePage),
                "DirectoryPage" => typeof(DirectoryPage),
                _ => null
            };

            if (pageType != null && PreferenceContentFrame.CurrentSourcePageType != pageType)
            {
                PreferenceContentFrame.Navigate(pageType, null, new EntranceNavigationTransitionInfo());
            }
        }

        private void nvSample_PaneOpened(NavigationView sender, object args)
        {
            UpdatePaneColumnWidth();
        }

        private void nvSample_PaneClosed(NavigationView sender, object args)
        {
            UpdatePaneColumnWidth();
        }

        private void UpdatePaneColumnWidth()
        {
            if (this.Content is Grid grid && grid.ColumnDefinitions.Count >= 1)
            {
                var targetWidth = nvSample.IsPaneOpen ? nvSample.OpenPaneLength : nvSample.CompactPaneLength;
                grid.ColumnDefinitions[0].Width = new GridLength(targetWidth);
            }
        }

        private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            // ナビゲーション失敗時の処理
        }
    }
}
