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
    }
}
