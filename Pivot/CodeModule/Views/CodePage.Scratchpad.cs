using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Pivot.Utilities;
using Pivot.CodeModule.ViewModels;

namespace Pivot.CodeModule.Views
{
    public sealed partial class CodePage
    {
        // / <summary>
        // / Shows or hides the scratchpad overlay.
        // / </summary>
        private void SetScratchpadOverlayVisible(bool visible)
        {
            try
            {
                var root = this.Content as FrameworkElement;
                var overlay = root?.FindName("ScratchpadOverlay") as UIElement;
                if (overlay != null) overlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        // Show the existing scratchpad overlay and populate it. The overlay is made draggable via pointer handlers.
        private Task ShowScratchpadOverlayAsync()
        {
            try
            {
                var vm = ViewModel;
                if (vm?.SelectedSnippet == null) return Task.CompletedTask;

                var root = this.Content as FrameworkElement;
                if (root == null) return Task.CompletedTask;

                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        var overlay = root.FindName("ScratchpadOverlay") as Grid;
                        var container = root.FindName("ScratchpadContainer") as FrameworkElement;
                        var title = root.FindName("ScratchpadTitle") as TextBlock;
                        var scratchEditor = root.FindName("ScratchpadEditor") as TextBox;
                        var cardPanelLocal = root.FindName("CardPanel") as FrameworkElement;

                        // Ensure card area is visible so overlay/container can be shown
                        if (cardPanelLocal != null) cardPanelLocal.Visibility = Visibility.Visible;

                        if (overlay != null) overlay.Visibility = Visibility.Visible;
                        if (container != null) container.Visibility = Visibility.Visible;
                        if (title != null) title.Text = vm.SelectedSnippet.Title ?? "Scratchpad";
                        if (scratchEditor != null)
                        {
                            scratchEditor.Text = vm.SelectedSnippet.Content ?? string.Empty;
                        }


                        // ensure scratch editor focused
                        try { scratchEditor?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic); } catch { }

                        // adjust container size to fit available area
                        try { AdjustScratchpadSize(); } catch { }
                    }
                    catch { }
                });
            }
            catch { }
            return Task.CompletedTask;
        }

        private void ScratchpadOverlay_PointerPressed(object? sender, PointerRoutedEventArgs e)
        {
            // Only close when click occurs outside the ScratchpadContainer.
            try
            {
                var overlay = sender as FrameworkElement ?? (this.Content as FrameworkElement);
                var container = overlay?.FindName("ScratchpadContainer") as FrameworkElement;
                if (container == null)
                {
                    // Fallback: try finding from root
                    var root = this.Content as FrameworkElement;
                    container = root?.FindName("ScratchpadContainer") as FrameworkElement;
                }

                // If container missing or not measured yet, treat as outside click and close
                if (container == null || container.ActualWidth <= 0 || container.ActualHeight <= 0)
                {
                    try { CloseScratchpadSimple(); } catch { }
                    try { e.Handled = true; } catch { }
                    return;
                }

                // Determine pointer position relative to overlay and test against container bounds
                var pt = e.GetCurrentPoint(overlay).Position;
                var transform = container.TransformToVisual(overlay);
                var bounds = transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
                if (!bounds.Contains(pt))
                {
                    try { CloseScratchpadSimple(); } catch { }
                    try { e.Handled = true; } catch { }
                }
                // If inside bounds, do nothing so inner controls (tabs/title) receive the event normally.
            }
            catch { }
        }

        // Purchase button removed - handler intentionally deleted

        private void ScratchpadCloseButton_Click(object? sender, RoutedEventArgs e)
        {
            CloseScratchpadSimple();
        }

        private void ScratchpadTagMenu_Click(object? sender, RoutedEventArgs e)
        {
            // TODO: implement tag management UI
        }

        private void ScratchpadDeleteMenu_Click(object? sender, RoutedEventArgs e)
        {
            // Delete the current scratchpad snippet
            var vm = DataContext as CodeViewModel;
            if (vm?.SelectedSnippet != null)
            {
                vm.DeleteSnippetCommand?.Execute(vm.SelectedSnippet);
                CloseScratchpadSimple();
            }
        }

        private void ScratchpadCopyMenu_Click(object? sender, RoutedEventArgs e)
        {
            // Copy snippet content to clipboard
            var vm = DataContext as CodeViewModel;
            if (vm?.SelectedSnippet != null)
            {
                vm.CopyToClipboardCommand?.Execute(vm.SelectedSnippet);
            }
        }

        private void ScratchpadHistoryMenu_Click(object? sender, RoutedEventArgs e)
        {
            // TODO: implement history display
        }

        private async void CloseScratchpadSimple()
        {
            try
            {
                var root = this.Content as FrameworkElement;
                
                // エディタの内容をSelectedSnippetに反映
                try
                {
                    var scratchEditor = root?.FindName("ScratchpadEditor") as TextBox;
                    var titleBox = root?.FindName("ScratchpadTitleBox") as TextBox;
                    var vm = ViewModel;
                    var snip = vm?.SelectedSnippet;
                    
                    if (vm != null && snip != null)
                    {
                        if (scratchEditor != null)
                        {
                            snip.Content = scratchEditor.Text ?? string.Empty;
                        }
                        if (titleBox != null)
                        {
                            snip.Title = titleBox.Text ?? string.Empty;
                        }
                    }
                }
                catch { }
                
                var overlay = root?.FindName("ScratchpadOverlay") as UIElement;
                if (overlay != null) overlay.Visibility = Visibility.Collapsed;
                // Save current snippet before closing if present, then clear selection
                try
                {
                    var vm = ViewModel;
                    var snip = vm?.SelectedSnippet;
                    if (vm != null && snip != null)
                    {
                        try
                        {
#if DEBUG
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] CloseScratchpadSimple: saving snippet id={snip.Id}");
#endif
                            await vm.SaveSnippetFileAsync(snip);
#if DEBUG
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] CloseScratchpadSimple: save returned for snippet id={snip.Id}");
#endif
                            
                            // UI update - SyncService removed; Refresh handles this
                            vm.Refresh();
                        }
                        catch { }
                        vm.IsDirty = false;
                    }
                }
                catch { }

                if (ViewModel != null) ViewModel.SelectedSnippet = null;
                _previousSelectedSnippet = null;
                _isAnimationActive = false;

                // Re-enable list interactions
                var list = root?.FindName("SnippetListView") as ListView;
                if (list != null)
                {
                    list.IsHitTestVisible = true;
                    list.IsItemClickEnabled = true;
                    list.SelectionMode = ListViewSelectionMode.Single;
                }

            }
            catch { }
        }

        private void ScratchpadEditor_TextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[DEBUG] ScratchpadEditor_TextChanged: called");
