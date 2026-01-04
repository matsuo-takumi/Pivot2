using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Pivot.Utilities;
using Pivot.CodeModule.ViewModels;
using Pivot.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Composition;
using WinRT.Interop;
using WinRT;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.Graphics;

namespace Pivot.CodeModule.Views
{
    public sealed partial class CodePage
    {
        private void CodePage_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Unregister messenger
                if (_registeredMessenger != null)
                {
                    _registeredMessenger.UnregisterAll(this);
                    _registeredMessenger = null;
                }

                // Unregister collection changed handler
                if (_snippetsCollectionChangedHandler != null && ViewModel?.Snippets != null)
                {
                    ViewModel.Snippets.CollectionChanged -= _snippetsCollectionChangedHandler;
                    _snippetsCollectionChangedHandler = null;
                }

                // Unregister property changed handler
                if (DataContext is INotifyPropertyChanged pc)
                {
                    pc.PropertyChanged -= OnViewModelPropertyChanged;
                }

                // Unregister editor handlers
                EnsureUIElementsCached();
                if (_codeEditor != null)
                {
                    _codeEditor.TextChanged -= CodeEditor_TextChanged;
                }
                if (_scratchpadEditor != null)
                {
                    _scratchpadEditor.TextChanged -= ScratchpadEditor_TextChanged;
                }

                // Unregister size changed handler
                var rootGrid = this.FindName("CodePageRoot") as FrameworkElement;
                if (rootGrid != null)
                {
                    rootGrid.SizeChanged -= RootGrid_SizeChanged;
                }

                // Unregister overlay pointer handler
                if (_scratchpadOverlay != null)
                {
                    _scratchpadOverlay.PointerPressed -= ScratchpadOverlay_PointerPressed;
                }

                // Save current snippet before unloading
                if (ViewModel != null && ViewModel.SelectedSnippet != null)
                {
                    try
                    {
                        _ = ViewModel.SaveSnippetFileAsync(ViewModel.SelectedSnippet, refreshAfterSave: false);
                    }
                    catch { }
                }
                
