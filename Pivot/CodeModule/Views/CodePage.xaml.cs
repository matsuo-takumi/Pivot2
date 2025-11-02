using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pivot.CodeModule.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using System.Text.Json;
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

// WebView2 core types are used if available at runtime
using Microsoft.Web.WebView2.Core;

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
                var saveBtn = this.FindName("ScratchpadSaveButton") as Button;
                var closeBtn = this.FindName("ScratchpadCloseButton") as Button;
                if (saveBtn != null) saveBtn.Click += (_, __) => { if (ViewModel != null && ViewModel.SelectedSnippet != null) _ = ViewModel.SaveSnippetFileAsync(ViewModel.SelectedSnippet); };
                if (closeBtn != null) closeBtn.Click += (_, __) => { if (ViewModel != null) ViewModel.SelectedSnippet = null; };
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

        private bool _webMessageHooked = false;
        private bool _scratchpadWebMessageHooked = false;

        private void CoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                var json = args.WebMessageAsJson;
                if (string.IsNullOrEmpty(json)) return;
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("action", out var actionEl)) return;
                var action = actionEl.GetString();

                if (action == "contentChanged")
                {
                    var content = doc.RootElement.GetProperty("payload").GetProperty("content").GetString();
                    App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                    {
                        if (ViewModel != null && ViewModel.SelectedSnippet != null)
                        {
                            ViewModel.SelectedSnippet.Content = content ?? string.Empty;
                            ViewModel.IsDirty = true;
                        }
                    });
                }
                else if (action == "save")
                {
                    App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(async () =>
                    {
                        if (ViewModel != null && ViewModel.SaveSnippetCommand != null)
                        {
                            if (ViewModel.SaveSnippetCommand is IAsyncRelayCommand asyncCmd)
                            {
                                await asyncCmd.ExecuteAsync(null);
                            }
                            else if (ViewModel.SaveSnippetCommand.CanExecute(null))
                            {
                                ViewModel.SaveSnippetCommand.Execute(null);
                            }
                        }
                    });
                }
            }
            catch { }
        }

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

            UIElement? source = null;
            try
            {
                if (list != null && ViewModel?.SelectedSnippet != null)
                {
                    list.ScrollIntoView(ViewModel.SelectedSnippet);
                    await Task.Delay(80).ConfigureAwait(false);
                    App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() => { });
                    var container = list.ContainerFromItem(ViewModel.SelectedSnippet) as ListViewItem;
                    if (container != null)
                    {
                        source = FindDescendantByName(container, "SnippetCardBorder") as UIElement;
                        if (source != null)
                        {
                            try { _pendingOpenAnimation = ConnectedAnimationService.GetForCurrentView()?.PrepareToAnimate("OpenSnippet", source); } catch { _pendingOpenAnimation = null; }
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
                }
                catch { }
            }
            catch { }

            // Start connected animation to editor header (or editor panel)
            try
            {
                        App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                        {
                            try
                            {
                                var target = root?.FindName("EditorHeader") as UIElement ?? (UIElement?)editorPanel;
                                if (_pendingOpenAnimation != null && target != null)
                                {
                                    try { _pendingOpenAnimation.TryStart(target); } catch { }
                                    _pendingOpenAnimation = null;
                                }

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
                                    Storyboard.SetTargetProperty(scaleX, "RenderTransform.ScaleX");
                                    Storyboard.SetTarget(scaleY, header);
                                    Storyboard.SetTargetProperty(scaleY, "RenderTransform.ScaleY");
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
                        var container = list.ContainerFromItem(_previousSelectedSnippet) as ListViewItem;
                        if (container != null)
                        {
                            var target = FindDescendantByName(container, "SnippetCardBorder") as UIElement;
                            if (target != null)
                            {
                                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                                {
                                    try { if (_pendingCloseAnimation != null) { _pendingCloseAnimation.TryStart(target); _pendingCloseAnimation = null; } } catch { }
                                });
                            }
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
            }
            catch { }

            // detach pointer handler
            try
            {
                var rootUi = this.Content as UIElement;
                if (rootUi != null) rootUi.PointerPressed -= Root_PointerPressed;
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

        private void Root_PointerPressed(object? sender, PointerRoutedEventArgs e)
        {
            try
            {
                var rootUi = sender as FrameworkElement;
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
                        if (ViewModel != null) ViewModel.SelectedSnippet = null;
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
                }
            }
            catch { }
        }

        private void RefreshScratchpadTags()
        {
            try
            {
                var repo = App.Current.Services.GetService(typeof(ICodeRepository)) as ICodeRepository;
                var list = this.FindName("ScratchpadTagList") as ItemsControl;
                if (repo == null || list == null) return;
                var tags = repo.GetAllTags().ToList();
                // convert to simple view items with IsChecked state
                var items = tags.Select(t => new { Id = t.Id, Name = t.Name }).ToList();
                App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    list.ItemsSource = items;
                    // set toggle states based on selected snippet once containers are realized
                    try
                    {
                        var selected = ViewModel?.SelectedSnippet;
                        var selectedTags = selected != null ? (selected.Tags ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < items.Count; i++)
                        {
                            var item = items[i];
                            var container = list.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                            if (container == null) continue;
                            var toggle = FindDescendantOfType<ToggleButton>(container);
                            if (toggle != null)
                            {
                                toggle.IsChecked = selectedTags.Contains(item.Name);
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

        private async Task SendSelectedSnippetToEditorAsync()
        {
            if (CodeEditor == null) return;
            var vm = ViewModel;
            if (vm?.SelectedSnippet == null) return;
            try
            {
                // Ensure CoreWebView2 is created
                try { await CodeEditor.EnsureCoreWebView2Async(); } catch { }

                var payload = new
                {
                    action = "load",
                    payload = new
                    {
                        id = vm.SelectedSnippet.Id,
                        title = vm.SelectedSnippet.Title,
                        language = vm.SelectedSnippet.Language,
                        content = vm.SelectedSnippet.Content
                    }
                };
                var json = JsonSerializer.Serialize(payload);
                if (CodeEditor.CoreWebView2 != null)
                {
                    // attach handler once
                    if (!_webMessageHooked)
                    {
                        CodeEditor.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                        _webMessageHooked = true;
                    }

                    CodeEditor.CoreWebView2.PostWebMessageAsJson(json);
                }
                // also send to scratchpad editor if present
                try
                {
                    var sp = this.FindName("ScratchpadEditor") as WebView2;
                    if (sp != null)
                    {
                        try { await sp.EnsureCoreWebView2Async(); } catch { }
                        if (sp.CoreWebView2 != null)
                        {
                            if (!_scratchpadWebMessageHooked)
                            {
                                sp.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                                _scratchpadWebMessageHooked = true;
                            }
                            sp.CoreWebView2.PostWebMessageAsJson(json);
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        private void ScratchpadTag_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(sender is ToggleButton tb) || tb.DataContext == null) return;
                var tagName = tb.Content?.ToString() ?? string.Empty;
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


