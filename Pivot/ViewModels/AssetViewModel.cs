using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;
using Pivot.Models;
using Pivot.Services;
using Pivot.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;

namespace Pivot.ViewModels
{
    public partial class AssetViewModel : ObservableObject, 
        IRecipient<DirectoryChangedMessage>
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly DirectorySettingsService? _directorySettings;
        private readonly IMessenger? _messenger;
        
        // Folder navigation
        public ObservableCollection<FolderNode> FolderTree { get; } = new();
        
        [ObservableProperty]
        private FolderNode? _selectedFolder;
        
        [ObservableProperty]
        private TemplateItem? _selectedAsset;

        /// <summary>
        /// DI-compatible constructor.
        /// </summary>
        public AssetViewModel(
            DirectorySettingsService directorySettings,
            IMessenger messenger)
        {
            _directorySettings = directorySettings;
            _messenger = messenger;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            
            _messenger?.RegisterAll(this);
        }

        public void Receive(DirectoryChangedMessage message)
        {
            if (message.Value.Category == DirectoryCategory.Asset && _directorySettings != null)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    Initialize(_directorySettings.AssetDirectories);
                });
            }
        }

        public void Initialize(IEnumerable<string> rootDirectories)
        {
            DirectoryTreeBuilder.BuildTree(FolderTree, rootDirectories);
        }
    }
}
