using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Pivot.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Windows.Foundation; // for IAsyncOperation<T> extension methods like GetAwaiter
using System.Threading.Tasks; // for GetAwaiter extension methods
using System;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Pivot.Views
{
    public sealed partial class DirectoryPage : Page
    {
        public MainViewModel ViewModel { get; }

        public DirectoryPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            this.DataContext = ViewModel;
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string targetTextBoxName)
            {
                var folderPicker = new FolderPicker();

                // Get the current window's HWND by passing a Window object
                // App.Current は Application クラスのインスタンスを返すが、MainWindow プロパティは App クラスで定義されているため、App 型にキャストしてアクセスする
                var hWnd = WindowNative.GetWindowHandle((App.Current as App)?.MainWindow);
                InitializeWithWindow.Initialize(folderPicker, hWnd);

                folderPicker.FileTypeFilter.Add("*");

                //var folder = await folderPicker.PickSingleFolderAsync();
                var folder = await folderPicker.PickSingleFolderAsync().AsTask();
                if (folder != null)
                {
                    TextBox targetTextBox = (TextBox)FindName(targetTextBoxName);
                    if (targetTextBox != null)
                    {
                        targetTextBox.Text = folder.Path;
                    }
                }
            }
        }
    }
}


