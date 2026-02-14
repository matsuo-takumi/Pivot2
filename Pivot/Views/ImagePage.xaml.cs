using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;
using Pivot.ViewModels;
using System.ComponentModel;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Services;
using Pivot.Engine.Models;
using Pivot.Models;
using System;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Pivot.Controls;

namespace Pivot.Views
{
    public sealed partial class ImagePage : Page, IRecipient<SettingsChangedMessage>
    {
        public ImageViewModel ViewModel { get; }

        public ImagePage()
        {
            this.InitializeComponent();
            
            // Get ViewModel from DI container (proper dependency injection)
            ViewModel = App.Current.Services.GetRequiredService<ImageViewModel>();
            this.DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.FolderTree.CollectionChanged += FolderTree_CollectionChanged;
            ViewModel.SmartFolderNodes.CollectionChanged += FolderTree_CollectionChanged;

            // Initialize BrowserControl after Loaded event
            this.Loaded += ImagePage_Loaded;

            try
            {
                var dirSettings = App.Current.Services.GetService<DirectorySettingsService>();
                if (dirSettings != null)
                {
                    // Use ImageDirectories from DirectorySettingsService
                    if (dirSettings.ImageDirectories != null && dirSettings.ImageDirectories.Count > 0)
                    {
                         ViewModel.Initialize(dirSettings.ImageDirectories);
                    }
                }
            }
            catch { }

            this.Unloaded += ImagePage_Unloaded;

            // Register for messages
            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this);
        }
        
        private async void ImagePage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize BrowserControl with Image assets
                await BrowserControl.InitializeAsync(AssetKind.Image);
            }
            catch { }
        }
        
        // Event handlers for BrowserControl
        private void BrowserControl_ItemClicked(object? sender, AssetModel asset)
        {
            // Selection handling
            System.Diagnostics.Debug.WriteLine($"Item clicked: {asset.FileName}");
        }
        
        private void BrowserControl_ItemDoubleClicked(object? sender, AssetModel asset)
        {
            try
            {
                // Show preview using centralized mapper
                var item = AssetMapper.ToPreviewItem(asset.Entity);
                _ = PreviewControl.ShowAsync(item);
            }
            catch { }
        }
        
        private void BrowserControl_ItemRightTapped(object? sender, (AssetModel Asset, Windows.Foundation.Point Position) args)
        {
            _rightTappedAsset = args.Asset;
            
            if (Resources.TryGetValue("BrowserItemContextMenu", out var menuObj) && menuObj is MenuFlyout menu)
            {
                menu.ShowAt(BrowserControl, args.Position);
            }
        }
        
        private AssetModel? _rightTappedAsset;
        
        private void OpenDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (_rightTappedAsset == null || string.IsNullOrEmpty(_rightTappedAsset.FilePath)) return;
            
            try
            {
                var filePath = _rightTappedAsset.FilePath;
                if (System.IO.File.Exists(filePath))
                {
                    // Open Explorer and select the file
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else
                {
                    // If file doesn't exist, just open the directory
                    var directory = System.IO.Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && System.IO.Directory.Exists(directory))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", directory);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenDirectory failed: {ex.Message}");
            }
        }

        public void Receive(SettingsChangedMessage message)
        {
            if (message.Value == "ImageSelectionBorderThickness" && ViewModel != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        var themeSettings = App.Current.Services.GetService<ThemeSettingsService>();
                        if (themeSettings != null)
                        {
                            var newThickness = themeSettings.ImageSelectionBorderThickness;
                            if (Math.Abs(ViewModel.SelectionBorderThickness - newThickness) > 0.01)
                            {
                                ViewModel.SelectionBorderThickness = newThickness;
                            }
                        }
                    }
                    catch { }
                });
            }
        }



        private void FolderTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is FolderNode node)
            {
                ViewModel.SelectedFolder = node;
            }
        }

        private void ImagePage_Unloaded(object sender, RoutedEventArgs e)
        {
            try { ViewModel.FolderTree.CollectionChanged -= FolderTree_CollectionChanged; } catch { }
            try { ViewModel.SmartFolderNodes.CollectionChanged -= FolderTree_CollectionChanged; } catch { }
            // Clean up message registration
            try { WeakReferenceMessenger.Default.UnregisterAll(this); } catch { }

            try { PreviewControl.Close(); } catch { }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImageViewModel.CurrentLayout))
            {
                if (ViewModel != null)
                {
                    ApplyLayout(ViewModel.CurrentLayout);
                }
            }
        }

        private void ApplyLayout(LayoutType layout)
        {
            // Layout is now handled by BrowserControl
            // This method is kept for backward compatibility
        }

        // ... Existing Pointer Handlers ...

        private void FolderTree_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() => BuildNavigationMenuItems());
        }


        private void BuildNavigationMenuItems()
        {
            try
            {
                FolderNavigationView.MenuItems.Clear();

                // 1. Smart Folders
                if (ViewModel.SmartFolderNodes.Count > 0)
                {
                    var smartHeader = new NavigationViewItemHeader { Content = "Smart Folders" };
                    FolderNavigationView.MenuItems.Add(smartHeader);

                    foreach (var node in ViewModel.SmartFolderNodes)
                    {
                        var navItem = CreateNavigationViewItem(node);
                        navItem.Icon = new FontIcon { Glyph = "\uE8B9", FontFamily = new FontFamily("Segoe MDL2 Assets") }; // Auto-filter icon
                        FolderNavigationView.MenuItems.Add(navItem);
                    }

                    FolderNavigationView.MenuItems.Add(new NavigationViewItemSeparator());
                }

                // 2. File System Folders
                var folderHeader = new NavigationViewItemHeader { Content = "Folders" };
                FolderNavigationView.MenuItems.Add(folderHeader);

                foreach (var node in ViewModel.FolderTree)
                {
                    var navItem = CreateNavigationViewItem(node);
                    FolderNavigationView.MenuItems.Add(navItem);
                }
            }
            catch { }
        }

        private NavigationViewItem CreateNavigationViewItem(FolderNode node)
        {
            var item = new NavigationViewItem
            {
                Content = node.Name,
                Tag = node
            };

            var icon = new FontIcon
            {
                Glyph = "\uE8B7",
                FontFamily = new FontFamily("Segoe MDL2 Assets")
            };

            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var accentColor))
            {
                icon.Foreground = new SolidColorBrush((Windows.UI.Color)accentColor);
            }

            item.Icon = icon;

            // Recursively add children
            foreach (var child in node.Children)
            {
                item.MenuItems.Add(CreateNavigationViewItem(child));
            }

            return item;
        }

        private async void FolderNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is FolderNode node)
            {
                ViewModel.SelectedFolder = node;
                
                if (node.FullPath.StartsWith("smart:"))
                {
                    // Handle Smart Folder
                    // Format: smart:{ID}
                    if (int.TryParse(node.FullPath.Substring(6), out int id))
                    {
                        var criteria = await ViewModel.GetSmartFolderCriteriaAsync(id);
                        if (criteria != null)
                        {
                            BrowserControl.SetFilterCriteria(criteria);
                        }
                    }
                }
                else
                {
                    // Regular Folder
                    BrowserControl.SetDirectoryFilter(node.FullPath);
                }
            }
            else
            {
                // Clear filter when no folder selected
                BrowserControl.SetDirectoryFilter(null);
            }
        }

        private void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear folder selection and show all images
            FolderNavigationView.SelectedItem = null;
            ViewModel.SelectedFolder = null;
            BrowserControl.SetDirectoryFilter(null);
        }

    }
}

