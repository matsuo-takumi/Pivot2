using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pivot.Models;
using Pivot.CodeModule.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using System.IO;

namespace Pivot.CodeModule.Controls
{
    public sealed partial class SnippetCardControl : UserControl
    {
        public static readonly DependencyProperty AssetProperty =
            DependencyProperty.Register("Asset", typeof(AssetEntity), typeof(SnippetCardControl), new PropertyMetadata(null));

        public AssetEntity Asset
        {
            get => (AssetEntity)GetValue(AssetProperty);
            set => SetValue(AssetProperty, value);
        }

        public SnippetCardControl()
        {
            this.InitializeComponent();
        }

        private CodeListViewModel? GetListViewModel()
        {
            DependencyObject? current = this;
            while (current != null)
            {
                if (current is Pivot.CodeModule.Views.CodePage page)
                {
                    return page.ViewModel?.ListVM;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            await CopyContentAsync();
        }

        private async void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await CopyContentAsync();
        }

        private async System.Threading.Tasks.Task CopyContentAsync()
        {
            if (Asset == null) return;

            try
            {
                string content;
                if (!string.IsNullOrEmpty(Asset.FilePath) && File.Exists(Asset.FilePath))
                {
                    content = await File.ReadAllTextAsync(Asset.FilePath);
                }
                else
                {
                    content = Asset.ContentIndex ?? string.Empty;
                }

                var dataPackage = new DataPackage();
                dataPackage.SetText(content);
                Clipboard.SetContent(dataPackage);
            }
            catch { }
        }

        private async void DuplicateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Asset == null) return;

            var listVM = GetListViewModel();
            if (listVM == null) return;

            // Find CodePage to access EditorVM for creation
            DependencyObject? current = this;
            Pivot.CodeModule.Views.CodePage? page = null;
            while (current != null)
            {
                if (current is Pivot.CodeModule.Views.CodePage p)
                {
                    page = p;
                    break;
                }
                current = VisualTreeHelper.GetParent(current);
            }

            if (page?.ViewModel?.EditorVM == null) return;

            // Read content
            string content = Asset.ContentIndex ?? string.Empty;
            if (!string.IsNullOrEmpty(Asset.FilePath) && File.Exists(Asset.FilePath))
            {
                content = await File.ReadAllTextAsync(Asset.FilePath);
            }

            // Create duplicate with modified name
            var newSnippet = await page.ViewModel.EditorVM.CreateSnippetAsync(
                Asset.FileName + "_copy",
                Asset.Tool ?? "text",
                content,
                Asset.GetTags().ToArray()
            );

            if (newSnippet != null)
            {
                await listVM.LoadSnippetsAsync();
            }
        }

        private void MoveUpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Asset == null) return;
            var listVM = GetListViewModel();
            listVM?.MoveItemUpCommand?.Execute(Asset);
        }

        private void MoveDownMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Asset == null) return;
            var listVM = GetListViewModel();
            listVM?.MoveItemDownCommand?.Execute(Asset);
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Asset == null) return;
            var listVM = GetListViewModel();
            listVM?.DeleteItemCommand?.Execute(Asset);
        }
    }
}
