using Microsoft.UI.Xaml.Controls;
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
using Windows.Foundation;
using System.Collections.Generic;
using Pivot.CodeModule.Services;
using Microsoft.UI.Xaml.Controls.Primitives;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.CodeModule.Models;

// Note: editing is implemented with WinUI TextBox controls (replaced WebView2)

namespace Pivot.CodeModule.Views
{
    public sealed partial class CodePage : Page
    {
        public CodeViewModel? ViewModel => DataContext as CodeViewModel;
        private Pivot.CodeModule.Models.CodeFile? _previousSelectedSnippet;

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
                            }
                        }
                        catch { }
                    };
                }
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

            // ESC key to close
            try
            {
                var esc = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
                esc.Invoked += (_, __) => { if (ViewModel != null) ViewModel.SelectedSnippet = null; };
                this.KeyboardAccelerators.Add(esc);
            }
            catch { }

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
                    _ = OpenSnippetWithAnimationAsync();
                }
                else
                {
                    _ = CloseSnippetWithAnimationAsync();
                }
            }
        }

        private async Task OpenSnippetWithAnimationAsync()
        {
            if (_isAnimationActive) return;
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
            catch { }

            // show editor and hide cards
            try
            {
                if (editorPanel != null)
                {
                    editorPanel.Opacity = 0;
                    editorPanel.Visibility = Visibility.Visible;
                }
                if (cardPanel != null) cardPanel.Visibility = Visibility.Collapsed;
                if (placeholder != null) placeholder.Visibility = Visibility.Collapsed;
                // show scratchpad overlay
                try
                {
                    var overlay = root?.FindName("ScratchpadOverlay") as Grid;
                    if (overlay != null) overlay.Visibility = Visibility.Visible;
                                Debug.WriteLine($"OpenSnippetWithAnimationAsync: overlay visibility set to {(overlay==null?"null":overlay.Visibility.ToString())}");
                // disable close button while opening
                try
                {
                    var closeBtn = this.FindName("ScratchpadCloseButton") as Button;
                    if (closeBtn != null) closeBtn.IsEnabled = false;
                }
                catch { }
                }
                catch { }
            }
            catch { }

            // Start connected animation to editor header (or editor panel)
            try
            {
                        App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(async () => // Make the lambda async
                        {
                            try
                            {
                                var target = root?.FindName("ScratchpadContainer") as UIElement ?? (UIElement?)editorPanel;
                                Debug.WriteLine($"OpenSnippetWithAnimationAsync: target for TryStart is {(target==null?"null":target.ToString())}, overlay visibility={(root?.FindName("ScratchpadOverlay") as Grid)?.Visibility}");
                                // ensure scratchpad content is populated before running the connected animation
                                try
                                {
                                    var scratchEditor = this.FindName("ScratchpadEditor") as TextBox;
                                    if (scratchEditor != null && ViewModel?.SelectedSnippet != null)
                                    {
                                        scratchEditor.Text = ViewModel.SelectedSnippet.Content ?? string.Empty;
                                    }
                                }
                                catch { }

                                // Ensure overlay is visible before starting animation
                                var overlayElement = root?.FindName("ScratchpadOverlay") as Grid;
                                if (overlayElement != null)
                                {
                                    overlayElement.Visibility = Visibility.Visible;
                                    // Force layout update after setting visibility
                                    overlayElement.UpdateLayout();
                                    await Task.Delay(20).ConfigureAwait(true); // Short delay to allow layout to settle
                                }

                                if (_pendingOpenAnimation != null && target != null)
                                {
                                    try { _pendingOpenAnimation.TryStart(target); Debug.WriteLine("OpenSnippetWithAnimationAsync: TryStart called on pending animation."); } catch (Exception ex) { Debug.WriteLine($"OpenSnippetWithAnimationAsync: TryStart threw: {ex}"); }
                                    _pendingOpenAnimation = null;

                                    // ensure scratchpad editor is focused after animation
                                    try
                                    {
                                        var sp = this.FindName("ScratchpadEditor") as TextBox;
                                        if (sp != null)
                                        {
                                            App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                                            {
                                                try { sp.Focus(Microsoft.UI.Xaml.FocusState.Programmatic); Debug.WriteLine("ScratchpadEditor focused."); } catch (Exception ex) { Debug.WriteLine($"Focus failed: {ex}"); }
                                            });
                                        }
                                    }
                                    catch (Exception ex) { Debug.WriteLine($"Post-start focus/action threw: {ex}"); }
                                }
                                else
                                {
                                    // Fallback: show overlay and focus editor even if ConnectedAnimation not prepared
                                    try
                                    {
                                        var overlay = root?.FindName("ScratchpadOverlay") as Grid;
                                        var sp = this.FindName("ScratchpadEditor") as TextBox;
                                        if (overlay != null) overlay.Visibility = Visibility.Visible;
                                        Debug.WriteLine($"OpenSnippetWithAnimationAsync: fallback overlay visibility set to {(overlay==null?"null":overlay.Visibility.ToString())}");
                                        if (sp != null)
                                        {
                                            App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => { try { sp.Focus(Microsoft.UI.Xaml.FocusState.Programmatic); Debug.WriteLine("Fallback: ScratchpadEditor focused."); } catch (Exception ex) { Debug.WriteLine($"Fallback focus failed: {ex}"); } });
                                        }
                                        Debug.WriteLine("OpenSnippetWithAnimationAsync: fallback path used (no pending animation)");
                                    }
                                    catch (Exception ex) { Debug.WriteLine($"Fallback show/focus threw: {ex}"); }
                                }

                                // re-enable close button after open complete
                                try
                                {
                                    var closeBtn = this.FindName("ScratchpadCloseButton") as Button;
                                    if (closeBtn != null) closeBtn.IsEnabled = true;
                                }
                                catch { }

                                // run a fade-in for editor panel
                                if (editorPanel != null)
                                {
                                    var fade = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(220)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                                    var sb = new Storyboard();
                                    Storyboard.SetTarget(fade, editorPanel);
                                    Storyboard.SetTargetProperty(fade, "Opacity");
                                    sb.Children.Add(fade);
                                    sb.Begin();
                                }

                                var header = root?.FindName("EditorHeader") as FrameworkElement;
                                if (header != null)
                                {
                                    header.RenderTransform = new ScaleTransform { ScaleX = 0.94, ScaleY = 0.94 };
                                    var scaleX = new DoubleAnimation { From = 0.94, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(260)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                                    var scaleY = new DoubleAnimation { From = 0.94, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(260)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                                    var sb2 = new Storyboard();
                                    Storyboard.SetTarget(scaleX, header);
                                        Storyboard.SetTargetProperty(scaleX, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
                                        Storyboard.SetTarget(scaleY, header);
                                        Storyboard.SetTargetProperty(scaleY, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");
                                    sb2.Children.Add(scaleX);
                                    sb2.Children.Add(scaleY);
                                    sb2.Begin();
                                }
                            }
                            catch { }
                        });
            }
            catch { }

            // attach pointer handler to detect clicks outside editor
            try
            {
                var rootUi = this.Content as UIElement;
                if (rootUi != null)
                {
                    rootUi.PointerPressed -= Root_PointerPressed;
                    rootUi.PointerPressed += Root_PointerPressed;
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
            }
            catch { }

            // detach pointer handler
            try
            {
                var rootUi = this.Content as UIElement;
                if (rootUi != null) rootUi.PointerPressed -= Root_PointerPressed;
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

            // clear scratchpad tag list
            try
            {
                var tagsControl = this.FindName("ScratchpadTagList") as ItemsControl;
                if (tagsControl != null) App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => tagsControl.ItemsSource = null);
            }
            catch { }
        }

        private void SnippetListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (_isAnimationActive) { Debug.WriteLine("SnippetListView_ItemClick: Animation is active, ignoring click."); return; }
            if (e.ClickedItem is Pivot.CodeModule.Models.CodeFile clickedSnippet)
            {
                if (ViewModel != null)
                {
                    Debug.WriteLine($"SnippetListView_ItemClick: clicked='{clickedSnippet?.Title}'");
                    ViewModel.SelectedSnippet = clickedSnippet;
                }
            }
        }

        private async void Root_PointerPressed(object? sender, PointerRoutedEventArgs e)
        {
            try
            {
                var rootUi = this.Content as FrameworkElement;
                if (rootUi == null) return;
                var overlay = rootUi.FindName("ScratchpadOverlay") as FrameworkElement;
                if (overlay != null && overlay.Visibility == Visibility.Visible)
                {
                    var container = rootUi.FindName("ScratchpadContainer") as FrameworkElement;
                    if (container == null) return;
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

        private async void ScratchpadOverlay_PointerPressed(object? sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (!(sender is FrameworkElement overlay)) return;
                var container = overlay.FindName("ScratchpadContainer") as FrameworkElement;
                if (container == null) return;
                var pt = e.GetCurrentPoint(overlay).Position;
                var transform = container.TransformToVisual(overlay);
                var bounds = transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
                if (!bounds.Contains(pt))
                {
                    Debug.WriteLine("ScratchpadOverlay_PointerPressed: Click outside ScratchpadContainer. Closing snippet.");
                    // mark handled so other handlers don't interfere
                    try { e.Handled = true; } catch { }

                    // clear selection on viewmodel
                    if (ViewModel != null) ViewModel.SelectedSnippet = null;

                    // ensure close sequence runs even if binding didn't trigger (fallback)
                    try { await CloseSnippetWithAnimationAsync(); } catch { }
                }
            }
            catch { }
        }

        // Purchase button removed - handler intentionally deleted

        private async void ScratchpadCloseButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("ScratchpadCloseButton clicked");
                // run the close animation which also hides overlay and clears previous selection
                try { await CloseSnippetWithAnimationAsync(); } catch { }
            }
            catch { }
        }

        // Backdrop handlers removed — root-level handlers handle outside clicks now

        private void RefreshScratchpadTags()
        {
            try
            {
                var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                var list = this.FindName("ScratchpadTagList") as ItemsControl;
                if (repo == null || list == null) return;
                var tags = repo.GetAllTags().ToList();
                // convert to simple view items with IsChecked state
                var selected = ViewModel?.SelectedSnippet;
                var selectedTags = selected != null ? (selected.Tags ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var items = tags.Select(t => new TagItem { Id = t.Id, Name = t.Name, IsSelected = selectedTags.Contains(t.Name) }).ToList();

                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    list.ItemsSource = items;
                    // set toggle states based on selected snippet once containers are realized
                    // (this block can be removed as IsSelected is set above)
                    try
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            var item = items[i];
                            var container = list.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                            if (container == null) continue;
                            var toggle = FindDescendantOfType<ToggleButton>(container);
                            if (toggle != null)
                            {
                                toggle.IsChecked = item.IsSelected; // Use the IsSelected from TagItem
                            }
                        }
                    }
                    catch { }
                });
            }
            catch { }
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
                        if (codeEditor != null) codeEditor.Text = content;
                        if (scratchEditor != null) scratchEditor.Text = content;
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
    }
}


