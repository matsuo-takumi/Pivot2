using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;
using Pivot.Engine.Messages;
using Pivot.ViewModels;
using Pivot.Services;
using Pivot.Engine.Models;
using Pivot.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Pivot.Views
{
    public sealed partial class AssetPage : Page, 
        IRecipient<AssetsSyncedMessage>,
        IRecipient<AssetEntityChangedMessage>
    {
        public AssetViewModel ViewModel { get; }
        
        private readonly ObservableCollection<AssetEntity> _assets = new();
        private readonly AssetQueryService _queryService;

        public AssetPage()
        {
            this.InitializeComponent();
            
            // Get services from DI
            ViewModel = App.Current.Services.GetRequiredService<AssetViewModel>();
            _queryService = App.Current.Services.GetRequiredService<AssetQueryService>();
            
            this.DataContext = ViewModel;
            
            FileListView.ItemsSource = _assets;
            
            this.Loaded += AssetPage_Loaded;
            this.Unloaded += AssetPage_Unloaded;
            
            // Register for messages
            WeakReferenceMessenger.Default.Register<AssetsSyncedMessage>(this);
            WeakReferenceMessenger.Default.Register<AssetEntityChangedMessage>(this);
        }
        
        private async void AssetPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAssetsAsync();
        }
        
        private const int PageSize = 500; // Load in smaller batches for faster initial display
        
        private async Task LoadAssetsAsync()
        {
            try
            {
                var criteria = new FilterCriteria
                {
                    TargetKind = AssetKind.Model3D,
                    SortField = Pivot.Services.SortField.Name,
                    SortDirection = Pivot.Services.SortDirection.Ascending
                };
                
                // Run query on background thread to avoid blocking UI
                var assets = await Task.Run(async () => 
                    await _queryService.QueryAsync(criteria, 0, PageSize));
                
                DispatcherQueue.TryEnqueue(() =>
                {
                    _assets.Clear();
                    foreach (var asset in assets)
                    {
                        _assets.Add(asset);
                    }
                    FileCountText.Text = $"{_assets.Count} items";
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAssetsAsync failed: {ex.Message}");
            }
        }
        
        private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileListView.SelectedItem is AssetEntity asset)
            {
                ViewModel.SelectedAsset = AssetMapper.ToTemplateItem(asset);
                SelectedModelText.Text = asset.FileName;
            }
        }
        
        public void Receive(AssetsSyncedMessage message)
        {
            // Reload when assets are synced
            DispatcherQueue.TryEnqueue(async () =>
            {
                await LoadAssetsAsync();
            });
        }
        
        public void Receive(AssetEntityChangedMessage message)
        {
            // Reload when individual asset changes
            if (message.Asset?.Kind == AssetKind.Model3D || message.Type == AssetChangeType.Deleted)
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await LoadAssetsAsync();
                });
            }
        }

        private void AssetPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try { WeakReferenceMessenger.Default.UnregisterAll(this); } catch { }
        }
        
        /// <summary>
        /// Format file size for display in XAML (used by x:Bind).
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}
