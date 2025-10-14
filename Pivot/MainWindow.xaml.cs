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
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Pivot
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            var rootElement = this.Content as FrameworkElement;
            if (rootElement != null)
            {
                rootElement.DataContext = ViewModel;
            }
            ContentFrame.DataContext = ViewModel;

            Title = "Pivot - AI Asset Foundation App";

            // 初期ナビゲーション
            ContentFrame.Navigate(typeof(Views.AssetPage)); // ダミーのAssetPageを想定
        }

        private void MainNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(Views.PreferencePage)); // ダミーのPreferencePageを想定
            }
            else if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                switch (selectedItem.Tag)
                {
                    case "Asset":
                        ContentFrame.Navigate(typeof(Views.AssetPage)); // ダミーのAssetPageを想定
                        break;
                    case "Image":
                        ContentFrame.Navigate(typeof(Views.ImagePage)); // ダミーのImagePageを想定
                        break;
                    case "Project":
                        ContentFrame.Navigate(typeof(Views.ProjectPage)); // ダミーのProjectPageを想定
                        break;
                    case "Preference":
                        ContentFrame.Navigate(typeof(Views.PreferencePage)); // ダミーのPreferencePageを想定
                        break;
                }
            }
        }
    }
}
