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
            AssetFrame.Navigate(typeof(Views.AssetPage));
            ImageFrame.Navigate(typeof(Views.ImagePage));
            ProjectFrame.Navigate(typeof(Views.ProjectPage));
            PreferenceFrame.Navigate(typeof(Views.PreferencePage));
        }

        private void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainPivot.SelectedItem is PivotItem selectedPivotItem)
            {
                switch (selectedPivotItem.Header as string)
                {
                    case "Asset":
                        AssetFrame.Navigate(typeof(Views.AssetPage));
                        break;
                    case "Image":
                        ImageFrame.Navigate(typeof(Views.ImagePage));
                        break;
                    case "Project":
                        ProjectFrame.Navigate(typeof(Views.ProjectPage));
                        break;
                    case "Preference":
                        PreferenceFrame.Navigate(typeof(Views.PreferencePage));
                        break;
                }
            }
        }
    }
}
