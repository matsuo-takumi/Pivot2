using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Engine.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Services;

using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;
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
        
        // Navigation
        public ObservableCollection<FolderNode> FolderTree { get; } = new();

        [ObservableProperty]
        private FolderNode? _selectedFolder;

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
            IMessenger messenger)
        {
            _themeSettings = themeSettings;
            _directorySettings = directorySettings;
            _messenger = messenger;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            
            _messenger?.RegisterAll(this);
            
            if (_themeSettings != null)
            {
                SelectionBorderThickness = _themeSettings.ImageSelectionBorderThickness;
            }
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
    }
}
