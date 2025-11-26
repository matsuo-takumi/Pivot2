using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Microsoft.UI.Composition.SystemBackdrops;
using WinRT;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Pivot.CodeModule.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;
using Microsoft.UI;
using Windows.Foundation;
using Windows.System;
using System.Collections.Generic;
using Pivot.CodeModule.Services;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.CodeModule.Models;
using Pivot.Services;
using Pivot.Messages;
using Windows.ApplicationModel.DataTransfer;

// Note: editing is implemented with WinUI TextBox controls (replaced WebView2)

namespace Pivot.CodeModule.Views
{
    public sealed partial class CodePage : Page
    {
        public CodeViewModel? ViewModel => DataContext as CodeViewModel;
        private Pivot.CodeModule.Models.CodeFile? _previousSelectedSnippet;
        // Dragging state for movable scratchpad
        private bool _isScratchpadDragging = false;
        private Windows.Foundation.Point _scratchpadDragStart;
        private double _scratchpadStartX = 0;
        private double _scratchpadStartY = 0;

        public CodePage()
        {
            this.InitializeComponent();
            // Resolve via DI if available
            try
            {
                DataContext = App.Current.Services.GetRequiredService<CodeViewModel>();
            }
            catch (Exception)
            {
                // fallback: create a local viewmodel instance so UI still works in environments
                // where DI or EF Core registration failed.
                try
                {
                    DataContext = new CodeViewModel();
                }
                catch { }
            }

            // build left navigation (Snippets/Categories)
            try { BuildNavigationMenu(); } catch { }
            try
            {
                var messenger = App.Current.Services.GetService(typeof(IMessenger)) as IMessenger;
                messenger?.Register<CodePage, CodeFiltersUpdatedMessage>(this, (r, m) => r.OnCodeFiltersUpdated(m.Value));
            }
            catch { }
            // Restore previously selected snippet (persisted) if available
            try
            {
                var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                if (settings != null && ViewModel != null && repo != null)
                {
                    var lastId = settings.GetLastSelectedSnippetId();
                    if (lastId != Guid.Empty)
                    {
                        var all = repo.GetAll() ?? Enumerable.Empty<Pivot.CodeModule.Models.CodeFile>();
                        var found = all.FirstOrDefault(s => s.Id == lastId);
                        if (found != null)
                        {
                            ViewModel.SelectedSnippet = found;
                            _ = SendSelectedSnippetToEditorAsync();
                        }
                    }
                }
            }
            catch { }

            // subscribe to selection changes to push content to editor
            if (DataContext is INotifyPropertyChanged pc)
            {
                pc.PropertyChanged += OnViewModelPropertyChanged;
            }

            // wire editors (TextBox) change handlers
            try
            {
                var codeEditor = this.FindName("CodeEditor") as TextBox;
                var scratchEditor = this.FindName("ScratchpadEditor") as TextBox;
                if (codeEditor != null)
                {
                    codeEditor.TextChanged -= CodeEditor_TextChanged;
                    codeEditor.TextChanged += CodeEditor_TextChanged;
                }
                if (scratchEditor != null)
                {
                    scratchEditor.TextChanged -= ScratchpadEditor_TextChanged;
                    scratchEditor.TextChanged += ScratchpadEditor_TextChanged;
                }
            }
            catch { }

            // wire close button
            try
            {
                var closeBtn = this.FindName("ScratchpadCloseButton") as Button;
                if (closeBtn != null)
                {
                    closeBtn.Click -= ScratchpadCloseButton_Click;
                    closeBtn.Click += ScratchpadCloseButton_Click;
                }
            }
            catch { }

            // wire backdrop click to close scratchpad; avoid attaching overlay-level pointer handlers
            try
            {
                // ensure overlay pointer handler is attached only once
                var overlayRoot = this.FindName("ScratchpadOverlay") as UIElement;
                if (overlayRoot != null)
                {
                    // Always remove to prevent duplicate attachments, then add
                    overlayRoot.PointerPressed -= ScratchpadOverlay_PointerPressed;
                    overlayRoot.PointerPressed += ScratchpadOverlay_PointerPressed;
                    
                }
            }
            catch { }

            // Scratchpad drag disabled — keep editor fixed centered. (No pointer handlers attached.)

            // keep scratchpad sized to available area when page resizes
            try
            {
                var rootGrid = this.FindName("CodePageRoot") as FrameworkElement;
                if (rootGrid != null)
                {
                    rootGrid.SizeChanged -= RootGrid_SizeChanged;
                    rootGrid.SizeChanged += RootGrid_SizeChanged;
                }
            }
            catch { }

            // register navigation message to close scratchpad when leaving Code tab
            try
            {
                var messenger = App.Current.Services.GetService(typeof(IMessenger)) as IMessenger;
                if (messenger != null)
                {
                    _registeredMessenger = messenger;
                    messenger.Register<CodePage, Pivot.Messages.NavigationRequestMessage>(this, (r, m) =>
                    {
                        try
                        {
                            if (m.Value != Pivot.Models.NavigationRegion.Code)
                            {
                                // When navigating away from Code tab, ensure current snippet is saved.
                                try
                                {
                                    var vm = ViewModel;
                                    if (vm != null && vm.SelectedSnippet != null)
                                    {
                                        _ = vm.SaveSnippetFileAsync(vm.SelectedSnippet);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"CodePage: Error saving snippet on navigation: {ex.Message}");
                                }
                                // Do not clear SelectedSnippet automatically; preserve state.
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"CodePage: Error handling navigation message: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodePage: Error registering navigation message: {ex.Message}");
            }

            // NOTE: Escape closing removed per request — closing should only occur via
            // the Close button, tab navigation, or clicking outside the snippet.

            // ensure placeholder visibility reflects whether snippets exist
            try
            {
                if (ViewModel?.Snippets != null)
                {
                    var root = this.Content as FrameworkElement;
                    var placeholder = root?.FindName("PlaceholderBorder") as Border;
                    if (placeholder != null)
                    {
                        placeholder.Visibility = ViewModel.Snippets.Any() ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
                    }

                    // Store handler reference for cleanup
                    System.Collections.Specialized.NotifyCollectionChangedEventHandler collectionChangedHandler = (_, __) =>
                    {
                        try
                        {
                            var p2 = (this.Content as FrameworkElement)?.FindName("PlaceholderBorder") as Border;
                            if (p2 != null)
                            {
                                p2.Visibility = ViewModel.Snippets.Any() ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
                            }
                        }
                        catch { }
                    };
                    
                    ViewModel.Snippets.CollectionChanged += collectionChangedHandler;
                    // Store handler for cleanup in Unloaded event
                    _snippetsCollectionChangedHandler = collectionChangedHandler;
                }
            }
            catch { }

            // Register Unloaded event for cleanup
            this.Unloaded += CodePage_Unloaded;
        }

        private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _snippetsCollectionChangedHandler;
        private IMessenger? _registeredMessenger;

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
                var codeEditor = this.FindName("CodeEditor") as TextBox;
                var scratchEditor = this.FindName("ScratchpadEditor") as TextBox;
                if (codeEditor != null)
                {
                    codeEditor.TextChanged -= CodeEditor_TextChanged;
                }
                if (scratchEditor != null)
                {
                    scratchEditor.TextChanged -= ScratchpadEditor_TextChanged;
                }

                // Unregister size changed handler
                var rootGrid = this.FindName("CodePageRoot") as FrameworkElement;
                if (rootGrid != null)
                {
                    rootGrid.SizeChanged -= RootGrid_SizeChanged;
                }

                // Unregister overlay pointer handler
                var overlayRoot = this.FindName("ScratchpadOverlay") as UIElement;
                if (overlayRoot != null)
                {
                    overlayRoot.PointerPressed -= ScratchpadOverlay_PointerPressed;
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
            var newSnippet = new Models.CodeFile { Title = "New Snippet", Language = "Python" };
            try
            {
                // Persist immediately via repository if available
                var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                if (repo != null)
                {
                    repo.Save(newSnippet);
                }
            }
            catch { }

            if (ViewModel != null)
            {
                if (ViewModel.Snippets != null)
                {
                    var insertIndex = ViewModel.Snippets.Count > 0 && ViewModel.Snippets[0].Id == Guid.Empty ? 1 : 0;
                    ViewModel.Snippets.Insert(insertIndex, newSnippet);
                }
                else
                {
                    ViewModel.Snippets = new System.Collections.ObjectModel.ObservableCollection<Models.CodeFile> { newSnippet };
                }
                
                // Ensure ListView is clickable after adding new item
                try
                {
                    var list = this.FindName("SnippetListView") as ListView;
                    if (list != null)
                    {
                        // Force layout update to ensure new item is rendered
                        list.UpdateLayout();
                        list.ScrollIntoView(newSnippet);
                        
                        // Ensure clickability is enabled
                        list.IsItemClickEnabled = true;
                        list.IsHitTestVisible = true;
                        list.SelectionMode = ListViewSelectionMode.Single;
                    }
                }
                catch { }
                
                ViewModel.SelectedSnippet = newSnippet;
            }
        }

        private void TagFilterBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            try
            {
                ViewModel?.FilterTags(sender.Text);
            }
            catch { }
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
                    var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                    if (settings != null)
                    {
                        var id = ViewModel?.SelectedSnippet?.Id ?? Guid.Empty;
                        _ = settings.SetLastSelectedSnippetIdAsync(id);
                    }
                }
                catch { }
            }
        }

        private void BuildNavigationMenu()
        {
            try
            {
                var nav = this.FindName("CodeNav") as NavigationView;
                if (nav == null) return;
                nav.MenuItems.Clear();

                // Keep a single "All" menu item for compact/navigation behavior (for NavigationView menu)
                var allItem = new NavigationViewItem { Content = "All Snippets", Tag = "all", Icon = new SymbolIcon(Symbol.AllApps) };
                nav.MenuItems.Add(allItem);

                var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                // Use Preferences > Code のタグ（CodeFilters）を左ナビに追加して、All Snippets のような挙動にする
                var filters = settings?.GetCodeFilters() ?? new List<Pivot.Models.CustomFilter>();

                // Add tags to NavigationView menu items with right-click context menu
                try
                {
                    foreach (var f in filters.OrderBy(f => f.SortOrder).ThenBy(f => f.Name))
                    {
                        try
                        {
                            var ni = new NavigationViewItem { Content = f.Name, Tag = f.Id, Icon = new SymbolIcon(Symbol.Tag) };
                            
                            // Add right-click context menu
                            var menuFlyout = new MenuFlyout();
                            
                            // Ensure ListView remains clickable when menu is shown/closed
                            menuFlyout.Opening += (s, e) =>
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
                            };
                            
                            menuFlyout.Closed += (s, e) =>
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
                            };
                            
                            var editMenuItem = new MenuFlyoutItem { Text = "編集" };
                            editMenuItem.Click += async (s, e) => 
                            {
                                try
                                {
                                    await EditTagAsync(f.Id, f.Name);
                                    // Ensure ListView is clickable after edit
                                    var list = this.FindName("SnippetListView") as ListView;
                                    if (list != null)
                                    {
                                        list.IsHitTestVisible = true;
                                        list.IsItemClickEnabled = true;
                                    }
                                }
                                catch { }
                            };
                            menuFlyout.Items.Add(editMenuItem);
                            
                            var deleteMenuItem = new MenuFlyoutItem { Text = "削除" };
                            deleteMenuItem.Click += async (s, e) => 
                            {
                                try
                                {
                                    await DeleteTagAsync(f.Id, f.Name);
                                    // Ensure ListView is clickable after delete
                                    var list = this.FindName("SnippetListView") as ListView;
                                    if (list != null)
                                    {
                                        list.IsHitTestVisible = true;
                                        list.IsItemClickEnabled = true;
                                    }
                                }
                                catch { }
                            };
                            menuFlyout.Items.Add(deleteMenuItem);
                            
                            ni.ContextFlyout = menuFlyout;
                            
                            nav.MenuItems.Add(ni);
                        }
                        catch { }
                    }
                }
                catch { }

                nav.SelectedItem = allItem;
                try { ViewModel?.Refresh(); } catch { }
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
                try { ViewModel.GetType().GetMethod("ShowDeletedSnippets")?.Invoke(ViewModel, null); } catch { }
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
                if (string.Equals(tagStr, "all", StringComparison.OrdinalIgnoreCase))
                {
                    ViewModel.ActiveFilters.Clear();
                    ViewModel.FilterSnippets();
                    // Ensure ListView is clickable after filtering
                    var list = this.FindName("SnippetListView") as ListView;
                    if (list != null)
                    {
                        list.IsHitTestVisible = true;
                        list.IsItemClickEnabled = true;
                    }
                    if (ViewModel?.SelectedSnippet != null) _ = CloseSnippetWithAnimationAsync(skipRefreshAfterSave: true);
                    return;
                }

                if (string.Equals(tagStr, "trash", StringComparison.OrdinalIgnoreCase))
                {
                    try { ViewModel?.GetType().GetMethod("ShowDeletedSnippets")?.Invoke(ViewModel, null); } catch { }
                    return;
                }

                if (Guid.TryParse(tagStr, out var filterId))
                {
                    ViewModel.ActiveFilters.Clear();
                    ViewModel.ActiveFilters.Add(filterId);
                    ViewModel.FilterSnippets();
                    // Ensure ListView is clickable after filtering
                    var list = this.FindName("SnippetListView") as ListView;
                    if (list != null)
                    {
                        list.IsHitTestVisible = true;
                        list.IsItemClickEnabled = true;
                    }
                    if (ViewModel?.SelectedSnippet != null) _ = CloseSnippetWithAnimationAsync(skipRefreshAfterSave: true);
                }
                else
                {
                    // If the clicked item is a category parent (no Tag), try to find the category and apply all its filters
                    try
                    {
                        var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                        var catName = item.Content?.ToString() ?? string.Empty;
                            if (string.Equals(catName, "ごみ箱", StringComparison.OrdinalIgnoreCase))
                            {
                                // Show deleted snippets (Trash)
                                try { ViewModel?.GetType().GetMethod("ShowDeletedSnippets")?.Invoke(ViewModel, null); } catch { }
                                return;
                            }
                        if (settings != null && !string.IsNullOrWhiteSpace(catName))
                        {
                            var cats = settings.GetCodeCategories() ?? new List<Pivot.Models.CodeCategory>();
                            var cat = cats.FirstOrDefault(c => string.Equals(c.Name, catName, StringComparison.OrdinalIgnoreCase));
                            if (cat != null)
                            {
                                ViewModel.ActiveFilters.Clear();
                                foreach (var fid in cat.FilterIds) ViewModel.ActiveFilters.Add(fid);
                                ViewModel.FilterSnippets();
                                if (ViewModel?.SelectedSnippet != null) _ = CloseSnippetWithAnimationAsync(skipRefreshAfterSave: true);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }


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
                            source = FindDescendantByName(container, "SnippetCardBorder") as UIElement;
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

        // Open the selected snippet in a new OS-level window. Save on close and clear selection.
        private Task ShowSnippetWindowAsync()
        {
            try
            {
                var vm = ViewModel;
                if (vm?.SelectedSnippet == null) return Task.CompletedTask;

                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        var snippet = vm.SelectedSnippet;

                        // Build window content with reasonable initial size and matching scratchpad width
                        var titleText = new TextBlock { Text = snippet?.Title ?? "Snippet", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0,0,0,8) };

                        // Panel width (controls overall window size) - reduced default
                        double panelWidth = 560;
                        double panelHeight = 420;

                        var contentBox = new TextBox
                        {
                            AcceptsReturn = true,
                            TextWrapping = TextWrapping.Wrap,
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 12,
                            Text = snippet?.Content ?? string.Empty,
                            Width = panelWidth - 40,
                            Height = panelHeight - 140
                        };

                        // Insert tab characters into the TextBox instead of moving focus
                        try
                        {
                            contentBox.KeyDown += (s, e) =>
                            {
                                try
                                {
                                    if (e.Key == Windows.System.VirtualKey.Tab)
                                    {
                                        var tb = s as TextBox;
                                        if (tb != null)
                                        {
                                            var pos = tb.SelectionStart;
                                            tb.Text = tb.Text.Insert(pos, "\t");
                                            tb.SelectionStart = pos + 1;
                                        }
                                        e.Handled = true;
                                    }
                                }
                                catch { }
                            };
                        }
                        catch { }

                        var panel = new StackPanel { Spacing = 8, Padding = new Thickness(12), Width = panelWidth };
                        panel.Children.Add(titleText);
                        panel.Children.Add(contentBox);

                        // Apply theme/style from the current page to the panel so the new window respects theme
                        try { panel.RequestedTheme = this.RequestedTheme; } catch { }

                        // (AppWindow titlebar styling will be applied after creating the Window)

                        var wnd = new Window();
                        wnd.Content = panel;

                        // Try to get AppWindow and set explicit size to avoid huge default sizing
                        try
                        {
                            var hwnd = WindowNative.GetWindowHandle(wnd);
                            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                            var appWindow = AppWindow.GetFromWindowId(windowId);
                            if (appWindow != null)
                            {
                                try { appWindow.Resize(new SizeInt32 { Width = (int)panelWidth + 24, Height = (int)panelHeight + 48 }); } catch { }

                                // Try to apply Mica backdrop to the new AppWindow (best-effort)
                                try
                                {
                                    // Ensure dispatcher queue for composition backdrops
                                    try { DispatcherQueue.EnsureSystemDispatcherQueue(); } catch { }
                                    var config = new SystemBackdropConfiguration();
                                    config.IsInputActive = true;
                                    // Set initial theme mapping
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
                                    // Apply to the window's composition target
                                    try { mica.AddSystemBackdropTarget(wnd.As<ICompositionSupportsSystemBackdrop>()); } catch { }
                                    mica.SetSystemBackdropConfiguration(config);

                                    // Update config when theme changes
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

                                    // Dispose mica and detach handlers when window closes
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

                        // Helper to restore list interaction and clear selection
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

                        // Close the snippet window when it loses activation (focus out) or when main app window closes.
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
                                        // Save snippet contents when the snippet window loses activation,
                                        // but do NOT close the window or deselect the snippet. Closing
                                        // should only be performed via the Close button, tab navigation,
                                        // or clicking outside the snippet.
                                        if (snippet != null)
                                        {
                                            snippet.Content = contentBox.Text ?? string.Empty;
                                            if (vm != null) _ = vm.SaveSnippetFileAsync(snippet);
                                        }
                                    }
                                    catch { }

                                    // Restore list interaction but do not dismiss the snippet window.
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

                        // Ensure closing via window chrome saves, clears and restores list
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
                                    var target = FindDescendantByName(container, "SnippetCardBorder") as UIElement;
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
                if (e.Key == Windows.System.VirtualKey.Enter && !isShiftDown)
                {
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
            catch { }
        }

        private void CommitQuickAdd()
        {
            try
            {
                var box = this.FindName("QuickAddBox") as TextBox;
                if (box == null) return;
                var text = (box.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text)) return;

                // Derive title from first non-empty line
                var lines = text.Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.None);
                var title = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "Snippet";
                title = title.Length > 120 ? title.Substring(0, 120) : title;

                var newSnippet = new Pivot.CodeModule.Models.CodeFile
                {
                    Title = title,
                    Content = text,
                    Updated = DateTime.Now
                };

                // inherit current left-pane filter as tag (if a specific filter is selected)
                try
                {
                    var nav = this.FindName("CodeNav") as NavigationView;
                    var selected = nav?.SelectedItem as NavigationViewItem;
                    var tagStr = selected?.Tag?.ToString() ?? string.Empty;
                    if (Guid.TryParse(tagStr, out var fid))
                    {
                        var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                        var tagName = repo?.GetFilterNameById(fid) ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(tagName)) newSnippet.Tags = tagName;
                    }
                }
                catch { }

                // Persist via repository
                try
                {
                    var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                    repo?.Save(newSnippet);
                }
                catch { }

                // Reflect in UI
                try
                {
                    if (ViewModel != null)
                    {
                        if (ViewModel.Snippets != null)
                        {
                            var insertIndex = ViewModel.Snippets.Count > 0 && ViewModel.Snippets[0].Id == Guid.Empty ? 1 : 0;
                            ViewModel.Snippets.Insert(insertIndex, newSnippet);
                            
                            // Ensure ListView is clickable after adding new item
                            try
                            {
                                var list = this.FindName("SnippetListView") as ListView;
                                if (list != null)
                                {
                                    // Force layout update to ensure new item is rendered
                                    list.UpdateLayout();
                                    list.ScrollIntoView(newSnippet);
                                    
                                    // Ensure clickability is enabled
                                    list.IsItemClickEnabled = true;
                                    list.IsHitTestVisible = true;
                                    list.SelectionMode = ListViewSelectionMode.Single;
                                    
                                    // Small delay to ensure layout is complete
                                    _ = Task.Run(async () =>
                                    {
                                        await Task.Delay(100);
                                        App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                                        {
                                            try
                                            {
                                                if (list != null)
                                                {
                                                    list.IsItemClickEnabled = true;
                                                    list.IsHitTestVisible = true;
                                                }
                                            }
                                            catch { }
                                        });
                                    });
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            ViewModel.Refresh();
                        }
                    }
                }
                catch { }

                // Clear input
                box.Text = string.Empty;

                // show a brief saved toast (InfoBar)
                try
                {
                    // show small saved overlay at CardPanel bottom-right
                    ShowCardSavedOverlay();
                }
                catch { }
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
                        var fromListOrCard = IsAncestorNamed(orig, "SnippetListView") || IsAncestorNamed(orig, "SnippetCardBorder");
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
            // TODO: implement scratchpad delete handling
        }

        private void ScratchpadCopyMenu_Click(object? sender, RoutedEventArgs e)
        {
            // TODO: implement duplicate creation
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

        // Backdrop handlers removed — root-level handlers handle outside clicks now

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
                vm.Snippets = new ObservableCollection<Pivot.CodeModule.Models.CodeFile>(snapshot);
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

        private bool IsAncestorNamed(DependencyObject start, string name)
        {
            try
            {
                var current = start;
                while (current != null)
                {
                    if (current is FrameworkElement fe && fe.Name == name) return true;
                    current = VisualTreeHelper.GetParent(current);
                }
            }
            catch { }
            return false;
        }

        private T? FindDescendantOfType<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return default;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var found = FindDescendantOfType<T>(child);
                if (found != null) return found;
            }
            return default;
        }

    private Pivot.CodeModule.Models.CodeFile? FindContainingSnippet(DependencyObject start)
    {
        var current = start;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is Pivot.CodeModule.Models.CodeFile cf) return cf;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
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
                var tb = sender as TextBox;
                if (tb == null) return;
                if (ViewModel != null && ViewModel.SelectedSnippet != null)
                {
                    var selectedSnippet = ViewModel.SelectedSnippet;
                    var newContent = tb.Text ?? string.Empty;
                    
                    // SelectedSnippetを更新
                    selectedSnippet.Content = newContent;
                    ViewModel.IsDirty = true;
                    
                    // Snippetsコレクション内の対応するインスタンスも更新（リアルタイム更新のため）
                    var snippetInCollection = ViewModel.Snippets.FirstOrDefault(s => s.Id == selectedSnippet.Id);
                    if (snippetInCollection != null && !ReferenceEquals(snippetInCollection, selectedSnippet))
                    {
                        snippetInCollection.Content = newContent;
                    }
                }
            }
            catch { }
        }

        private void ScratchpadEditor_TextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
                var tb = sender as TextBox;
                if (tb == null) return;
                if (ViewModel != null && ViewModel.SelectedSnippet != null)
                {
                    var selectedSnippet = ViewModel.SelectedSnippet;
                    var newContent = tb.Text ?? string.Empty;
                    
                    // SelectedSnippetを更新
                    selectedSnippet.Content = newContent;
                    ViewModel.IsDirty = true;
                    
                    // Snippetsコレクション内の対応するインスタンスも更新（リアルタイム更新のため）
                    var snippetInCollection = ViewModel.Snippets.FirstOrDefault(s => s.Id == selectedSnippet.Id);
                    if (snippetInCollection != null && !ReferenceEquals(snippetInCollection, selectedSnippet))
                    {
                        snippetInCollection.Content = newContent;
                    }
                }
            }
            catch { }
        }

        private void ScratchpadTitleBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
                var tb = sender as TextBox;
                if (tb == null) return;
                if (ViewModel != null && ViewModel.SelectedSnippet != null)
                {
                    var selectedSnippet = ViewModel.SelectedSnippet;
                    var newTitle = tb.Text ?? string.Empty;
                    
                    // SelectedSnippetを更新
                    selectedSnippet.Title = newTitle;
                    ViewModel.IsDirty = true;
                    
                    // Snippetsコレクション内の対応するインスタンスも更新（リアルタイム更新のため）
                    var snippetInCollection = ViewModel.Snippets.FirstOrDefault(s => s.Id == selectedSnippet.Id);
                    if (snippetInCollection != null && !ReferenceEquals(snippetInCollection, selectedSnippet))
                    {
                        snippetInCollection.Title = newTitle;
                    }
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

        // Scratchpad delete button removed from UI; deletion via card delete remains.

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
                    // try to resolve from ViewModel
                    snippet = ViewModel?.Snippets?.FirstOrDefault(s => s.Id == id);
                }
                if (snippet == null || snippet.Id == Guid.Empty) return; // don't delete placeholder
                // No confirmation dialog: move snippet to Trash (soft-delete)
                var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                try { repo?.Delete(snippet.Id); } catch { }
                try { ViewModel?.Snippets?.Remove(snippet); } catch { }
                if (ViewModel != null && ViewModel.SelectedSnippet == snippet)
                {
                    ViewModel.SelectedSnippet = null;
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
                // No confirmation dialog for flyout delete: soft-delete immediately
                var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                try { repo?.Delete(snippet.Id); } catch { }
                try { ViewModel?.Snippets?.Remove(snippet); } catch { }
                if (ViewModel != null && ViewModel.SelectedSnippet == snippet)
                {
                    ViewModel.SelectedSnippet = null;
                    try { await CloseSnippetWithAnimationAsync(); } catch { }
                }
            }
            catch { }
        }