                // SyncService removed in architecture consolidation
                // Saves now happen through CodeService
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodePage_Unloaded: Error during cleanup: {ex.Message}");
            }
        }

        // Text-based editors (TextBox) are used instead of WebView2. Text change events update the ViewModel.

        private void OnSearchClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel?.Refresh();
        }

        private void OnAddClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                // Delegate to ViewModel
                ViewModel?.AddSnippetCommand.Execute(null);
                
                if (ViewModel?.SelectedSnippet != null)
                {
                    ViewModel.SelectedSnippet.Title = "New Snippet";
                    ViewModel.SelectedSnippet.Language = "Python";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnAddClicked: Error: {ex.Message}");
            }
        }

        private void TagFilterBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            try
            {
                ViewModel?.FilterTags(sender.Text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TagFilterBox_TextChanged: Error: {ex.Message}");
            }
        }

        private void NewTagBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter) return;
            if (ViewModel?.AddTagCommand?.CanExecute(null) == true)
            {
                ViewModel.AddTagCommand.Execute(null);
                e.Handled = true;
            }
        }

        private volatile bool _isAnimationActive = false;
        private ConnectedAnimation? _pendingOpenAnimation;
        private ConnectedAnimation? _pendingCloseAnimation;

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CodeViewModel.SelectedSnippet))
            {
                
                // Do not auto-save on selection change; saving occurs on explicit Save or on close.

                _ = SendSelectedSnippetToEditorAsync();

                // decide which animation path to run
                if (ViewModel?.SelectedSnippet != null)
                {
                    // Open inline overlay instead of a separate OS window
                    _ = OpenSnippetWithAnimationAsync();
                }
                else
                {
                    _ = CloseSnippetWithAnimationAsync();
                }
                // Persist last selected snippet id for restoration across page instances
                try
                {
                    var browserSettings = App.Current.Services.GetService(typeof(Pivot.Services.BrowserSettingsService)) as Pivot.Services.BrowserSettingsService;
                    if (browserSettings != null)
                    {
                        var id = ViewModel?.SelectedSnippet?.Id;
                        _ = browserSettings.SetLastSelectedSnippetIdAsync(id);
                    }
                }
                catch { }
            }
        }

        private void BuildNavigationMenu()
        {
            try
            {
                // Delegate navigation item building to ViewModel
                ViewModel?.BuildNavigationItems();
                
                // Bind NavigationView to ViewModel's NavigationItems if not already bound
                EnsureUIElementsCached();
                if (_codeNav != null && ViewModel != null)
                {
                    // Build menu items from ViewModel's NavigationItems collection
                    _codeNav.MenuItems.Clear();
                    foreach (var item in ViewModel.NavigationItems)
                    {
                        try
                        {
                            var ni = new NavigationViewItem 
                            { 
                                Content = item.Name, 
                                Tag = item.IsAllSnippets ? "all" : item.FilterId.ToString(),
                                Icon = new SymbolIcon(item.IsAllSnippets ? Symbol.AllApps : Symbol.Tag)
                            };
                            
                            // Add context menu for filter items (non-All items)
                            if (!item.IsAllSnippets)
                            {
                                var menuFlyout = new MenuFlyout();
                                var filterId = item.FilterId;
                                var filterName = item.Name;
                                
                                var editMenuItem = new MenuFlyoutItem { Text = "編集" };
                                editMenuItem.Click += async (s, e) => 
                                {
                                    try
                                    {
                                        await EditTagAsync(filterId, filterName);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"EditTag: Error: {ex.Message}");
                                    }
                                };
                                menuFlyout.Items.Add(editMenuItem);
                                
                                var deleteMenuItem = new MenuFlyoutItem { Text = "削除" };
                                deleteMenuItem.Click += async (s, e) => 
                                {
                                    try
                                    {
                                        await DeleteTagAsync(filterId, filterName);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"DeleteTag: Error: {ex.Message}");
                                    }
                                };
                                menuFlyout.Items.Add(deleteMenuItem);
                                
                                ni.ContextFlyout = menuFlyout;
                            }
                            
                            _codeNav.MenuItems.Add(ni);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"BuildNavigationMenu: Error adding item '{item.Name}': {ex.Message}");
                        }
                    }
                    
                    // Select first item (All Snippets) by default
                    if (_codeNav.MenuItems.Count > 0)
                    {
                        _codeNav.SelectedItem = _codeNav.MenuItems[0];
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BuildNavigationMenu: Error: {ex.Message}");
            }
        }


        private void TrashButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel == null) return;
                ViewModel.ShowDeletedSnippets();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TrashButton_Click: Error: {ex.Message}");
            }
        }

        private void CodeTagCheckBox_Toggled(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (!(sender is CheckBox cb)) return;
                var tagName = cb.Content?.ToString() ?? string.Empty;
                var isSelected = cb.IsChecked == true;
                var messenger = App.Current.Services.GetService(typeof(IMessenger)) as IMessenger;
                messenger?.Send(new TagSelectionMessage("Code", tagName, isSelected));
            }
            catch { }
        }

        private void CodeNav_ItemInvoked(object sender, NavigationViewItemInvokedEventArgs args)
        {
            try
            {
                if (args.IsSettingsInvoked) return;
                var item = args.InvokedItemContainer as NavigationViewItem;
                if (item == null || ViewModel == null) return;

                var tagStr = item.Tag?.ToString() ?? string.Empty;
                
                // Handle "All Snippets" selection
                if (string.Equals(tagStr, "all", StringComparison.OrdinalIgnoreCase))
                {
                    ViewModel.ShowAllSnippetsCommand.Execute(null);
                    if (ViewModel.SelectedSnippet != null) 
                    {
                        _ = CloseSnippetWithAnimationAsync(skipRefreshAfterSave: true);
                    }
                    return;
                }

                // Handle "Trash" selection
                if (string.Equals(tagStr, "trash", StringComparison.OrdinalIgnoreCase))
                {
                    ViewModel.ShowDeletedSnippets();
                    return;
                }

                // Handle filter selection by GUID
                if (Guid.TryParse(tagStr, out var filterId))
                {
                    ViewModel.SelectFilterCommand.Execute(filterId);
                    if (ViewModel.SelectedSnippet != null) 
                    {
                        _ = CloseSnippetWithAnimationAsync(skipRefreshAfterSave: true);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodeNav_ItemInvoked: Error: {ex.Message}");
            }
        }

        // Open the selected snippet in a new OS-level window. Save on close and clear selection.
        private Task ShowSnippetWindowAsync()
        {
            // Note: This feature opens a separate AppWindow. 
            // In the current single-window design, we are using the overlay (OpenSnippetWithAnimationAsync).
            // However, keeping this method or adapting it if needed.
            // For now, this implementation creates a new Window for the snippet.

            try
            {
                var vm = ViewModel;
                if (vm?.SelectedSnippet == null) return Task.CompletedTask;

                var snippet = vm.SelectedSnippet;
                
                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        var panelWidth = 800.0;
                        var panelHeight = 600.0;

                        var titleText = new TextBlock 
                        { 
                            Text = snippet.Title ?? "Snippet", 
                            Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style,
                            Margin = new Thickness(0,0,0,12)
                        };
                        
                        var contentBox = new TextBox 
                        { 
                            Text = snippet.Content ?? "", 
                            AcceptsReturn = true, 
                            TextWrapping = TextWrapping.Wrap,
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch,
                            Height = double.NaN 
                        };
                        // contentBox should fill remaining space
                        Grid.SetRow(contentBox, 1);
                        
                        // We need a simple grid layout
                        // But here we use StackPanel for simplicity as in original code, or upgrade to Grid
                        // Original code used StackPanel with fixed width? 
                        
                        // Let's use the code from trace:
                        // var panel = new StackPanel { Spacing = 8, Padding = new Thickness(12), Width = panelWidth };
                        // ...
                        // Since we are moving exact code, use exact logic.

                        // Reconstruct based on trace:
                        var panel = new StackPanel { Spacing = 8, Padding = new Thickness(12), Width = panelWidth };
                        panel.Children.Add(titleText);
                        panel.Children.Add(contentBox);

                        try { panel.RequestedTheme = this.RequestedTheme; } catch { }

                        var wnd = new Window();
                        wnd.Content = panel;

                        try
                        {
                            var hwnd = WindowNative.GetWindowHandle(wnd);
                            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                            var appWindow = AppWindow.GetFromWindowId(windowId);
                            if (appWindow != null)
                            {
                                try { appWindow.Resize(new SizeInt32 { Width = (int)panelWidth + 24, Height = (int)panelHeight + 48 }); } catch { }

                                try
                                {
                                    try { DispatcherQueue.EnsureSystemDispatcherQueue(); } catch { }
                                    var config = new SystemBackdropConfiguration();
                                    config.IsInputActive = true;
                                    try
                                    {
                                        switch (panel.ActualTheme)
                                        {
                                            case ElementTheme.Dark: config.Theme = SystemBackdropTheme.Dark; break;
                                            case ElementTheme.Light: config.Theme = SystemBackdropTheme.Light; break;
                                            default: config.Theme = SystemBackdropTheme.Default; break;
                                        }
                                    }
                                    catch { }
                                    var mica = new MicaController();
                                    mica.Kind = MicaKind.Base;
                                    try { mica.AddSystemBackdropTarget(wnd.As<ICompositionSupportsSystemBackdrop>()); } catch { }
                                    mica.SetSystemBackdropConfiguration(config);

                                    Windows.Foundation.TypedEventHandler<FrameworkElement, object>? themeChangedHandler = null;
                                    themeChangedHandler = (FrameworkElement s, object ev) =>
                                    {
                                        try
                                        {
                                            switch (panel.ActualTheme)
                                            {
                                                case ElementTheme.Dark: config.Theme = SystemBackdropTheme.Dark; break;
                                                case ElementTheme.Light: config.Theme = SystemBackdropTheme.Light; break;
                                                default: config.Theme = SystemBackdropTheme.Default; break;
                                            }
                                        }
                                        catch { }
                                    };
                                    try { panel.ActualThemeChanged += themeChangedHandler; } catch { }

                                    wnd.Closed += (_, __) =>
                                    {
                                        try { mica.Dispose(); } catch { }
                                        try { if (themeChangedHandler != null) panel.ActualThemeChanged -= themeChangedHandler; } catch { }
                                    };
                                }
                                catch { }
                            }
                        }
                        catch { }

                        void RestoreListInteraction()
                        {
                            try
                            {
                                var root = this.Content as FrameworkElement;
                                var list = root?.FindName("SnippetListView") as ListView;
                                if (list != null)
                                {
                                    list.IsItemClickEnabled = true;
                                    list.IsHitTestVisible = true;
                                    list.SelectionMode = ListViewSelectionMode.Single;
                                    list.SelectedItem = null;
                                }
                                _previousSelectedSnippet = null;
                            }
                            catch { }
                        }

                        try
                        {
                        wnd.Activated += (s, args) =>
                        {
                            try
                            {
                                if (args.WindowActivationState == WindowActivationState.Deactivated)
                                {
                                    try
                                    {
                                        if (snippet != null)
                                        {
                                            snippet.Content = contentBox.Text ?? string.Empty;
                                            if (vm != null) _ = vm.SaveSnippetFileAsync(snippet);
                                        }
                                    }
                                    catch { }

                                    try { RestoreListInteraction(); } catch { }
                                }
                            }
                            catch { }
                        };
                        }
                        catch { }

                        try
                        {
                            var mainWnd = App.Current.MainWindow as Window;
                            if (mainWnd != null)
                            {
                                Windows.Foundation.TypedEventHandler<object, Microsoft.UI.Xaml.WindowEventArgs>? mainClosedHandler = null;
                                mainClosedHandler = (ms, me) =>
                                {
                                    try { wnd.Close(); } catch { }
                                    try { mainWnd.Closed -= mainClosedHandler; } catch { }
                                };
                                try { mainWnd.Closed += mainClosedHandler; } catch { }
                            }
                        }
                        catch { }

                        wnd.Closed += (_, __) =>
                        {
                            try
                            {
                                if (snippet != null)
                                {
                                    snippet.Content = contentBox.Text ?? string.Empty;
                                    if (vm != null) _ = vm.SaveSnippetFileAsync(snippet);
                                }
                            }
                            catch { }
                            try { if (vm != null) vm.SelectedSnippet = null; } catch { }
                            try { RestoreListInteraction(); } catch { }
                        };

                        wnd.Activate();
                    }
                    catch { }
                });
            }
            catch { }
            return Task.CompletedTask;
        }

        private async void SnippetListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Ensure ListView is clickable before processing click
            try
            {
                var list = sender as ListView;
                if (list != null)
                {
                    list.IsItemClickEnabled = true;
                    list.IsHitTestVisible = true;
                }
            }
            catch { }
            
            if (_isAnimationActive) 
            { 
                // Reset animation flag if stuck
                _isAnimationActive = false;
                return; 
            }
            
            // If a snippet is already open, close it before opening another
            if (ViewModel != null && ViewModel.SelectedSnippet != null)
            {
                await CloseSnippetWithAnimationAsync();
            }
            if (e.ClickedItem is Pivot.CodeModule.Models.CodeFile clickedSnippet)
            {
                if (ViewModel != null)
                {
                    // Select clicked snippet for editing
                    ViewModel.SelectedSnippet = clickedSnippet;
                    // Fallback: explicitly push content to the editor and open it in case ViewModel change didn't trigger the UI update
                    try
                    {
                        _ = SendSelectedSnippetToEditorAsync();
                        _ = OpenSnippetWithAnimationAsync();
                    }
                    catch 
                    {
                        // Ensure clickability is restored even if animation fails
                        try
                        {
                            var list = sender as ListView;
                            if (list != null)
                            {
                                list.IsItemClickEnabled = true;
                                list.IsHitTestVisible = true;
                                _isAnimationActive = false;
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        // Quick add: commit on blur or Ctrl+Enter
        private void QuickAddBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                _isQuickAddTabMode = false;
                CommitQuickAdd();
            }
            catch { }
        }

        private void QuickAddBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[CodePage] QuickAddBox_KeyDown: Key = {e.Key}");
            try
            {
                // If QuickAdd has special tab mode enabled, intercept Tab to insert a tab character
                if (_isQuickAddTabMode && e.Key == Windows.System.VirtualKey.Tab)
                {
                    try
                    {
                        if (sender is TextBox tb)
                        {
                            var pos = tb.SelectionStart;
                            tb.Text = tb.Text.Insert(pos, "\t");
                            tb.SelectionStart = pos + 1;
                        }
                        e.Handled = true; // prevent moving focus to other components
                        return;
                    }
                    catch { }
                }

                var isShiftDown = IsShiftDown();
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    System.Diagnostics.Debug.WriteLine($"[CodePage] QuickAddBox_KeyDown: Enter pressed, Shift={isShiftDown}");
                    if (isShiftDown)
                    {
                        // Shift+Enter: insert newline
                        try
                        {
                            if (sender is TextBox tb)
                            {
                                var pos = tb.SelectionStart;
                                var len = tb.SelectionLength;
                                var text = tb.Text ?? string.Empty;
                                // Remove selected text and insert newline
                                if (len > 0)
                                {
                                    text = text.Remove(pos, len);
                                }
                                tb.Text = text.Insert(pos, "\r\n");
                                tb.SelectionStart = pos + 2;
                                tb.SelectionLength = 0;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"QuickAddBox_KeyDown: Error inserting newline: {ex.Message}");
                        }
                        e.Handled = true;
                        return;
                    }
                    else
                    {
                        // Enter: commit
                        e.Handled = true;
                        CommitQuickAdd();

                    // Move focus away from the QuickAddBox so it is effectively blurred.
                    // Use DispatcherQueue to ensure this runs after any UI changes caused by CommitQuickAdd.
                    try
                    {
                        App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                        {
                            try
                            {
                                var root2 = this.Content as FrameworkElement;
                                var list2 = root2?.FindName("SnippetListView") as ListView;
                                if (list2 != null)
                                {
                                    // If there's a selected item (the newly created snippet), scroll to it and focus its container.
                                    var sel = list2.SelectedItem ?? (list2.Items.Count > 0 ? list2.Items[0] : null);
                                    if (sel != null)
                                    {
                                        try { list2.ScrollIntoView(sel); } catch { }
                                        var container = list2.ContainerFromItem(sel) as ListViewItem;
                                        if (container != null)
                                        {
                                            try { container.Focus(Microsoft.UI.Xaml.FocusState.Programmatic); } catch { }
                                        }
                                        else
                                        {
                                            try { list2.Focus(Microsoft.UI.Xaml.FocusState.Programmatic); } catch { }
                                        }
                                    }
                                    else
                                    {
                                        try { list2.Focus(Microsoft.UI.Xaml.FocusState.Programmatic); } catch { }
                                    }
                                }
                                else
                                {
                                    (this as FrameworkElement)?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                                }
                            }
                            catch { }
                        });
                    }
                    catch { }
                    }
                }
            }
            catch { }
        }

        private async void CommitQuickAdd()
        {
            System.Diagnostics.Debug.WriteLine("[CodePage] CommitQuickAdd called");
            try
            {
                EnsureUIElementsCached();
                if (_quickAddBox == null)
                {
                    System.Diagnostics.Debug.WriteLine("[CodePage] CommitQuickAdd: _quickAddBox is null");
                    return;
                }
                var text = (_quickAddBox.Text ?? string.Empty).Trim();
                System.Diagnostics.Debug.WriteLine($"[CodePage] CommitQuickAdd: text = '{text}'");
                if (string.IsNullOrWhiteSpace(text))
                {
                    System.Diagnostics.Debug.WriteLine("[CodePage] CommitQuickAdd: text is empty, returning");
                    return;
                }

                // Use ViewModel to create and save snippet
                var vm = DataContext as CodeViewModel;
                if (vm == null)
                {
                    System.Diagnostics.Debug.WriteLine("[CodePage] CommitQuickAdd: ViewModel is null");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[CodePage] CommitQuickAdd: Creating snippet...");
                var newSnippet = await vm.CreateSnippetFromTextAsync(text);

                // Handle UI updates (scroll into view)
                if (newSnippet != null)
                {
                    try
                    {
                        var list = this.FindName("SnippetListView") as ListView;
                        if (list != null)
                        {
                            list.UpdateLayout();
                            list.ScrollIntoView(newSnippet);
                        }
                    }
                    catch { }
                }

                // Clear input
                _quickAddBox.Text = string.Empty;

                // Show saved overlay
                try { ShowCardSavedOverlay(); } catch { }
            }
            catch { }
        }

        private async void Root_PointerPressed(object? sender, PointerRoutedEventArgs e)
        {
            try
            {
                var rootUi = this.Content as FrameworkElement;
                if (rootUi == null) return;
                // Determine overlay visibility early so we only ignore list/card clicks when overlay is NOT visible.
                var overlay = rootUi.FindName("ScratchpadOverlay") as FrameworkElement;
                bool overlayVisible = overlay != null && overlay.Visibility == Visibility.Visible;
                try
                {
                    var orig = e.OriginalSource as DependencyObject;
                    if (orig != null)
                    {
                        var fromListOrCard = TreeHelper.IsAncestorNamed(orig, "SnippetListView") || TreeHelper.IsAncestorNamed(orig, "SnippetCardBorder");
                        // If the click originated in the list/card AND the overlay is NOT visible,
                        // treat it as a normal list click (ignore here). If overlay is visible, do NOT ignore.
                        if (fromListOrCard && !overlayVisible)
                        {
                            return;
                        }
                    }
                }
                catch { }
                if (overlay != null && overlay.Visibility == Visibility.Visible)
                {
                    var container = rootUi.FindName("ScratchpadContainer") as FrameworkElement;
                    // If the container is missing or not measured yet, treat this as an outside click
                    // so the snippet closes instead of ignoring the input.
                    if (container == null || container.ActualWidth <= 0 || container.ActualHeight <= 0)
                    {
                        
                        if (ViewModel != null) ViewModel.SelectedSnippet = null;
                        try { await CloseSnippetWithAnimationAsync(); } catch { }
                        return;
                    }
                    var pt = e.GetCurrentPoint(rootUi).Position;
                    var transform = container.TransformToVisual(rootUi);
                    var bounds = transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
                    if (!bounds.Contains(pt))
                    {
                        
                        // Clear selection on viewmodel and trigger close animation
                        if (ViewModel != null) ViewModel.SelectedSnippet = null;
                        try { await CloseSnippetWithAnimationAsync(); } catch { }
                    }
                    return;
                }

                var editorPanel = rootUi.FindName("EditorPanel") as FrameworkElement;
                if (editorPanel == null) return;

                var pt2 = e.GetCurrentPoint(rootUi).Position;
                var transform2 = editorPanel.TransformToVisual(rootUi);
                var bounds2 = transform2.TransformBounds(new Rect(0, 0, editorPanel.ActualWidth, editorPanel.ActualHeight));
                if (!bounds2.Contains(pt2))
                {
                        
                    // click outside editor -> close
                    if (ViewModel != null) ViewModel.SelectedSnippet = null;
                    try { await CloseSnippetWithAnimationAsync(); } catch { }
                }
            }
            catch { }
        }

        // Fallback: handle PointerReleased to catch cases where Pressed was handled by child
        private async void Root_PointerReleased(object? sender, PointerRoutedEventArgs e)
        {
            try
            {
                var rootUi = this.Content as FrameworkElement;
                if (rootUi == null) return;
                var overlay = rootUi.FindName("ScratchpadOverlay") as FrameworkElement;
                if (overlay != null && overlay.Visibility == Visibility.Visible)
                {
                    var container = rootUi.FindName("ScratchpadContainer") as FrameworkElement;
                    if (container == null || container.ActualWidth <= 0 || container.ActualHeight <= 0)
                    {
                        
                        if (ViewModel != null) ViewModel.SelectedSnippet = null;
                        try { await CloseSnippetWithAnimationAsync(); } catch { }
                        return;
                    }
                    var pt = e.GetCurrentPoint(rootUi).Position;
                    var transform = container.TransformToVisual(rootUi);
                    var bounds = transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
                    if (!bounds.Contains(pt))
                    {
                        
                        if (ViewModel != null) ViewModel.SelectedSnippet = null;
                        try { await CloseSnippetWithAnimationAsync(); } catch { }
                    }
                    return;
                }

                var editorPanel = rootUi.FindName("EditorPanel") as FrameworkElement;
                if (editorPanel == null) return;

                var pt2 = e.GetCurrentPoint(rootUi).Position;
                var transform2 = editorPanel.TransformToVisual(rootUi);
                var bounds2 = transform2.TransformBounds(new Rect(0, 0, editorPanel.ActualWidth, editorPanel.ActualHeight));
                if (!bounds2.Contains(pt2))
                {
                        
                    if (ViewModel != null) ViewModel.SelectedSnippet = null;
                    try { await CloseSnippetWithAnimationAsync(); } catch { }
                }
            }
            catch { }
        }

        private void OnCodeFiltersUpdated(List<Pivot.Models.CustomFilter> filters)
        {
            try
            {
                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    try { BuildNavigationMenu(); } catch { }
                    try { RefreshSnippetCollectionView(); } catch { }
                });
            }
            catch { }
        }

        private void RefreshSnippetCollectionView()
        {
            try
            {
                var vm = ViewModel;
                if (vm?.Snippets == null) return;
                var snapshot = vm.Snippets.ToList();
                var selectedId = vm.SelectedSnippet?.Id ?? Guid.Empty;
                vm.Snippets = new System.Collections.ObjectModel.ObservableCollection<Pivot.CodeModule.Models.CodeFile>(snapshot);
                if (selectedId != Guid.Empty)
                {
                    var selected = vm.Snippets.FirstOrDefault(s => s.Id == selectedId);
                    if (selected != null)
                    {
                        vm.SelectedSnippet = selected;
                    }
                }
            }
            catch { }
        }

        private void EditorCollapseButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var root = this.Content as FrameworkElement;
                var editorPanel = root?.FindName("EditorPanel") as FrameworkElement;
                var cardPanel = root?.FindName("CardPanel") as FrameworkElement;
                if (editorPanel != null) editorPanel.Visibility = Visibility.Collapsed;
                if (cardPanel != null) cardPanel.Visibility = Visibility.Visible;
                // Do not change selection; closing is separate action
            }
            catch { }
        }

        private Task SendSelectedSnippetToEditorAsync()
        {
            if (CodeEditor == null) return Task.CompletedTask;
            var vm = ViewModel;
            if (vm?.SelectedSnippet == null) return Task.CompletedTask;
            try
            {
                var content = vm.SelectedSnippet.Content ?? string.Empty;
                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        var codeEditor = this.FindName("CodeEditor") as TextBox;
                        var scratchEditor = this.FindName("ScratchpadEditor") as TextBox;
                        var titleBox = this.FindName("ScratchpadTitleBox") as TextBox;
                        if (codeEditor != null) codeEditor.Text = content;
                            if (scratchEditor != null)
                            {
                                scratchEditor.Text = content;
                            }
                        if (titleBox != null) titleBox.Text = vm.SelectedSnippet?.Title ?? string.Empty;
                    }
                    catch { }
                });
            }
            catch { }
            return Task.CompletedTask;
        }

        private void CodeEditor_TextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[DEBUG] CodeEditor_TextChanged: called");
