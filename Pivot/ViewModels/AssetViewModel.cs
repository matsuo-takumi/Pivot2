using CommunityToolkit.Mvvm.ComponentModel;
using Pivot.Models;

namespace Pivot.ViewModels
{
    public partial class AssetViewModel : ObservableObject
    {
        [ObservableProperty]
        private TemplateItem? _selectedAsset;

        public AssetViewModel()
        {
        }
    }
}
