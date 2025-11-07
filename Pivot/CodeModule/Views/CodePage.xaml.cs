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
using Windows.Foundation;
using System.Collections.Generic;
using Pivot.CodeModule.Services;
using Microsoft.UI.Xaml.Controls.Primitives;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.CodeModule.Models;
using Pivot.Services;

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

            // subscribe to selection changes to push content to editor
            if (DataContext is INotifyPropertyChanged pc)
            {
                pc.PropertyChanged += OnViewModelPropertyChanged;
            }

            // wire scratchpad buttons
            try
            {
                // Close button handler is wired later to a named handler to ensure removal/consistency
                var tmpCloseBtn = this.FindName("ScratchpadCloseButton") as Button;
                // no-op here
                var addTagBtn = this.FindName("ScratchpadAddTagButton") as Button;
                var newTagBox = this.FindName("ScratchpadNewTagBox") as TextBox;
                if (addTagBtn != null && newTagBox != null)
                {
                    addTagBtn.Click += (_, __) =>
                    {
                        try
                        {
                            var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                            var name = newTagBox.Text?.Trim() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(name) && repo != null)
                            {
                                repo.AddTag(name);
                                newTagBox.Text = string.Empty;
                                RefreshScratchpadTags();

                                // Also add to CodeFilters and create a CodeCategory for this tag so it appears under Preferences > Code > Categories
                                try
                                {
                                    var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                                    if (settings != null)
                                    {
                                        var filters = settings.GetCodeFilters() ?? new List<Pivot.Models.CustomFilter>();
                                        if (!filters.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            var nf = new Pivot.Models.CustomFilter { Name = name };
                                            filters.Add(nf);
                                            _ = settings.SetCodeFiltersAsync(filters);

                                            var cats = settings.GetCodeCategories() ?? new List<Pivot.Models.CodeCategory>();
                                            if (!cats.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                                            {
                                                cats.Add(new Pivot.Models.CodeCategory { Name = name, FilterIds = new List<Guid> { nf.Id } });
                                                _ = settings.SetCodeCategoriesAsync(cats);
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                    };
                }

                // Also wire the editor-level add tag controls (moved into main section)
                try
                {
                    var editorAddBtn = this.FindName("EditorAddTagButton") as Button;
                    var editorNewTagBox = this.FindName("EditorNewTagBox") as TextBox;
                    if (editorAddBtn != null && editorNewTagBox != null)
                    {
                        editorAddBtn.Click += (_, __) =>
                        {
                            try
                            {
                                var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                                var name = editorNewTagBox.Text?.Trim() ?? string.Empty;
                                if (!string.IsNullOrWhiteSpace(name) && repo != null)
                                {
                                    repo.AddTag(name);
                                    editorNewTagBox.Text = string.Empty;
                                    RefreshScratchpadTags();

                                    // Also add to CodeFilters and create a CodeCategory
                                    try
                                    {
                                        var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                                        if (settings != null)
                                        {
                                            var filters = settings.GetCodeFilters() ?? new List<Pivot.Models.CustomFilter>();
                                            if (!filters.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
                                            {
                                                var nf = new Pivot.Models.CustomFilter { Name = name };
                                                filters.Add(nf);
                                                _ = settings.SetCodeFiltersAsync(filters);

                                                var cats = settings.GetCodeCategories() ?? new List<Pivot.Models.CodeCategory>();
                                                if (!cats.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                                                {
                                                    cats.Add(new Pivot.Models.CodeCategory { Name = name, FilterIds = new List<Guid> { nf.Id } });
                                                    _ = settings.SetCodeCategoriesAsync(cats);
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        };
                    }
                }
                catch { }
            }
            catch { }

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
                    Debug.WriteLine("Constructor: ensured ScratchpadOverlay.PointerPressed is attached");
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
                    messenger.Register<CodePage, Pivot.Messages.NavigationRequestMessage>(this, (r, m) =>
                    {
                        try
                        {
                            if (m.Value != Pivot.Models.NavigationRegion.Code)
                            {
                                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => { if (ViewModel != null) ViewModel.SelectedSnippet = null; });
                            }
                        }
                        catch { }
                    });
                }
            }
            catch { }

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

                    ViewModel.Snippets.CollectionChanged += (_, __) =>
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
                }
            }
            catch { }
        }

        // Text-based editors (TextBox) are used instead of WebView2. Text change events update the ViewModel.

        private void OnSearchClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel?.Refresh();
        }

        private void OnAddClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var newSnippet = new Models.CodeFile { Title = "New Snippet", Language = "Python" };
            ViewModel?.Snippets.Add(newSnippet);
            if (ViewModel != null)
            {
                ViewModel.SelectedSnippet = newSnippet;
            }
        }

        private volatile bool _isAnimationActive = false;
        private ConnectedAnimation? _pendingOpenAnimation;
        private ConnectedAnimation? _pendingCloseAnimation;

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CodeViewModel.SelectedSnippet))
            {
                Debug.WriteLine($"OnViewModelPropertyChanged: SelectedSnippet changed -> {(ViewModel?.SelectedSnippet==null?"null":"set")}");
                // auto-save previous snippet (Google Keep style)
                try
                {
                    var prev = _previousSelectedSnippet;
                    if (prev != null && ViewModel != null)
                    {
                        // if content changed, persist
                        _ = ViewModel.SaveSnippetFileAsync(prev);
                    }
                }
                catch { }

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
            }
        }

        private void BuildNavigationMenu()
        {
            try
            {
                var nav = this.FindName("CodeNav") as NavigationView;
                if (nav == null) return;
                nav.MenuItems.Clear();

                // "All" entry with icon for compact mode
                var allItem = new NavigationViewItem { Content = "All Snippets", Tag = "all", Icon = new SymbolIcon(Symbol.AllApps) };
                nav.MenuItems.Add(allItem);

                // Categories from Preferences → Code categories (groups) with filters
                var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                var categories = settings?.GetCodeCategories() ?? new List<Pivot.Models.CodeCategory>();
                // hide default 'Languages' category so user-defined categories (tags) appear instead
                try { categories = categories.Where(c => !string.Equals(c.Name, "Languages", StringComparison.OrdinalIgnoreCase)).ToList(); } catch { }
                var filters = settings?.GetCodeFilters() ?? new List<Pivot.Models.CustomFilter>();

                if (categories.Any())
                {
                    nav.MenuItems.Add(new NavigationViewItemSeparator());
                    foreach (var cat in categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name))
                    {
                        var parent = new NavigationViewItem { Content = cat.Name, Icon = new SymbolIcon(Symbol.Folder) };
                        foreach (var fid in cat.FilterIds)
                        {
                            var f = filters.FirstOrDefault(x => x.Id == fid);
                            if (f == null) continue;
                            parent.MenuItems.Add(new NavigationViewItem { Content = f.Name, Tag = f.Id, Icon = new SymbolIcon(Symbol.Document) });
                        }
                        nav.MenuItems.Add(parent);
                    }
                }
                else
                {
                    // Fallback: list all filters flat
                    nav.MenuItems.Add(new NavigationViewItemSeparator());
                    foreach (var f in filters.OrderBy(f => f.SortOrder).ThenBy(f => f.Name))
                    {
                        nav.MenuItems.Add(new NavigationViewItem { Content = f.Name, Tag = f.Id, Icon = new SymbolIcon(Symbol.Document) });
                    }
                }

                nav.SelectedItem = allItem;
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
                    if (ViewModel?.SelectedSnippet != null) _ = CloseSnippetWithAnimationAsync();
                    return;
                }

                if (Guid.TryParse(tagStr, out var filterId))
                {
                    ViewModel.ActiveFilters.Clear();
                    ViewModel.ActiveFilters.Add(filterId);
                    ViewModel.FilterSnippets();
                    if (ViewModel?.SelectedSnippet != null) _ = CloseSnippetWithAnimationAsync();
                }
                else
                {
                    // If the clicked item is a category parent (no Tag), try to find the category and apply all its filters
                    try
                    {
                        var settings = App.Current.Services.GetService(typeof(SettingsService)) as SettingsService;
                        var catName = item.Content?.ToString() ?? string.Empty;
                        if (settings != null && !string.IsNullOrWhiteSpace(catName))
                        {
                            var cats = settings.GetCodeCategories() ?? new List<Pivot.Models.CodeCategory>();
                            var cat = cats.FirstOrDefault(c => string.Equals(c.Name, catName, StringComparison.OrdinalIgnoreCase));
                            if (cat != null)
                            {
                                ViewModel.ActiveFilters.Clear();
                                foreach (var fid in cat.FilterIds) ViewModel.ActiveFilters.Add(fid);
                                ViewModel.FilterSnippets();
                                if (ViewModel?.SelectedSnippet != null) _ = CloseSnippetWithAnimationAsync();
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
            Debug.WriteLine("OpenSnippetWithAnimationAsync: called");
            if (_isAnimationActive) { Debug.WriteLine("OpenSnippetWithAnimationAsync: animation active, returning"); return; }
            _isAnimationActive = true;
            var root = this.Content as FrameworkElement;
            var list = root?.FindName("SnippetListView") as ListView;
            var editorPanel = root?.FindName("EditorPanel") as Grid;
            var cardPanel = root?.FindName("CardPanel") as Grid;
            var placeholder = root?.FindName("PlaceholderBorder") as Border;

            // Temporarily disable list interaction during open animation
            if (list != null)
            {
                list.IsHitTestVisible = false;
            }

            UIElement? source = null;
            try
            {
                if (list != null && ViewModel?.SelectedSnippet != null)
                {
                    Debug.WriteLine($"OpenSnippetWithAnimationAsync: SelectedSnippet='{ViewModel.SelectedSnippet?.Title}'");
                    list.ScrollIntoView(ViewModel.SelectedSnippet);
                    await Task.Delay(80).ConfigureAwait(false);
                    App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => { });
                    var container = list.ContainerFromItem(ViewModel.SelectedSnippet) as ListViewItem;
                    Debug.WriteLine($"OpenSnippetWithAnimationAsync: container={(container==null?"null":"found")}");
                    if (container != null)
                    {
                        // Prefer ListView helper to prepare connected animation from item -> named element inside template
                        try
                        {
                            _pendingOpenAnimation = list.PrepareConnectedAnimation("OpenSnippet", ViewModel.SelectedSnippet, "SnippetCardBorder");
                            Debug.WriteLine($"OpenSnippetWithAnimationAsync: PrepareConnectedAnimation returned {(_pendingOpenAnimation==null?"null":"animation")}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"OpenSnippetWithAnimationAsync: PrepareConnectedAnimation threw: {ex}");
                            // Fallback: try to find the element and prepare via ConnectedAnimationService
                            source = FindDescendantByName(container, "SnippetCardBorder") as UIElement;
                            if (source != null)
                            {
                                try { _pendingOpenAnimation = ConnectedAnimationService.GetForCurrentView()?.PrepareToAnimate("OpenSnippet", source); Debug.WriteLine($"OpenSnippetWithAnimationAsync: Fallback prepare returned {(_pendingOpenAnimation==null?"null":"animation")} "); } catch (Exception ex2) { Debug.WriteLine($"OpenSnippet fallback threw: {ex2}"); _pendingOpenAnimation = null; }
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
                    if (ViewModel?.SelectedSnippet != null)
                    {
                        if (scratchEditor != null) scratchEditor.Text = ViewModel.SelectedSnippet.Content ?? string.Empty;
                        if (titleBox != null) titleBox.Text = ViewModel.SelectedSnippet.Title ?? string.Empty;
                    }
                    RefreshScratchpadTags();
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

            _previousSelectedSnippet = ViewModel?.SelectedSnippet;
            _isAnimationActive = false;

            // Re-enable list interaction after open animation
            if (list != null)
            {
                list.IsHitTestVisible = true;
            }

            // refresh tags for scratchpad UI
            try { RefreshScratchpadTags(); } catch { }
            await Task.CompletedTask;
            return;
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
                        if (scratchEditor != null) scratchEditor.Text = vm.SelectedSnippet.Content ?? string.Empty;

                        // refresh tags
                        try { RefreshScratchpadTags(); } catch { }

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
                        var tagsBox = new TextBox { PlaceholderText = "Tags (comma separated)", Text = snippet?.Tags ?? string.Empty, Margin = new Thickness(0,0,0,8) };

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
                        panel.Children.Add(tagsBox);
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
                                            snippet.Tags = tagsBox.Text ?? string.Empty;
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
                                    snippet.Tags = tagsBox.Text ?? string.Empty;
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

        private async Task CloseSnippetWithAnimationAsync()
        {
            if (_isAnimationActive) return;
            _isAnimationActive = true;
            var root = this.Content as FrameworkElement;
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
                                Debug.WriteLine("CloseSnippetWithAnimationAsync: attempting TryStartConnectedAnimationAsync via ListView helper");
                                await list.TryStartConnectedAnimationAsync(_pendingCloseAnimation, _previousSelectedSnippet, "SnippetCardBorder");
                                _pendingCloseAnimation = null;
                            }
                            catch (System.Runtime.InteropServices.COMException ex)
                            {
                                Debug.WriteLine($"TryStartConnectedAnimationAsync threw: {ex}");
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
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"CloseSnippetWithAnimationAsync fallback overall threw: {ex}");
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
                        if (vm != null) await vm.SaveSnippetFileAsync(prev);
                    }
                    catch { }
                }
            }
            catch { }

            _previousSelectedSnippet = null;
            _isAnimationActive = false;

            // clear scratchpad/editor tag lists
            try
            {
                var tagsControl = this.FindName("ScratchpadTagList") as ItemsControl;
                var editorTags = this.FindName("EditorTagList") as ItemsControl;
                if (tagsControl != null) App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => tagsControl.ItemsSource = null);
                if (editorTags != null) App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => editorTags.ItemsSource = null);
            }
            catch { }
        }

        private async void SnippetListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (_isAnimationActive) { Debug.WriteLine("SnippetListView_ItemClick: Animation is active, ignoring click."); return; }
            // If a snippet is already open, close it before opening another
            if (ViewModel != null && ViewModel.SelectedSnippet != null)
            {
                await CloseSnippetWithAnimationAsync();
            }
            if (e.ClickedItem is Pivot.CodeModule.Models.CodeFile clickedSnippet)
            {
                if (ViewModel != null)
                {
                    Debug.WriteLine($"SnippetListView_ItemClick: clicked='{clickedSnippet?.Title}'");
                    // If this is the placeholder "New" card (Id == Guid.Empty), create a new snippet instead
                    if (clickedSnippet != null && clickedSnippet.Id == Guid.Empty)
                    {
                        try { ViewModel.AddSnippetCommand?.Execute(null); } catch { }
                        return;
                    }

                    ViewModel.SelectedSnippet = clickedSnippet;
                    // Fallback: explicitly push content to the editor and open it in case ViewModel change didn't trigger the UI update
                    try
                    {
                        _ = SendSelectedSnippetToEditorAsync();
                        _ = OpenSnippetWithAnimationAsync();
                    }
                    catch { }
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

                var ctrlDown2 = IsControlDown();
                if (e.Key == Windows.System.VirtualKey.Enter && ctrlDown2)
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
                            // Scroll into view
                            try
                            {
                                var list = this.FindName("SnippetListView") as ListView;
                                list?.ScrollIntoView(newSnippet);
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
                        Debug.WriteLine("Root_PointerPressed: ScratchpadContainer missing/zero-size — treating as outside. Closing snippet.");
                        if (ViewModel != null) ViewModel.SelectedSnippet = null;
                        try { await CloseSnippetWithAnimationAsync(); } catch { }
                        return;
                    }
                    var pt = e.GetCurrentPoint(rootUi).Position;
                    var transform = container.TransformToVisual(rootUi);
                    var bounds = transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
                    if (!bounds.Contains(pt))
                    {
                        Debug.WriteLine("Root_PointerPressed: Click outside ScratchpadContainer. Closing snippet.");
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
                    Debug.WriteLine("Root_PointerPressed: Click outside EditorPanel. Closing snippet.");
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
                        Debug.WriteLine("Root_PointerReleased: ScratchpadContainer missing/zero-size — treating as outside. Closing snippet.");
                        if (ViewModel != null) ViewModel.SelectedSnippet = null;
                        try { await CloseSnippetWithAnimationAsync(); } catch { }
                        return;
                    }
                    var pt = e.GetCurrentPoint(rootUi).Position;
                    var transform = container.TransformToVisual(rootUi);
                    var bounds = transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
                    if (!bounds.Contains(pt))
                    {
                        Debug.WriteLine("Root_PointerReleased: Click outside ScratchpadContainer. Closing snippet.");
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
                    Debug.WriteLine("Root_PointerReleased: Click outside EditorPanel. Closing snippet.");
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
            Debug.WriteLine("ScratchpadCloseButton clicked");
            CloseScratchpadSimple();
        }

        private void CloseScratchpadSimple()
        {
            try
            {
                var root = this.Content as FrameworkElement;
                var overlay = root?.FindName("ScratchpadOverlay") as UIElement;
                if (overlay != null) overlay.Visibility = Visibility.Collapsed;
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

                // Clear scratchpad tag state
                RefreshScratchpadTags();
            }
            catch { }
        }

        // Backdrop handlers removed — root-level handlers handle outside clicks now

        private void RefreshScratchpadTags()
        {
            try
            {
                var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                var scratchList = this.FindName("ScratchpadTagList") as ItemsControl;
                var editorList = this.FindName("EditorTagList") as ItemsControl;
                if (repo == null || (scratchList == null && editorList == null)) return;
                var tags = repo.GetAllTags().ToList();

                // convert to simple view items with IsChecked state
                var selected = ViewModel?.SelectedSnippet;
                var selectedTags = selected != null ? (selected.Tags ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var items = tags.Select(t => new TagItem { Id = t.Id, Name = t.Name, IsSelected = selectedTags.Contains(t.Name) }).ToList();

                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    try
                    {
                        if (scratchList != null) scratchList.ItemsSource = items;
                        if (editorList != null) editorList.ItemsSource = items;
                    }
                    catch { }

                    // ensure toggle states reflect IsSelected after containers are realized
                    try
                    {
                        if (scratchList != null)
                        {
                            for (int i = 0; i < items.Count; i++)
                            {
                                var container = scratchList.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                                if (container == null) continue;
                                var toggle = FindDescendantOfType<ToggleButton>(container);
                                if (toggle != null) toggle.IsChecked = items[i].IsSelected;
                            }
                        }

                        if (editorList != null)
                        {
                            for (int i = 0; i < items.Count; i++)
                            {
                                var container = editorList.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                                if (container == null) continue;
                                var toggle = FindDescendantOfType<ToggleButton>(container);
                                if (toggle != null) toggle.IsChecked = items[i].IsSelected;
                            }
                        }
                    }
                    catch { }
                });
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
                        if (scratchEditor != null) scratchEditor.Text = content;
                        if (titleBox != null) titleBox.Text = vm.SelectedSnippet.Title ?? string.Empty;
                    }
                    catch { }
                });
            }
            catch { }
            return Task.CompletedTask;
        }

        private void ScratchpadTag_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(sender is ToggleButton tb) || tb.DataContext == null) return;
                // Using TagItem directly
                if (!(tb.DataContext is TagItem tagItem)) return;

                var tagName = tagItem.Name ?? string.Empty;
                if (ViewModel?.SelectedSnippet == null) return;
                var snippet = ViewModel.SelectedSnippet;
                var current = (snippet.Tags ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                if (tb.IsChecked == true)
                {
                    if (!current.Contains(tagName, StringComparer.OrdinalIgnoreCase)) current.Add(tagName);
                }
                else
                {
                    current.RemoveAll(x => string.Equals(x, tagName, StringComparison.OrdinalIgnoreCase));
                }
                snippet.Tags = string.Join(",", current);
                // persist
                try { var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository; repo?.Save(snippet); } catch { }
            }
            catch { }
        }

        private void CodeEditor_TextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
                var tb = sender as TextBox;
                if (tb == null) return;
                if (ViewModel != null && ViewModel.SelectedSnippet != null)
                {
                    ViewModel.SelectedSnippet.Content = tb.Text ?? string.Empty;
                    ViewModel.IsDirty = true;
                    // auto-save on input
                    try { _ = ViewModel.SaveSnippetFileAsync(ViewModel.SelectedSnippet); } catch { }
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
                    ViewModel.SelectedSnippet.Content = tb.Text ?? string.Empty;
                    ViewModel.IsDirty = true;
                    // auto-save on input
                    try { _ = ViewModel.SaveSnippetFileAsync(ViewModel.SelectedSnippet); } catch { }
                }
            }
            catch { }
        }

        private void ScratchpadTitleBox_TextChanged(object? sender, TextBoxTextChangingEventArgs e)
        {
            try
            {
                var tb = sender as TextBox;
                if (tb == null) return;
                if (ViewModel != null && ViewModel.SelectedSnippet != null)
                {
                    ViewModel.SelectedSnippet.Title = tb.Text ?? string.Empty;
                    ViewModel.IsDirty = true;
                    try { _ = ViewModel.SaveSnippetFileAsync(ViewModel.SelectedSnippet); } catch { }
                }
            }
            catch { }
        }

        private async void ScratchpadDeleteButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel?.SelectedSnippet == null) return;
                var snippet = ViewModel.SelectedSnippet;
                var dlg = new ContentDialog { Title = "Delete this snippet?", PrimaryButtonText = "Delete", CloseButtonText = "Cancel" };
                dlg.XamlRoot = this.XamlRoot;
                var result = await dlg.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

                var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                try { if (snippet.Id != Guid.Empty) repo?.Delete(snippet.Id); } catch { }
                try { ViewModel.Snippets.Remove(snippet); } catch { }
                ViewModel.SelectedSnippet = null;
                try { await CloseSnippetWithAnimationAsync(); } catch { }
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
                    // try to resolve from ViewModel
                    snippet = ViewModel?.Snippets?.FirstOrDefault(s => s.Id == id);
                }
                if (snippet == null || snippet.Id == Guid.Empty) return; // don't delete placeholder

                var dlg = new ContentDialog { Title = "Delete this snippet?", PrimaryButtonText = "Delete", CloseButtonText = "Cancel" };
                dlg.XamlRoot = this.XamlRoot;
                var result = await dlg.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

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

                var dlg = new ContentDialog { Title = "Delete this snippet?", PrimaryButtonText = "Delete", CloseButtonText = "Cancel" };
                dlg.XamlRoot = this.XamlRoot;
                var result = await dlg.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

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
                Debug.WriteLine("CodeEditor got focus: enabling tab-insert mode.");
            }
            catch { }
        }

        private void CodeEditor_LostFocus(object? sender, RoutedEventArgs e)
        {
            try
            {
                _isCodeEditorTabMode = false;
                Debug.WriteLine("CodeEditor lost focus: disabling tab-insert mode.");
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
                Debug.WriteLine("QuickAddBox got focus: enabling tab-insert mode.");
            }
            catch { }
        }
    }
}


