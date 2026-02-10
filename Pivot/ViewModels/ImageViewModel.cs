using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Engine.Models;
using Pivot.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Services;
using Pivot.Engine.Services;

using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;
using Pivot.Engine.Messages;
using Microsoft.UI.Dispatching;

namespace Pivot.ViewModels
{
    public partial class ImageViewModel : ObservableObject, 
        IRecipient<DirectoryChangedMessage>
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly ThemeSettingsService? _themeSettings;
        private readonly DirectorySettingsService? _directorySettings;
        private readonly IMessenger? _messenger;
        
        private readonly SmartFolderService _smartFolderService;

        // Navigation
        public ObservableCollection<FolderNode> FolderTree { get; } = new();
        public ObservableCollection<FolderNode> SmartFolderNodes { get; } = new();

        [ObservableProperty]
        private FolderNode? _selectedFolder;

        // Service dependencies for Partial Classes
        private readonly IAnalysisQueryService? _analysisService;

        // Legacy layout properties (retained for binding compatibility if any, but simplified)
        [ObservableProperty]
        private LayoutType _currentLayout = LayoutType.Grid;

        [ObservableProperty]
        private double _selectionBorderThickness = 2.0;

        /// <summary>
        /// DI-compatible constructor. Use this when resolving via service provider.
        /// </summary>
        public ImageViewModel(
            ThemeSettingsService themeSettings,
            DirectorySettingsService directorySettings,
            SmartFolderService smartFolderService,
            IMessenger messenger,
            IAnalysisQueryService analysisService)
        {
            _themeSettings = themeSettings;
            _directorySettings = directorySettings;
            _smartFolderService = smartFolderService;
            _messenger = messenger;
            _analysisService = analysisService;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            
            _messenger?.RegisterAll(this);
            
            if (_themeSettings != null)
            {
                SelectionBorderThickness = _themeSettings.ImageSelectionBorderThickness;
            }
            
            // Load smart folders
            _ = LoadSmartFoldersAsync();
        }

        partial void OnCurrentLayoutChanged(LayoutType value)
        {
            // Retained for potential binding compatibility
        }

        public async void Receive(DirectoryChangedMessage message)
        {
            if (message.Value.Category == DirectoryCategory.Image && _directorySettings != null)
            {
               // Just rebuild tree, no asset loading
               Initialize(_directorySettings.ImageDirectories);
            }
        }

        public void Initialize(IEnumerable<string> rootDirectories)
        {
            // Delegate to centralized utility
            Utilities.DirectoryTreeBuilder.BuildTree(FolderTree, rootDirectories);
        }

        public async Task LoadSmartFoldersAsync()
        {
            try
            {
                var folders = await _smartFolderService.GetSmartFoldersAsync();
                
                _dispatcherQueue.TryEnqueue(() =>
                {
                    SmartFolderNodes.Clear();
                    foreach (var folder in folders)
                    {
                        // Use a special URI scheme for Smart Folders: "smart:{ID}"
                        // Store the serialized criteria in Tag or retrieve it later? 
                        // FolderNode doesn't have Tag. We'll use ID in FullPath and fetch from service or maintain a dictionary.
                        // Actually, simpler to just store ID in path.
                        
                        var node = new FolderNode(folder.Name, $"smart:{folder.Id}");
                        // We can't store the criteria directly in FolderNode without modifying it.
                        // But we can fetch it or just use the ID to look it up if we cached it.
                        // For now, let's keep it simple.
                        SmartFolderNodes.Add(node);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading smart folders: {ex.Message}");
            }
        }

        public async Task<SmartFolder?> GetSmartFolderByIdAsync(int id)
        {
            var folders = await _smartFolderService.GetSmartFoldersAsync();
            return folders.FirstOrDefault(f => f.Id == id);
        }

        public async Task<FilterCriteria?> GetSmartFolderCriteriaAsync(int id)
        {
            var folder = await GetSmartFolderByIdAsync(id);
            if (folder != null && !string.IsNullOrEmpty(folder.CriteriaJson))
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<FilterCriteria>(folder.CriteriaJson);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deserializing criteria for folder {id}: {ex.Message}");
                }
            }
            return null;
        }
    }
}
