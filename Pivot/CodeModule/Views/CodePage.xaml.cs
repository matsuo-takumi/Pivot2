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

using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.CodeModule.Models;
using Pivot.Services;
using Pivot.Messages;
using Pivot.Utilities;
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

        #region Cached UI Elements
        // Cached references to frequently accessed UI elements to avoid repeated FindName calls
        private ListView? _snippetListView;
        private Grid? _scratchpadOverlay;
        private Border? _scratchpadContainer;
        private TextBox? _scratchpadEditor;
        private TextBox? _scratchpadTitleBox;
        private TextBox? _codeEditor;
        private TextBox? _quickAddBox;
        private NavigationView? _codeNav;
        private Border? _placeholderBorder;
        private Button? _scratchpadCloseButton;
        private Grid? _cardPanel;
        private Grid? _editorPanel;
        private InfoBar? _quickAddInfoBar;
        private bool _uiElementsCached = false;
        #endregion

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
            
            // Cache UI element references for performance
            EnsureUIElementsCached();
            try
            {
                var messenger = App.Current.Services.GetService(typeof(IMessenger)) as IMessenger;
                messenger?.Register<CodePage, CodeFiltersUpdatedMessage>(this, (r, m) => r.OnCodeFiltersUpdated(m.Value));
            }
            catch { }
            // Restore previously selected snippet (persisted) if available
            try
            {
                var browserSettings = App.Current.Services.GetService(typeof(BrowserSettingsService)) as BrowserSettingsService;
                if (browserSettings != null && ViewModel != null)
                {
                    var lastId = browserSettings.LastSelectedSnippetId;
                    if (lastId != null && lastId != Guid.Empty)
                    {
                        var found = ViewModel.Snippets.FirstOrDefault(s => s.Id == lastId);
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

        #region UI Element Caching and Helpers

        /// <summary>
        /// Caches references to frequently accessed UI elements.
        /// Call this once after InitializeComponent to avoid repeated FindName calls.
        /// </summary>
        private void EnsureUIElementsCached()
        {
            if (_uiElementsCached) return;
            
            try
            {
                _snippetListView = this.FindName("SnippetListView") as ListView;
                _scratchpadOverlay = this.FindName("ScratchpadOverlay") as Grid;
                _scratchpadContainer = this.FindName("ScratchpadContainer") as Border;
                _scratchpadEditor = this.FindName("ScratchpadEditor") as TextBox;
                _scratchpadTitleBox = this.FindName("ScratchpadTitleBox") as TextBox;
                _codeEditor = this.FindName("CodeEditor") as TextBox;
                _quickAddBox = this.FindName("QuickAddBox") as TextBox;
                _codeNav = this.FindName("CodeNav") as NavigationView;
                _placeholderBorder = this.FindName("PlaceholderBorder") as Border;
                _scratchpadCloseButton = this.FindName("ScratchpadCloseButton") as Button;
                _cardPanel = this.FindName("CardPanel") as Grid;
                _editorPanel = this.FindName("EditorPanel") as Grid;
                _quickAddInfoBar = this.FindName("QuickAddInfoBar") as InfoBar;
                
                _uiElementsCached = true;
                System.Diagnostics.Debug.WriteLine("CodePage: UI elements cached successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodePage: Error caching UI elements: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the interaction state of the snippet list.
        /// </summary>
        private void SetListInteractionEnabled(bool enabled)
        {
            EnsureUIElementsCached();
            if (_snippetListView == null) return;
            
            _snippetListView.IsHitTestVisible = enabled;
            _snippetListView.IsItemClickEnabled = enabled;
            if (enabled)
            {
                _snippetListView.SelectionMode = ListViewSelectionMode.Single;
            }
        }

        /// <summary>
        /// Updates placeholder visibility based on whether snippets exist.
        /// </summary>
        private void UpdatePlaceholderVisibility()
        {
            EnsureUIElementsCached();
            if (_placeholderBorder == null || ViewModel?.Snippets == null) return;
            
            _placeholderBorder.Visibility = ViewModel.Snippets.Any() 
                ? Visibility.Collapsed 
                : Visibility.Visible;
        }



        #endregion












        // Backdrop handlers removed — root-level handlers handle outside clicks now




    }


}


