using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;
using Windows.Foundation;
using System.Diagnostics;
using Pivot.Utilities;

namespace Pivot.CodeModule.Views
{
    public sealed partial class CodePage
    {
        private async Task OpenSnippetWithAnimationAsync()
        {
            // Ensure we run UI work on the UI thread to avoid COM RPC_E_DISCONNECTED errors.
            try
            {
                var dq = App.Current.MainWindow?.DispatcherQueue;
                if (dq != null && !dq.HasThreadAccess)
                {
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                    dq.TryEnqueue(() =>
                    {
                        try
                        {
                            _ = OpenSnippetWithAnimationAsync();
                        }
                        catch { }
                        try { tcs.SetResult(true); } catch { }
                    });
                    await tcs.Task.ConfigureAwait(false);
                    return;
                }
            }
            catch { }

            if (_isAnimationActive) { return; }
            _isAnimationActive = true;
            ListView? list = null;
            try
            {
                var root = this.Content as FrameworkElement;
                list = root?.FindName("SnippetListView") as ListView;
                var editorPanel = root?.FindName("EditorPanel") as Grid;
                var cardPanel = root?.FindName("CardPanel") as Grid;
                var placeholder = root?.FindName("PlaceholderBorder") as Border;
            // Capture DataContext/ViewModel once on UI thread to avoid COM RPC issues
            Pivot.CodeModule.ViewModels.CodeViewModel? viewModel = null;
            try
            {
                viewModel = (root?.DataContext) as Pivot.CodeModule.ViewModels.CodeViewModel;
            }
            catch { viewModel = null; }

            // Temporarily disable list interaction during open animation
            if (list != null)
            {
                list.IsHitTestVisible = false;
                list.IsItemClickEnabled = false;
            }

            UIElement? source = null;
            try
            {
                if (list != null && viewModel?.SelectedSnippet != null)
                {
                    list.ScrollIntoView(viewModel.SelectedSnippet);
                    // Wait for layout update, especially for newly created items
                    await Task.Delay(150).ConfigureAwait(false);
                    // Force layout update on UI thread
                    App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => 
                    {
                        try
                        {
                            // Force layout update by invalidating measure
                            list.InvalidateMeasure();
                            list.UpdateLayout();
                        }
                        catch { }
                    });
                    await Task.Delay(50).ConfigureAwait(false);
                    
                    ListViewItem? container = null;
                    int retryCount = 0;
                    const int maxRetries = 5;
                    
                    // Retry getting container for newly created items
                    while (container == null && retryCount < maxRetries)
                    {
                        try
                        {
                            container = list.ContainerFromItem(viewModel.SelectedSnippet) as ListViewItem;
                            if (container == null && retryCount < maxRetries - 1)
                            {
                                await Task.Delay(50).ConfigureAwait(false);
                                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => 
                                {
                                    try
                                    {
                                        list.UpdateLayout();
                                    }
                                    catch { }
                                });
                                await Task.Delay(50).ConfigureAwait(false);
                            }
                        }
                        catch
                        {
                            // ContainerFromItem threw; retry or fallback to non-animated open.
                            container = null;
                        }
                        retryCount++;
                    }
                    if (container != null)
                    {
                        // Prefer ListView helper to prepare connected animation from item -> named element inside template
                        try
                        {
                            _pendingOpenAnimation = list.PrepareConnectedAnimation("OpenSnippet", viewModel.SelectedSnippet, "SnippetCardBorder");
                            
                        }
                        catch
                        {
                            // Fallback: try to find the element and prepare via ConnectedAnimationService
                            source = TreeHelper.FindDescendantByName(container, "SnippetCardBorder") as UIElement;
                            if (source != null)
                            {
                                try { _pendingOpenAnimation = ConnectedAnimationService.GetForCurrentView()?.PrepareToAnimate("OpenSnippet", source); } catch { _pendingOpenAnimation = null; }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenSnippetWithAnimationAsync: exception during prepare: {ex}");
            }

            // show editor and hide cards — display snippet in main editor panel instead of the overlay
            try
            {
                    if (editorPanel != null)
                    {
                        editorPanel.Opacity = 0;
                        editorPanel.Visibility = Visibility.Visible;
                    }
                    if (cardPanel != null) cardPanel.Visibility = Visibility.Collapsed;
                    if (placeholder != null) placeholder.Visibility = Visibility.Collapsed;
                    // Keep overlay collapsed — we will target the editor panel for animation and focus
                }
                catch { }

            // Open scratchpad without connected animation (no animation path)
            try
            {
                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    // Show scratchpad overlay and populate from the selected snippet
                    var overlay = root?.FindName("ScratchpadOverlay") as Grid;
                    var cardPanelLocal = root?.FindName("CardPanel") as FrameworkElement;
                    // Ensure the parent panel is visible so the overlay can render
                    if (cardPanelLocal != null) cardPanelLocal.Visibility = Visibility.Visible;
                    if (overlay != null) overlay.Visibility = Visibility.Visible;
                    // Ensure overlay-level handlers are registered with handledEventsToo so
                    // clicks inside child elements don't prevent the overlay from seeing outside clicks.
                    if (overlay != null)
                    {
                        try
                        {
                            overlay.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ScratchpadOverlay_PointerPressed));
                        }
                        catch { }
                        try
                        {
                            overlay.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ScratchpadOverlay_PointerPressed), true);
                        }
                        catch { }
                        try
                        {
                            overlay.RemoveHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ScratchpadOverlay_PointerPressed));
                        }
                        catch { }
                        try
                        {
                            overlay.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ScratchpadOverlay_PointerPressed), true);
                        }
                        catch { }
                    }
                    var scratchEditor = this.FindName("ScratchpadEditor") as TextBox;
                    var titleBox = this.FindName("ScratchpadTitleBox") as TextBox;
                    if (viewModel?.SelectedSnippet != null)
                    {
                        if (scratchEditor != null) scratchEditor.Text = viewModel.SelectedSnippet.Content ?? string.Empty;
                        if (titleBox != null) titleBox.Text = viewModel.SelectedSnippet.Title ?? string.Empty;
                    }
                    // adjust size to fit current page/window
                    try { AdjustScratchpadSize(); } catch { }
                    if (scratchEditor != null) scratchEditor.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                });
            }
            catch { }

            // attach pointer handler to detect clicks outside editor
            try
            {
                var rootUi = this.Content as UIElement;
                if (rootUi != null)
                {
                    // Normal attachment
                    rootUi.PointerPressed -= Root_PointerPressed;
                    rootUi.PointerPressed += Root_PointerPressed;

                    // Also register handledEventsToo so we catch clicks even if children mark events handled
                    try
                    {
                        rootUi.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(Root_PointerPressed));
                    }
                    catch { }
                    try
                    {
                        rootUi.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(Root_PointerPressed), true);
                    }
                    catch { }

                    // Also listen for PointerReleased as a reliable fallback for clicks that don't reach Pressed
                    try
                    {
                        rootUi.PointerReleased -= Root_PointerReleased;
                        rootUi.PointerReleased += Root_PointerReleased;
                    }
                    catch { }

                    try
                    {
                        rootUi.RemoveHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(Root_PointerReleased));
                    }
                    catch { }
                    try
                    {
                        rootUi.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(Root_PointerReleased), true);
                    }
                    catch { }
                }
            }
            catch { }

            // snippet content already posted to available editors

            _previousSelectedSnippet = viewModel?.SelectedSnippet;
            _isAnimationActive = false;

            await Task.CompletedTask;
            return;
            }
            finally
            {
                try 
                { 
                    if (list != null) 
                    {
                        list.IsHitTestVisible = true;
                        list.IsItemClickEnabled = true;
                    }
                } 
                catch { }
                try { _isAnimationActive = false; } catch { }
            }
        }

        private async Task CloseSnippetWithAnimationAsync(bool skipRefreshAfterSave = false)
        {
            if (_isAnimationActive) return;
            _isAnimationActive = true;
            // Debug: mark close start and persist current/previous snippet before closing so cards reflect changes.
            var root = this.Content as FrameworkElement;
            try
            {
                // エディタの内容をSelectedSnippetに反映
                try
                {
                    var scratchEditor = root?.FindName("ScratchpadEditor") as TextBox;
                    var titleBox = root?.FindName("ScratchpadTitleBox") as TextBox;
                    var vmForSync = ViewModel;
                    var snipForSync = vmForSync?.SelectedSnippet;
                    
                    if (vmForSync != null && snipForSync != null)
                    {
                        if (scratchEditor != null)
                        {
                            snipForSync.Content = scratchEditor.Text ?? string.Empty;
                        }
                        if (titleBox != null)
                        {
                            snipForSync.Title = titleBox.Text ?? string.Empty;
                        }
                    }
                }
                catch { }
                
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[DEBUG] CloseSnippetWithAnimationAsync: start. prevSelectedId={_previousSelectedSnippet?.Id}, currentSelectedId={ViewModel?.SelectedSnippet?.Id}");
#endif
                var vm = ViewModel;
                var toSave = vm?.SelectedSnippet ?? _previousSelectedSnippet;
                if (vm != null && toSave != null)
                {
                    try
                    {
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] CloseSnippetWithAnimationAsync: saving snippet id={toSave.Id}");
#endif
                        await vm.SaveSnippetFileAsync(toSave);
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] CloseSnippetWithAnimationAsync: save completed for snippet id={toSave.Id}");
#endif
                    }
                    catch { }
                }
            }
            catch { }
            var list = root?.FindName("SnippetListView") as ListView;
            var editorPanel = root?.FindName("EditorPanel") as Grid;
            var cardPanel = root?.FindName("CardPanel") as Grid;
            var placeholder = root?.FindName("PlaceholderBorder") as Border;

            // Disable list interaction to prevent re-selection during close
            if (list != null)
            {
                list.IsItemClickEnabled = false;
                list.SelectionMode = ListViewSelectionMode.None;
            }

            try
            {
                var overlayContainer = root?.FindName("ScratchpadContainer") as UIElement;
                if (overlayContainer != null)
                {
                    try { _pendingCloseAnimation = ConnectedAnimationService.GetForCurrentView()?.PrepareToAnimate("CloseSnippet", overlayContainer); } catch { _pendingCloseAnimation = null; }
                }
                else if (editorPanel != null)
                {
                    try { _pendingCloseAnimation = ConnectedAnimationService.GetForCurrentView()?.PrepareToAnimate("CloseSnippet", editorPanel); } catch { _pendingCloseAnimation = null; }
                }

                // make list visible so target can be found
                if (cardPanel != null) cardPanel.Visibility = Visibility.Visible;
                await Task.Delay(60).ConfigureAwait(false);
                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => { });

                if (list != null && _previousSelectedSnippet != null)
                {
                    try
                    {
                        // Prefer the ListView helper which handles scrolling and layout for us
                        if (_pendingCloseAnimation != null)
                        {
                            try
                            {
                        
                                await list.TryStartConnectedAnimationAsync(_pendingCloseAnimation, _previousSelectedSnippet, "SnippetCardBorder");
                                _pendingCloseAnimation = null;
                            }
                            catch (System.Runtime.InteropServices.COMException)
                            {
                                
                                // Fallback to manual TryStart on target if helper fails
                                var container = list.ContainerFromItem(_previousSelectedSnippet) as ListViewItem;
                                if (container != null)
                                {
                                    var target = TreeHelper.FindDescendantByName(container, "SnippetCardBorder") as UIElement;
                                    if (target != null)
                                    {
                                        try { if (_pendingCloseAnimation != null) _pendingCloseAnimation.TryStart(target); } catch { }
                                        _pendingCloseAnimation = null;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        
                    }
                }
            }
            catch { }

            try
            {
                if (editorPanel != null) editorPanel.Visibility = Visibility.Collapsed;
                if (placeholder != null) placeholder.Visibility = ViewModel?.Snippets != null && ViewModel.Snippets.Any() ? Visibility.Collapsed : Visibility.Visible;
                // hide scratchpad overlay
                try
                {
                    var overlay = root?.FindName("ScratchpadOverlay") as Grid;
                    if (overlay != null) overlay.Visibility = Visibility.Collapsed;
                }
                catch { }

                // enforce overlay collapse on UI thread as a final safety
                try
                {
                    App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                    {
                        try
                        {
                            var overlay2 = this.FindName("ScratchpadOverlay") as Grid;
                            if (overlay2 != null) overlay2.Visibility = Visibility.Collapsed;
                        }
                        catch { }
                    });
                }
                catch { }

                // re-enable close button after close
                try
                {
                    var closeBtn = this.FindName("ScratchpadCloseButton") as Button;
                    if (closeBtn != null) closeBtn.IsEnabled = true;
                }
                catch { }

                // Re-enable list interaction after close
                if (list != null)
                {
                    list.IsItemClickEnabled = true;
                    list.SelectionMode = ListViewSelectionMode.Single;
                    list.IsHitTestVisible = true;
                }
                // Ensure CardPanel is visible and hit testable after closing the snippet
                if (cardPanel != null)
                {
                    cardPanel.Visibility = Visibility.Visible;
                    cardPanel.IsHitTestVisible = true;
                }
            }
            catch { }

            // detach pointer handler
            try
            {
                var rootUi = this.Content as UIElement;
                if (rootUi != null)
                {
                    try { rootUi.PointerPressed -= Root_PointerPressed; } catch { }
                    try { rootUi.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(Root_PointerPressed)); } catch { }
                    try { rootUi.PointerReleased -= Root_PointerReleased; } catch { }
                    try { rootUi.RemoveHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(Root_PointerReleased)); } catch { }
                    try
                    {
                        var overlay = (this.Content as FrameworkElement)?.FindName("ScratchpadOverlay") as UIElement;
                        if (overlay != null)
                        {
                            try { overlay.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ScratchpadOverlay_PointerPressed)); } catch { }
                            try { overlay.RemoveHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ScratchpadOverlay_PointerPressed)); } catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // auto-save previous snippet on close
            try
            {
                var prev = _previousSelectedSnippet;
                if (prev != null)
                {
                    try
                    {
                        var vm = ViewModel;
                        if (vm != null) await vm.SaveSnippetFileAsync(prev, refreshAfterSave: !skipRefreshAfterSave);
                    }
                    catch { }
                }
            }
            catch { }

            _previousSelectedSnippet = null;
            _isAnimationActive = false;

        }
    }
}
