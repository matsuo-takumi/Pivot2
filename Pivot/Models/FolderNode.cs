using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Pivot.Models
{
    public partial class FolderNode : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        [ObservableProperty]
        private bool _isExpanded;

        public ObservableCollection<FolderNode> Children { get; } = new();

        public FolderNode(string name, string fullPath)
        {
            Name = name;
            FullPath = fullPath;
        }
    }
}
