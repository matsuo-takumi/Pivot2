using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pivot.Engine.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Pivot.ViewModels
{
    /// <summary>
    /// ViewModel for a group of duplicate assets
    /// </summary>
    public partial class DuplicateGroupViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<AssetEntity> _assets;

        [ObservableProperty]
        private AssetEntity? _selectedToKeep;

        public DuplicateGroupViewModel(List<AssetEntity> assets)
        {
            Assets = new ObservableCollection<AssetEntity>(assets);
            SelectedToKeep = assets.FirstOrDefault();
        }

        [RelayCommand]
        private async Task ResolveAsync()
        {
            // TODO: Implement deletion logic
            // Delete all assets except SelectedToKeep
            await Task.CompletedTask;
        }
    }
}
