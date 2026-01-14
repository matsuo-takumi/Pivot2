using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Models;

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

        private void Grid_DragStarting(UIElement sender, Microsoft.UI.Xaml.DragStartingEventArgs e)
        {
            if (Asset == null) return;

            // Set custom data to identify the dragged snippet
            e.Data.SetData("SnippetAssetId", Asset.Id.ToString());
            e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }
    }
}
