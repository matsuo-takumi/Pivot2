using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;
using Microsoft.UI.Dispatching;

namespace Pivot.ViewModels
{
    public partial class ImageViewModel : ObservableObject, 
        IRecipient<DirectoryChangedMessage>
    {
        private readonly DispatcherQueue _dispatcherQueue;
        
        // Navigation
        public ObservableCollection<FolderNode> FolderTree { get; } = new();

        [ObservableProperty]
        private FolderNode? _selectedFolder;

        // Legacy layout properties (retained for binding compatibility if any, but simplified)
        [ObservableProperty]
        private LayoutType _currentLayout = LayoutType.Grid;

        [ObservableProperty]
        private double _selectionBorderThickness = 2.0;

        private DirectorySettingsService? _directorySettings;
        private readonly IMessenger? _messenger;
        private IEnumerable<string>? _currentDirectories;

        public ImageViewModel()
        {
            try
            {
                var services = App.Current.Services;
                var themeSettings = services.GetService<ThemeSettingsService>();
                _directorySettings = services.GetService<DirectorySettingsService>();
                _messenger = services.GetService<IMessenger>();

                _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            
                if (_messenger != null)
                {
                    _messenger.RegisterAll(this);
                }

                if (themeSettings != null)
                {
                    SelectionBorderThickness = themeSettings.ImageSelectionBorderThickness;
                }
            }
            catch { }
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
