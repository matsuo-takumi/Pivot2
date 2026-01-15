using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Pivot.CodeModule.ViewModels;
using Pivot.CodeModule.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Pivot.Models;
using Windows.Foundation;

namespace Pivot.CodeModule.Views
{
    public sealed partial class CodePage : Page
    {
        public CodeViewModel ViewModel { get; private set; } = null!;

        public CodePage()
        {
            this.InitializeComponent();
            
            // Register Keyboard Accelerators
            var saveAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
            {
                Modifiers = Windows.System.VirtualKeyModifiers.Control,
                Key = Windows.System.VirtualKey.S
            };
            saveAccelerator.Invoked += SaveAccelerator_Invoked;
            this.KeyboardAccelerators.Add(saveAccelerator);
            // Resolve ViewModel from App Services
            ViewModel = ((App)Application.Current).Services.GetService<CodeViewModel>() 
                ?? throw new InvalidOperationException("CodeViewModel not registered in DI");
            this.DataContext = ViewModel;
        }

        private async void SaveAccelerator_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            if (ViewModel?.EditorVM?.IsEditing == true)
            {
                await ViewModel.EditorVM.SaveContentAsync();
            }
            else
            {
                // Trigger Quick Add Save if it's open
                QuickAddBar?.TrySave();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Trigger initial load
            if (ViewModel != null)
            {
               _ = InitializePageAsync();
            }
        }

        private async Task InitializePageAsync()
        {
            await ViewModel.InitializeAsync();
            
            // Pass available tags to QuickAddControl
            UpdateQuickAddTags();
        }

        private void UpdateQuickAddTags()
        {
            if (ViewModel?.FilterVM?.Tags != null)
            {
                QuickAddBar.SetAvailableTags(ViewModel.FilterVM.Tags);
                ViewModel.EditorVM?.SetAvailableTags(ViewModel.FilterVM.Tags);
            }
        }

        private async void QuickAddBar_SnippetCreated(object sender, QuickAddEventArgs e)
        {
            if (ViewModel?.EditorVM == null) return;

            // Create snippet via EditorVM
            var newSnippet = await ViewModel.EditorVM.CreateSnippetAsync(
                e.Title, 
                e.Language, 
                e.Code, 
                e.Tags);

            if (newSnippet != null)
            {
                // Small delay to ensure DB transaction commits
                await Task.Delay(100);
                
                // Refresh list to show new item
                await ViewModel.ListVM.LoadSnippetsAsync();
                // Also refresh tags
                ViewModel.FilterVM.LoadTags(ViewModel.ListVM.Snippets);
                // Update QuickAddControl with new tags
                UpdateQuickAddTags();
            }
        }

        private async void SnippetCard_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            // SnippetCardControl uses Asset dependency property, not DataContext
            if (sender is SnippetCardControl card && card.Asset != null)
            {
                await ViewModel.EditorVM.SetSnippetAsync(card.Asset);
            }
        }

        private void ScrollViewer_DragOver(object sender, DragEventArgs e)
        {
            // Check if the data contains our custom format and if reordering is allowed
            if (e.DataView.Contains("SnippetAssetId") && 
                ViewModel?.ListVM?.IsReorderingAllowed == true)
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
                e.DragUIOverride.Caption = "Move";
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsGlyphVisible = false;

                // Live Reordering Logic
                if (int.TryParse(Pivot.CodeModule.Controls.SnippetCardControl.CurrentDragId, out int draggedId))
                {
                     var dropPosition = e.GetPosition(SnippetsRepeater);
                     
                     // Adjust position by the grab offset to get the top-left of the floating card
                     var offset = Pivot.CodeModule.Controls.SnippetCardControl.CurrentDragOffset;
                     var actualPos = new Point(dropPosition.X - offset.X, dropPosition.Y - offset.Y);
                     
                     int targetIndex = GetTargetIndexFromPosition(actualPos);
                     
                     // Perform the visual move
                     ViewModel.ListVM.MoveItemVisual(draggedId, targetIndex);
                }
            }
            else
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            }
        }

        private async void ScrollViewer_Drop(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains("SnippetAssetId") || ViewModel?.ListVM?.IsReorderingAllowed != true) return;

            try
            {
                // Clear drag state
                Pivot.CodeModule.Controls.SnippetCardControl.CurrentDragId = null;

                // Commit the changes (Save to DB)
                await ViewModel.ListVM.CommitReorderAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Drop error: {ex.Message}");
            }
        }

        private int GetTargetIndexFromPosition(Point position)
        {
            if (SnippetsRepeater == null || ViewModel?.ListVM?.Snippets == null)
                return 0;

            int itemCount = ViewModel.ListVM.Snippets.Count;
            if (itemCount == 0)
                return 0;

            // Try to find which item we're dropping on
            for (int i = 0; i < itemCount; i++)
            {
                var element = SnippetsRepeater.TryGetElement(i);
                if (element != null)
                {
                    var transform = element.TransformToVisual(SnippetsRepeater);
                    var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualSize.X, element.ActualSize.Y));

                    // Check if the drop position is within this element's bounds
                    if (position.X >= bounds.X && position.X <= bounds.X + bounds.Width &&
                        position.Y >= bounds.Y && position.Y <= bounds.Y + bounds.Height)
                    {
                        // Drop on the second half means insert after
                        bool isGridLayout = ViewModel.ListVM.IsGridLayout;
                        double midPoint = isGridLayout ? bounds.X + bounds.Width / 2 : bounds.Y + bounds.Height / 2;
                        double checkPosition = isGridLayout ? position.X : position.Y;
                        
                        return checkPosition > midPoint ? i + 1 : i;
                    }
                }
            }

            // If we didn't find a specific element, drop at the end
            return itemCount;
        }
    }
}