#endif
                var tb = sender as TextBox;
                if (tb == null)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] ScratchpadEditor_TextChanged: tb is null");
#endif
                    return;
                }
                if (ViewModel != null && ViewModel.SelectedSnippet != null)
                {
                    var selectedSnippet = ViewModel.SelectedSnippet;
                    var newContent = tb.Text ?? string.Empty;
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] ScratchpadEditor_TextChanged: snippetId={selectedSnippet.Id}, contentLength={newContent.Length}, calling UpdateContentImmediate");
#endif
                    // SyncService removed - direct property update
                    selectedSnippet.Content = newContent;
                }
                else
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] ScratchpadEditor_TextChanged: ViewModel={ViewModel != null}, SelectedSnippet={ViewModel?.SelectedSnippet != null}");
#endif
                }
            }
            catch { }
        }

        private void ScratchpadTitleBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[DEBUG] ScratchpadTitleBox_TextChanged: called");
#endif
                var tb = sender as TextBox;
                if (tb == null)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] ScratchpadTitleBox_TextChanged: tb is null");
#endif
                    return;
                }
                if (ViewModel != null && ViewModel.SelectedSnippet != null)
                {
                    var selectedSnippet = ViewModel.SelectedSnippet;
                    var newTitle = tb.Text ?? string.Empty;
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] ScratchpadTitleBox_TextChanged: snippetId={selectedSnippet.Id}, newTitle='{newTitle}', calling UpdateTitleImmediate");
#endif
                    // SyncService removed - direct property update
                    selectedSnippet.Title = newTitle;
                }
                else
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] ScratchpadTitleBox_TextChanged: ViewModel={ViewModel != null}, SelectedSnippet={ViewModel?.SelectedSnippet != null}");
#endif
                }
            }
            catch { }
        }

        private void ScratchpadTitleBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            try
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    e.Handled = true;
                    var scratchEditor = this.FindName("ScratchpadEditor") as TextBox;
                    if (scratchEditor != null)
                    {
                        scratchEditor.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                    }
                }
            }
            catch { }
        }

        private void ScratchpadContainer_PointerPressed(object? sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (!(sender is FrameworkElement container)) return;
                var rootUi = this.Content as FrameworkElement;
                if (rootUi == null) return;

                // start drag
                var pt = e.GetCurrentPoint(rootUi).Position;
                _scratchpadDragStart = pt;

                if (container.RenderTransform is TranslateTransform tt)
                {
                    _scratchpadStartX = tt.X;
                    _scratchpadStartY = tt.Y;
                }
                else
                {
                    var newT = new TranslateTransform { X = 0, Y = 0 };
                    container.RenderTransform = newT;
                    _scratchpadStartX = 0;
                    _scratchpadStartY = 0;
                }

                try { container.CapturePointer(e.Pointer); } catch { }
                _isScratchpadDragging = true;
                try { e.Handled = true; } catch { }
            }
            catch { }
        }

        private void ScratchpadContainer_PointerMoved(object? sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (!_isScratchpadDragging) return;
                if (!(sender is FrameworkElement container)) return;
                var rootUi = this.Content as FrameworkElement;
                if (rootUi == null) return;

                var pt = e.GetCurrentPoint(rootUi).Position;
                var dx = pt.X - _scratchpadDragStart.X;
                var dy = pt.Y - _scratchpadDragStart.Y;

                if (container.RenderTransform is TranslateTransform tt)
                {
                    tt.X = _scratchpadStartX + dx;
                    tt.Y = _scratchpadStartY + dy;
                }
            }
            catch { }
        }

        private void ScratchpadContainer_PointerReleased(object? sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (!(sender is FrameworkElement container)) return;
                try { container.ReleasePointerCapture(e.Pointer); } catch { }
                _isScratchpadDragging = false;
                try { e.Handled = true; } catch { }
            }
            catch { }
        }
        private void AdjustScratchpadSize()
        {
            try
            {
                var root = this.Content as FrameworkElement;
                if (root == null) return;
                var overlay = root.FindName("ScratchpadOverlay") as FrameworkElement;
                var container = root.FindName("ScratchpadContainer") as FrameworkElement;
                if (overlay == null || container == null) return;
                if (overlay.Visibility != Visibility.Visible) return;

                // constraints should match XAML defaults
                const double minW = 320.0, minH = 240.0;
                const double maxW = 900.0, maxH = 640.0;

                // Prefer to size relative to the CardPanel area so the scratchpad matches the card region.
                var cardPanel = root.FindName("CardPanel") as FrameworkElement;

                double baseW = root.ActualWidth;
                double baseH = root.ActualHeight;

                if (cardPanel != null && cardPanel.ActualWidth > 0 && cardPanel.ActualHeight > 0)
                {
                    baseW = cardPanel.ActualWidth;
                    baseH = cardPanel.ActualHeight;
                }

                // prefer 90% of available card area, clamped to min/max
                var availW = Math.Max(minW, baseW * 0.9);
                var availH = Math.Max(minH, baseH * 0.9);

                var w = Math.Min(maxW, availW);
                var h = Math.Min(maxH, availH);

                // Apply new size on UI thread
                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        container.Width = w;
                        container.Height = h;
                    }
                    catch { }
                });
            }
            catch { }
        }

        private void ScratchpadEditor_KeyDown(object? sender, KeyRoutedEventArgs e)
        {
            try
            {
                // Escape closes the scratchpad immediately
                if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    try
                    {
                        CloseScratchpadSimple();
                        e.Handled = true;
                    }
                    catch { }
                    return;
                }
                if (e.Key == Windows.System.VirtualKey.Enter && IsControlDown())
                {
                    try
                    {
                        e.Handled = true;
                        var vm = ViewModel;
                        var snip = vm?.SelectedSnippet;
                        if (vm != null && snip != null)
                        {
                            _ = vm.SaveSnippetFileAsync(snip);
                            vm.IsDirty = false;
                        }

                        App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                        {
                            try { (this as FrameworkElement)?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic); } catch { }
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
