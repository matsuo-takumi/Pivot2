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
            this.Loaded += SnippetCardControl_Loaded;
        }

        private void SnippetCardControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply implicit animation to the control itself so it slides when ItemsRepeater moves it
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(this);
            var compositor = visual.Compositor;

            var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.Target = "Offset";
            offsetAnimation.InsertExpressionKeyFrame(1.0f, "this.FinalValue");
            offsetAnimation.Duration = System.TimeSpan.FromMilliseconds(400);

            var implicitAnimations = compositor.CreateImplicitAnimationCollection();
            implicitAnimations["Offset"] = offsetAnimation;

            visual.ImplicitAnimations = implicitAnimations;
        }

        public static string? CurrentDragId { get; set; }
        public static Windows.Foundation.Point CurrentDragOffset { get; set; }

        private Windows.Foundation.Point _lastPointerPt;

        private void Grid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _lastPointerPt = e.GetCurrentPoint(this).Position;
        }

        private void Grid_DragStarting(UIElement sender, Microsoft.UI.Xaml.DragStartingEventArgs e)
        {
            if (Asset == null) return;

            // Set custom data to identify the dragged snippet
            CurrentDragId = Asset.Id.ToString();
            CurrentDragOffset = _lastPointerPt;

            e.Data.SetData("SnippetAssetId", CurrentDragId);
            e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            
            // Hide the card in the list to create a "gap" effect
            // We use 0.01 to keep it hit-testable/layout-affecting but invisible
            this.Opacity = 0.01;
        }

        private void Grid_DropCompleted(UIElement sender, Microsoft.UI.Xaml.DropCompletedEventArgs e)
        {
             // Restore visibility when drag ends (whether dropped or cancelled)
             this.Opacity = 1.0;
             CurrentDragId = null;
        }
    }
}