        private void CardRestoreFlyout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(sender is FrameworkElement fe)) return;
                Guid id = Guid.Empty;
                try { if (fe.Tag is Guid gid) id = gid; else if (Guid.TryParse(fe.Tag?.ToString(), out var parsed)) id = parsed; } catch { }
                var snippet = ViewModel?.Snippets?.FirstOrDefault(s => s.Id == id);
                // If not found in current snippets, try repo
                if (snippet == null && id != Guid.Empty)
                {
                    // Attempt to locate in deleted set via repo (use reflection to avoid compile-time coupling)
                    var repoObj = App.Current.Services.GetService(typeof(ICodeRepository));
                    try
                    {
                        var getAllDeleted = repoObj?.GetType().GetMethod("GetAllDeleted");
                        if (getAllDeleted != null)
                        {
                            var deletedEnum = getAllDeleted.Invoke(repoObj, null) as System.Collections.IEnumerable;
                            if (deletedEnum != null)
                            {
                                foreach (var o in deletedEnum)
                                {
                                    if (o is Pivot.CodeModule.Models.CodeFile cf && cf.Id == id) { snippet = cf; break; }
                                }
                            }
                        }
                    }
                    catch { }
                }
                if (snippet == null) return;

                try
                {
                    var repoObj2 = App.Current.Services.GetService(typeof(ICodeRepository));
                    var restoreMethod = repoObj2?.GetType().GetMethod("Restore");
                    restoreMethod?.Invoke(repoObj2, new object[] { snippet.Id });
                }
                catch { }
                // Refresh UI lists
                try { ViewModel?.Refresh(); } catch { }
                try { if (ViewModel?.SelectedSnippet == snippet) ViewModel.SelectedSnippet = null; } catch { }
            }
            catch { }
        }

        // Copy snippet content to clipboard from card
        private void CardCopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(sender is FrameworkElement fe)) return;
                var snippet = fe.DataContext as Pivot.CodeModule.Models.CodeFile;
                if (snippet == null) return;
                var dp = new DataPackage();
                dp.SetText(snippet.Content ?? string.Empty);
                Clipboard.SetContent(dp);
            }
            catch { }
        }

        // Open the containing directory for a snippet (select the source file if found)
        private void CardOpenDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("CardOpenDirectoryButton_Click invoked");
                if (!(sender is FrameworkElement fe)) return;
                Guid id = Guid.Empty;
                try { if (fe.Tag is Guid gid) id = gid; else if (Guid.TryParse(fe.Tag?.ToString(), out var parsed)) id = parsed; } catch { }
                var snippet = fe.DataContext as Pivot.CodeModule.Models.CodeFile ?? ViewModel?.Snippets?.FirstOrDefault(s => s.Id == id);
                if (snippet == null)
                {
                    System.Diagnostics.Debug.WriteLine("CardOpenDirectoryButton_Click: snippet null");
                    return;
                }

                var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                var userSettings = settings?.GetUserSettings();
                var dirs = new System.Collections.Generic.List<string>(userSettings?.CodeDirectories ?? new System.Collections.Generic.List<string>());
                var allowedExts = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { ".json", ".cs", ".py", ".md", ".txt" };

                // include export directory as a fallback
                try
                {
                    var exportDir = settings?.GetExportOutputDirectory();
                    if (!string.IsNullOrWhiteSpace(exportDir) && !dirs.Contains(exportDir) && System.IO.Directory.Exists(exportDir))
                    {
                        dirs.Add(exportDir);
                    }
                }
                catch { }

                foreach (var dir in dirs)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(dir) || !System.IO.Directory.Exists(dir)) continue;
                        var files = System.IO.Directory.EnumerateFiles(dir, "*.*", System.IO.SearchOption.TopDirectoryOnly)
                                             .Where(f => allowedExts.Contains(System.IO.Path.GetExtension(f) ?? string.Empty));
                        foreach (var file in files)
                        {
                            try
                            {
                                if (CreateDeterministicGuid(file) == snippet.Id)
                                {
                                    System.Diagnostics.Debug.WriteLine($"CardOpenDirectoryButton_Click: found file {file}");
                                    var processInfo = new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = "explorer.exe",
                                        Arguments = $"/select,\"{file}\"",
                                        UseShellExecute = true
                                    };
                                    System.Diagnostics.Process.Start(processInfo);
                                    return;
                                }
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"CardOpenDirectoryButton_Click: file check error {ex.Message}"); }
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"CardOpenDirectoryButton_Click: dir scan error {ex.Message}"); }
                }

                // If file not found, try direct export filename match
                try
                {
                    var exportDir = settings?.GetExportOutputDirectory();
                    if (!string.IsNullOrWhiteSpace(exportDir) && System.IO.Directory.Exists(exportDir))
                    {
                        var jsonPath = System.IO.Path.Combine(exportDir, snippet.Id.ToString() + ".json");
                        var mdPath = System.IO.Path.Combine(exportDir, snippet.Id.ToString() + ".md");
                        if (System.IO.File.Exists(jsonPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"CardOpenDirectoryButton_Click: found exported json {jsonPath}");
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{jsonPath}\"", UseShellExecute = true });
                            return;
                        }
                        if (System.IO.File.Exists(mdPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"CardOpenDirectoryButton_Click: found exported md {mdPath}");
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{mdPath}\"", UseShellExecute = true });
                            return;
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"CardOpenDirectoryButton_Click: export check error {ex.Message}"); }

                // Fallback: open first configured code directory if available
                try
                {
                    var firstDir = userSettings?.CodeDirectories?.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(firstDir) && System.IO.Directory.Exists(firstDir))
                    {
                        System.Diagnostics.Debug.WriteLine($"CardOpenDirectoryButton_Click: opening directory {firstDir}");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{firstDir}\"", UseShellExecute = true });
                        return;
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"CardOpenDirectoryButton_Click: fallback open error {ex.Message}"); }

                System.Diagnostics.Debug.WriteLine("CardOpenDirectoryButton_Click: no file or directory found to open");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"CardOpenDirectoryButton_Click: unexpected error {ex.Message}"); }
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

        private FrameworkElement? FindDescendantByName(DependencyObject parent, string name)
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe)
                {
                    if (fe.Name == name) return fe;
                }
                var found = FindDescendantByName(child, name);
                if (found != null) return found;
            }
            return null;
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

                    var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                    if (settings != null)
                    {
                        var oldName = await settings.UpdateFilterNameAsync(filterId, newName);
                        if (!string.IsNullOrEmpty(oldName))
                        {
                            // Update all snippets that have this tag
                            var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                            if (repo != null)
                            {
                                repo.UpdateTagInAllSnippets(oldName, newName);
                                
                                // Update ViewModel's snippets in-place to avoid flickering
                                if (ViewModel != null)
                                {
                                    // Update snippets in _allSnippets and Snippets collection
                                    var allSnippets = repo.GetAll().ToList();
                                    
                                    // Update _allSnippets (using reflection to access private field)
                                    try
                                    {
                                        var allSnippetsField = ViewModel.GetType().GetField("_allSnippets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                        if (allSnippetsField != null)
                                        {
                                            allSnippetsField.SetValue(ViewModel, allSnippets);
                                        }
                                    }
                                    catch { }
                                    
                                    // Update visible snippets in-place without clearing the collection
                                    var dispatcher = App.Current.MainWindow?.DispatcherQueue;
                                    if (dispatcher != null)
                                    {
                                        dispatcher.TryEnqueue(() =>
                                        {
                                            try
                                            {
                                                // Update existing snippets in-place by updating their properties
                                                if (ViewModel.Snippets != null)
                                                {
                                                    foreach (var snippet in ViewModel.Snippets.ToList())
                                                    {
                                                        var updated = allSnippets.FirstOrDefault(s => s.Id == snippet.Id);
                                                        if (updated != null && updated.Tags != snippet.Tags)
                                                        {
                                                            // Update the snippet's tags property directly to avoid UI flickering
                                                            snippet.Tags = updated.Tags;
                                                            snippet.Updated = updated.Updated;
                                                        }
                                                    }
                                                }
                                                
                                                // Also update _allSnippets items
                                                try
                                                {
                                                    var allSnippetsField = ViewModel.GetType().GetField("_allSnippets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                                    if (allSnippetsField != null && allSnippetsField.GetValue(ViewModel) is List<Pivot.CodeModule.Models.CodeFile> allSnippetsList)
                                                    {
                                                        foreach (var snippet in allSnippetsList)
                                                        {
                                                            var updated = allSnippets.FirstOrDefault(s => s.Id == snippet.Id);
                                                            if (updated != null && updated.Tags != snippet.Tags)
                                                            {
                                                                snippet.Tags = updated.Tags;
                                                                snippet.Updated = updated.Updated;
                                                            }
                                                        }
                                                    }
                                                }
                                                catch { }
                                                
                                                // Ensure ListView is clickable
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
                    var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                    if (settings != null)
                    {
                        var deletedName = await settings.DeleteFilterAsync(filterId);
                        if (!string.IsNullOrEmpty(deletedName))
                        {
                            // Remove tag from all snippets
                            var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                            if (repo != null)
                            {
                                repo.RemoveTagFromAllSnippets(deletedName);
                                
                                // Update ViewModel's snippets in-place to avoid flickering
                                if (ViewModel != null)
                                {
                                    // Update snippets in _allSnippets and Snippets collection
                                    var allSnippets = repo.GetAll().ToList();
                                    
                                    // Update _allSnippets (using reflection to access private field)
                                    try
                                    {
                                        var allSnippetsField = ViewModel.GetType().GetField("_allSnippets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                        if (allSnippetsField != null)
                                        {
                                            allSnippetsField.SetValue(ViewModel, allSnippets);
                                        }
                                    }
                                    catch { }
                                    
                                    // Update visible snippets in-place without clearing the collection
                                    var dispatcher = App.Current.MainWindow?.DispatcherQueue;
                                    if (dispatcher != null)
                                    {
                                        dispatcher.TryEnqueue(() =>
                                        {
                                            try
                                            {
                                                // Update existing snippets in-place by updating their properties
                                                if (ViewModel.Snippets != null)
                                                {
                                                    foreach (var snippet in ViewModel.Snippets.ToList())
                                                    {
                                                        var updated = allSnippets.FirstOrDefault(s => s.Id == snippet.Id);
                                                        if (updated != null && updated.Tags != snippet.Tags)
                                                        {
                                                            // Update the snippet's tags property directly to avoid UI flickering
                                                            snippet.Tags = updated.Tags;
                                                            snippet.Updated = updated.Updated;
                                                        }
                                                    }
                                                }
                                                
                                                // Also update _allSnippets items
                                                try
                                                {
                                                    var allSnippetsField = ViewModel.GetType().GetField("_allSnippets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                                    if (allSnippetsField != null && allSnippetsField.GetValue(ViewModel) is List<Pivot.CodeModule.Models.CodeFile> allSnippetsList)
                                                    {
                                                        foreach (var snippet in allSnippetsList)
                                                        {
                                                            var updated = allSnippets.FirstOrDefault(s => s.Id == snippet.Id);
                                                            if (updated != null && updated.Tags != snippet.Tags)
                                                            {
                                                                snippet.Tags = updated.Tags;
                                                                snippet.Updated = updated.Updated;
                                                            }
                                                        }
                                                    }
                                                }
                                                catch { }
                                                
                                                // Ensure ListView is clickable
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

    }
}