#endif
                var tb = sender as TextBox;
                if (tb == null)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] CodeEditor_TextChanged: tb is null");
#endif
                    return;
                }
                if (ViewModel != null && ViewModel.SelectedSnippet != null)
                {
                    var selectedSnippet = ViewModel.SelectedSnippet;
                    var newContent = tb.Text ?? string.Empty;
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] CodeEditor_TextChanged: snippetId={selectedSnippet.Id}, contentLength={newContent.Length}");
#endif
                    // SyncService removed - direct property update
                    selectedSnippet.Content = newContent;
                }
                else
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] CodeEditor_TextChanged: ViewModel={ViewModel != null}, SelectedSnippet={ViewModel?.SelectedSnippet != null}");
#endif
                }
            }
            catch { }
        }

        private async void CardDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(sender is FrameworkElement fe)) return;
                Guid id = Guid.Empty;
                try { if (fe.Tag is Guid gid) id = gid; else if (Guid.TryParse(fe.Tag?.ToString(), out var parsed)) id = parsed; } catch { }
                var snippet = (fe.DataContext as Pivot.CodeModule.Models.CodeFile);
                if (snippet == null && id != Guid.Empty)
                {
                    snippet = ViewModel?.Snippets?.FirstOrDefault(s => s.Id == id);
                }
                if (snippet == null || snippet.Id == Guid.Empty) return;
                
                // Delegate to ViewModel command
                var wasSelected = ViewModel?.SelectedSnippet == snippet;
                (DataContext as CodeViewModel)?.DeleteSnippetByIdCommand.Execute(snippet);
                
                if (wasSelected)
                {
                    try { await CloseSnippetWithAnimationAsync(); } catch { }
                }
            }
            catch { }
        }

        private async void CardDeleteFlyout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(sender is FrameworkElement fe)) return;
                Guid id = Guid.Empty;
                try { if (fe.Tag is Guid gid) id = gid; else if (Guid.TryParse(fe.Tag?.ToString(), out var parsed)) id = parsed; } catch { }
                var snippet = ViewModel?.Snippets?.FirstOrDefault(s => s.Id == id);
                if (snippet == null || snippet.Id == Guid.Empty) return;
                
                // Delegate to ViewModel command
                var wasSelected = ViewModel?.SelectedSnippet == snippet;
                (DataContext as CodeViewModel)?.DeleteSnippetByIdCommand.Execute(snippet);
                
                if (wasSelected)
                {
                    try { await CloseSnippetWithAnimationAsync(); } catch { }
                }
            }
            catch { }
        }

        private void CardRestoreFlyout_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe)) return;
            var snippet = fe.DataContext as Pivot.CodeModule.Models.CodeFile;
            if (snippet == null) return;
            (DataContext as CodeViewModel)?.RestoreSnippetCommand.Execute(snippet);
        }

        // Copy snippet content to clipboard from card
        private void CardCopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe)) return;
            var snippet = fe.DataContext as Pivot.CodeModule.Models.CodeFile;
            if (snippet == null) return;
            (DataContext as CodeViewModel)?.CopyToClipboardCommand.Execute(snippet);
        }

        // Open the containing directory for a snippet (select the source file if found)
        private void CardOpenDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(sender is FrameworkElement fe)) return;
                var snippet = fe.DataContext as Pivot.CodeModule.Models.CodeFile;
                if (snippet == null)
                {
                    Guid id = Guid.Empty;
                    try { if (fe.Tag is Guid gid) id = gid; else if (Guid.TryParse(fe.Tag?.ToString(), out var parsed)) id = parsed; } catch { }
                    if (id != Guid.Empty)
                    {
                        snippet = ViewModel?.Snippets?.FirstOrDefault(s => s.Id == id);
                    }
                }
                if (snippet == null) return;
                
                // Delegate to ViewModel command
                (DataContext as CodeViewModel)?.OpenContainingDirectoryCommand.Execute(snippet);
            }
            catch { }
        }

        private static Guid CreateDeterministicGuid(string input)
        {
            try
            {
                using var md5 = System.Security.Cryptography.MD5.Create();
                var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
                if (bytes.Length >= 16)
                {
                    var guidBytes = new byte[16];
                    System.Array.Copy(bytes, guidBytes, 16);
                    return new Guid(guidBytes);
                }
            }
            catch { }
            return Guid.NewGuid();
        }

        // Copy content from main editor textbox
        private void EditorCopyButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var editor = this.FindName("CodeEditor") as TextBox;
                var content = editor?.Text ?? ViewModel?.SelectedSnippet?.Content ?? string.Empty;
                var dp = new DataPackage();
                dp.SetText(content);
                Clipboard.SetContent(dp);
            }
            catch { }
        }

        private void SnippetListView_DragItemsCompleted(object sender, DragItemsCompletedEventArgs e)
        {
            try
            {
                var root = this.Content as FrameworkElement;
                var list = root?.FindName("SnippetListView") as ListView;
                if (list != null)
                {
                    list.IsItemClickEnabled = true;
                    list.IsHitTestVisible = true;
                    list.SelectionMode = ListViewSelectionMode.Single;
                }
            }
            catch { }
        }

        // Find a TreeViewNode by matching its Content object (recursively)
        private TreeViewNode? FindTreeNodeByContent(IEnumerable<TreeViewNode> nodes, object content)
        {
            if (nodes == null || content == null) return null;
            foreach (var node in nodes)
            {
                try
                {
                    if (ReferenceEquals(node.Content, content) || (node.Content != null && node.Content.Equals(content))) return node;
                    var found = FindTreeNodeByContent(node.Children, content);
                    if (found != null) return found;
                }
                catch { }
            }
            return null;
        }

        private void RootGrid_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            try
            {
                AdjustScratchpadSize();
            }
            catch { }
        }

        private bool IsControlDown()
        {
            try
            {
                var core = Window.Current?.CoreWindow;
                if (core != null && core.GetKeyState(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) return true;
            }
            catch { }

            try
            {
                if (InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) return true;
            }
            catch { }

            return false;
        }

        private bool IsShiftDown()
        {
            try
            {
                var core = Window.Current?.CoreWindow;
                if (core != null && core.GetKeyState(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) return true;
            }
            catch { }

            try
            {
                if (InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) return true;
            }
            catch { }

            return false;
        }

        private bool IsAltDown()
        {
            try
            {
                var core = Window.Current?.CoreWindow;
                if (core != null && core.GetKeyState(Windows.System.VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) return true;
            }
            catch { }

            try
            {
                if (InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) return true;
            }
            catch { }

            return false;
        }

        private void ShowCardSavedOverlay()
        {
            try
            {
                var root = this.Content as FrameworkElement;
                var toast = root?.FindName("CardSavedOverlay") as FrameworkElement;
                if (toast == null) return;

                // show on UI thread
                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    try { toast.Visibility = Visibility.Visible; } catch { }
                });

                // hide after delay
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1500);
                        App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                        {
                            try { toast.Visibility = Visibility.Collapsed; } catch { }
                        });
                    }
                    catch { }
                });
            }
            catch { }
        }

        // Track when code editor has focus so tab behavior can be incident-specific
        private volatile bool _isCodeEditorTabMode = false;
        private volatile bool _isQuickAddTabMode = false;

        private void CodeEditor_GotFocus(object? sender, RoutedEventArgs e)
        {
            try
            {
                _isCodeEditorTabMode = true;
                // When focused, prefer inserting tabs rather than moving focus
                
            }
            catch { }
        }

        private void CodeEditor_LostFocus(object? sender, RoutedEventArgs e)
        {
            try
            {
                _isCodeEditorTabMode = false;
                
            }
            catch { }
        }

        private void CodeEditor_KeyDown(object? sender, KeyRoutedEventArgs e)
        {
            try
            {
                if (!_isCodeEditorTabMode) return;
                if (e.Key == Windows.System.VirtualKey.Tab)
                {
                    var tb = sender as TextBox;
                    if (tb != null)
                    {
                        var pos = tb.SelectionStart;
                        tb.Text = tb.Text.Insert(pos, "\t");
                        tb.SelectionStart = pos + 1;
                    }
                    e.Handled = true; // prevent focus navigation / component selection
                }
                // Ctrl+Enter: save and blur (move focus away)
                var ctrlDown = IsControlDown();
                if (e.Key == Windows.System.VirtualKey.Enter && ctrlDown)
                {
                    try
                    {
                        e.Handled = true;
                        // Save current snippet via ViewModel
                        var vm = ViewModel;
                        var snip = vm?.SelectedSnippet;
                        if (vm != null && snip != null)
                        {
                            _ = vm.SaveSnippetFileAsync(snip);
                            vm.IsDirty = false;
                        }

                        // Blur editor by moving focus to page root asynchronously
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
        
        private void QuickAddBox_GotFocus(object? sender, RoutedEventArgs e)
        {
            try
            {
                _isQuickAddTabMode = true;
                
            }
            catch { }
        }

        private async Task EditTagAsync(Guid filterId, string currentName)
        {
            try
            {
                var nameBox = new TextBox 
                { 
                    Header = "タグ名",
                    Text = currentName,
                    Width = 300
                };

                var panel = new StackPanel();
                panel.Children.Add(nameBox);

                var dialog = new ContentDialog
                {
                    Title = "タグを編集",
                    Content = panel,
                    PrimaryButtonText = "保存",
                    SecondaryButtonText = "キャンセル",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    var newName = nameBox.Text?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(newName))
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "エラー",
                            Content = "タグ名を入力してください。",
                            PrimaryButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                        return;
                    }

                    if (string.Equals(newName, currentName, StringComparison.OrdinalIgnoreCase))
                    {
                        return; // No change
                    }

                    var filterSettings = App.Current.Services.GetService(typeof(Pivot.Services.FilterSettingsService)) as Pivot.Services.FilterSettingsService;
                    if (filterSettings != null)
                    {
                        var oldName = await filterSettings.UpdateFilterNameAsync(filterId, newName);
                        if (!string.IsNullOrEmpty(oldName))
                        {
                            // Update all snippets that have this tag
                            
                            // Update all snippets via ViewModel
                            if (ViewModel != null)
                            {
                                await ViewModel.UpdateTagInAllSnippetsAsync(oldName, newName);
                            }



                            // Refresh the navigation menu
                            BuildNavigationMenu();
                            
                            // Ensure ListView is clickable after menu operations
                            var dispatcher2 = App.Current.MainWindow?.DispatcherQueue;
                            if (dispatcher2 != null)
                            {
                                dispatcher2.TryEnqueue(() =>
                                {
                                    try
                                    {
                                        var list = this.FindName("SnippetListView") as ListView;
                                        if (list != null)
                                        {
                                            list.IsHitTestVisible = true;
                                            list.IsItemClickEnabled = true;
                                        }
                                    }
                                    catch { }
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EditTagAsync: Error: {ex.Message}");
                // Ensure ListView is clickable even on error
                try
                {
                    var dispatcher = App.Current.MainWindow?.DispatcherQueue;
                    if (dispatcher != null)
                    {
                        dispatcher.TryEnqueue(() =>
                        {
                            try
                            {
                                var list = this.FindName("SnippetListView") as ListView;
                                if (list != null)
                                {
                                    list.IsHitTestVisible = true;
                                    list.IsItemClickEnabled = true;
                                }
                            }
                            catch { }
                        });
                    }
                }
                catch { }
            }
        }

        private async Task DeleteTagAsync(Guid filterId, string tagName)
        {
            try
            {
                var confirmDialog = new ContentDialog
                {
                    Title = "タグを削除",
                    Content = $"タグ「{tagName}」を削除しますか？\n\nこのタグはすべてのカードからも削除されます。",
                    PrimaryButtonText = "削除",
                    SecondaryButtonText = "キャンセル",
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    var filterSettings = App.Current.Services.GetService(typeof(Pivot.Services.FilterSettingsService)) as Pivot.Services.FilterSettingsService;
                    if (filterSettings != null)
                    {
                        var deletedName = await filterSettings.DeleteFilterAsync(filterId);
                        if (!string.IsNullOrEmpty(deletedName))
                        {
                            // Remove tag from all snippets
                            
                            // Remove tag from all snippets via ViewModel
                            if (ViewModel != null)
                            {
                                await ViewModel.RemoveTagFromAllSnippetsAsync(deletedName);
                                // The ViewModel handles saving and refreshing
                            }


                            // Refresh the navigation menu
                            BuildNavigationMenu();
                            
                            // Ensure ListView is clickable after menu operations
                            var dispatcher2 = App.Current.MainWindow?.DispatcherQueue;
                            if (dispatcher2 != null)
                            {
                                dispatcher2.TryEnqueue(() =>
                                {
                                    try
                                    {
                                        var list = this.FindName("SnippetListView") as ListView;
                                        if (list != null)
                                        {
                                            list.IsHitTestVisible = true;
                                            list.IsItemClickEnabled = true;
                                        }
                                    }
                                    catch { }
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteTagAsync: Error: {ex.Message}");
                // Ensure ListView is clickable even on error
                try
                {
                    // Ensure list view is enabled
                    SetListInteractionEnabled(true);
                }
                catch { }
            }
        }
    }
}
