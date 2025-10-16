using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pivot.ViewModels;

namespace Pivot.Views
{
    public sealed partial class PreferencePage : Page
    {
        public PreferencePage()
        {
            this.InitializeComponent();
            this.DataContext = App.Current.Services.GetRequiredService<DirectoryViewModel>();

            // 初期選択
            nvSample.SelectedItem = ThemeItem;
            contentFrame.Navigate(typeof(ThemePage)); // ThemePageへナビゲーション
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
                        contentFrame.Navigate(typeof(ThemePage)); // ThemePageへナビゲーション
                        break;
                    // 他のメニュー項目に対するナビゲーションロジック
                }
            }
        }
    }
}
